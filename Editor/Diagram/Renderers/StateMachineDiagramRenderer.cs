using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace FiniteState.Editor
{
    public static class StateMachineDiagramRenderer
    {
        private static string hoveredTooltip = "";
        private static Vector2 tooltipPosition;
        
        public static void DrawStateDiagram(
            List<StateData> states,
            Dictionary<string, Vector2> statePositions,
            ref Vector2 diagramScrollPosition,
            ref string draggingState,
            ref Vector2 dragOffset,
            GUIStyle boxStyle)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("🔗 State Diagram", EditorStyles.boldLabel);
            
            var diagramRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, 400);
            EditorGUI.DrawRect(diagramRect, new Color(0.15f, 0.15f, 0.15f, 1f));
            
            diagramScrollPosition = GUI.BeginScrollView(
                diagramRect,
                diagramScrollPosition,
                new Rect(0, 0, 1000, 1000)
            );
            
            // Draw connections (only junction system)
            DiagramJunctionRenderer.DrawJunctionConnections(states, statePositions);
            
            // Draw state nodes
            DrawStateNodes(states, statePositions, ref draggingState, ref dragOffset);
            
            // Handle dragging
            HandleDiagramInput(ref draggingState, ref dragOffset, statePositions);
            
            // Draw tooltip
            DrawTooltip();
            
            GUI.EndScrollView();
            
            // Controls
            DrawDiagramControls(states, statePositions);
            
            // Transition creator
            DiagramTransitionEditor.DrawTransitionCreationControls(states, statePositions, boxStyle);
            
            EditorGUILayout.EndVertical();
        }
        
        private static void DrawStateNodes(
            List<StateData> states, 
            Dictionary<string, Vector2> statePositions,
            ref string draggingState,
            ref Vector2 dragOffset)
        {
            hoveredTooltip = "";
            
            foreach (var state in states)
            {
                if (state.script == null) continue;
                
                if (!statePositions.ContainsKey(state.stateName))
                {
                    statePositions[state.stateName] = new Vector2(
                        states.IndexOf(state) * 150 + 50, 100
                    );
                }
                
                Vector2 nodePos = statePositions[state.stateName];
                
                // Draw node
                DiagramNodeRenderer.DrawStateNode(nodePos, state);
                
                // Handle interaction
                HandleNodeInteraction(nodePos, state, states, statePositions, ref draggingState, ref dragOffset);
            }
        }
        
        private static void HandleNodeInteraction(
            Vector2 nodePos, 
            StateData state, 
            List<StateData> states,
            Dictionary<string, Vector2> statePositions,
            ref string draggingState, 
            ref Vector2 dragOffset)
        {
            Rect nodeRect = new Rect(nodePos.x, nodePos.y, 120, 70);
            Vector2 mousePos = Event.current.mousePosition;
            
            if (nodeRect.Contains(mousePos))
            {
                // Set tooltip
                hoveredTooltip = GetStateTooltip(state);
                tooltipPosition = mousePos;
                
                // Handle transition creation mode
                if (DiagramTransitionEditor.HandleTransitionCreationInput(nodePos, state, states, statePositions))
                {
                    return;
                }
                
                // Handle normal interaction
                if (Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        if (Event.current.clickCount == 2)
                        {
                            // Double click - open script
                            if (state.script != null)
                                AssetDatabase.OpenAsset(state.script);
                            Event.current.Use();
                        }
                        else
                        {
                            // Single click - start drag
                            draggingState = state.stateName;
                            dragOffset = mousePos - nodePos;
                            Event.current.Use();
                        }
                    }
                    else if (Event.current.button == 1)
                    {
                        // Right click - context menu
                        ShowNodeContextMenu(state, states, statePositions);
                        Event.current.Use();
                    }
                }
            }
        }
        
        private static void HandleDiagramInput(
            ref string draggingState, 
            ref Vector2 dragOffset, 
            Dictionary<string, Vector2> statePositions)
        {
            if (draggingState != null)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    statePositions[draggingState] = Event.current.mousePosition - dragOffset;
                    GUI.changed = true;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    draggingState = null;
                    Event.current.Use();
                }
            }
        }
        
        private static void DrawDiagramControls(List<StateData> states, Dictionary<string, Vector2> statePositions)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Layout buttons
            if (GUILayout.Button("🎯 Auto Arrange", GUILayout.Width(100)))
            {
                AutoArrangeNodes(states, statePositions);
            }
            
            if (GUILayout.Button("⭕ Circle", GUILayout.Width(60)))
            {
                CircleLayoutNodes(states, statePositions);
            }
            
            GUILayout.FlexibleSpace();
            
            // State count
            int validStates = states.Count(s => s.script != null);
            EditorGUILayout.LabelField($"States: {validStates}/{states.Count}", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private static void AutoArrangeNodes(List<StateData> states, Dictionary<string, Vector2> statePositions)
        {
            float radius = Mathf.Max(150f, states.Count * 20f);
            Vector2 center = new Vector2(400, 300);
            
            for (int i = 0; i < states.Count; i++)
            {
                if (!string.IsNullOrEmpty(states[i].stateName))
                {
                    float angle = (float)i / states.Count * 2 * Mathf.PI;
                    statePositions[states[i].stateName] = center + new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius
                    );
                }
            }
        }
        
        private static void CircleLayoutNodes(List<StateData> states, Dictionary<string, Vector2> statePositions)
        {
            var validStates = states.Where(s => !string.IsNullOrEmpty(s.stateName)).ToList();
            Vector2 center = new Vector2(400, 300);
            float radius = 200f;
            
            for (int i = 0; i < validStates.Count; i++)
            {
                float angle = (float)i / validStates.Count * 2 * Mathf.PI - Mathf.PI / 2;
                statePositions[validStates[i].stateName] = center + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
            }
        }
        
        private static void ShowNodeContextMenu(StateData state, List<StateData> states, Dictionary<string, Vector2> statePositions)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Set as Initial"), state.isInitialState, () => {
                foreach (var s in states) s.isInitialState = false;
                state.isInitialState = true;
            });
            
            menu.AddItem(new GUIContent("Add Transition"), false, () => {
                state.transitions.Add(new TransitionData());
            });
            
            menu.AddSeparator("");
            
            if (state.script != null)
            {
                menu.AddItem(new GUIContent("Open Script"), false, () => {
                    AssetDatabase.OpenAsset(state.script);
                });
            }
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Remove State"), false, () => {
                states.Remove(state);
                if (statePositions.ContainsKey(state.stateName))
                    statePositions.Remove(state.stateName);
            });
            
            menu.ShowAsContext();
        }
        
        private static string GetStateTooltip(StateData state)
        {
            string tooltip = state.stateName;
            if (state.isInitialState) tooltip += "\n🏁 Initial State";
            if (state.autoDetected) tooltip += "\n🔍 Auto-Detected";
            tooltip += $"\n🔗 {state.transitions.Count} transitions";
            return tooltip;
        }
        
        private static void DrawTooltip()
        {
            if (!string.IsNullOrEmpty(hoveredTooltip))
            {
                var style = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 10,
                    padding = new RectOffset(8, 8, 6, 6)
                };
                
                Vector2 size = style.CalcSize(new GUIContent(hoveredTooltip));
                Rect tooltipRect = new Rect(tooltipPosition.x + 15, tooltipPosition.y - size.y - 5, size.x, size.y);
                
                GUI.Box(tooltipRect, hoveredTooltip, style);
            }
        }
    }
}