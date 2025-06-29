using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace FiniteState.Editor
{
    public static class DiagramTransitionEditor
    {
        private static bool isCreatingTransition = false;
        private static StateData sourceState = null;
        
        public static void DrawTransitionCreationControls(List<StateData> states, Dictionary<string, Vector2> statePositions, GUIStyle boxStyle)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("➕ Add Transitions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            var modeColor = isCreatingTransition ? new Color(0.2f, 0.8f, 0.2f) : Color.white;
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = modeColor;
            
            if (GUILayout.Button(isCreatingTransition ? "✅ Creating Mode" : "➕ Create Mode", GUILayout.Width(120)))
            {
                isCreatingTransition = !isCreatingTransition;
                if (!isCreatingTransition) sourceState = null;
            }
            
            GUI.backgroundColor = originalBg;
            
            if (isCreatingTransition)
            {
                EditorGUILayout.LabelField("Click source state, then target state", EditorStyles.miniLabel);
                
                if (GUILayout.Button("❌ Cancel", GUILayout.Width(60)))
                {
                    isCreatingTransition = false;
                    sourceState = null;
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (isCreatingTransition && sourceState != null)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"Source: {sourceState.stateName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("→ Click target state", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        public static bool HandleTransitionCreationInput(
            Vector2 nodePos, 
            StateData state, 
            List<StateData> states,
            Dictionary<string, Vector2> statePositions)
        {
            if (!isCreatingTransition) return false;
            
            Rect nodeRect = new Rect(nodePos.x, nodePos.y, 120, 70);
            
            if (nodeRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (sourceState == null)
                    {
                        // First click - select source state
                        sourceState = state;
                        Debug.Log($"🎯 Source state selected: {sourceState.stateName}");
                        Event.current.Use();
                        return true;
                    }
                    else if (sourceState != state)
                    {
                        // Second click - create transition
                        CreateTransition(sourceState, state, states);
                        sourceState = null; // Continue in creation mode
                        Event.current.Use();
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        public static void DrawTransitionPreview(Dictionary<string, Vector2> statePositions)
        {
            if (!isCreatingTransition || sourceState == null) return;
            
            if (statePositions.ContainsKey(sourceState.stateName))
            {
                Vector2 sourcePos = statePositions[sourceState.stateName] + new Vector2(60, 35);
                Vector2 mousePos = Event.current.mousePosition;
                
                // Draw preview line
                Handles.BeginGUI();
                Handles.color = new Color(1f, 1f, 0f, 0.7f);
                
                // Dashed line
                Vector2 direction = (mousePos - sourcePos).normalized;
                float distance = Vector2.Distance(sourcePos, mousePos);
                
                for (int i = 0; i < distance; i += 20)
                {
                    Vector2 start = sourcePos + direction * i;
                    Vector2 end = sourcePos + direction * Mathf.Min(i + 10, distance);
                    Handles.DrawLine(start, end);
                }
                
                Handles.EndGUI();
                
                EditorWindow.focusedWindow?.Repaint();
            }
        }
        
        private static void CreateTransition(StateData fromState, StateData toState, List<StateData> states)
        {
            // Check if transition already exists
            int targetIndex = states.IndexOf(toState) + 1;
            bool exists = fromState.transitions.Any(t => t.targetStateIndex == targetIndex);
            
            if (exists)
            {
                Debug.LogWarning($"⚠️ Transition already exists: {fromState.stateName} → {toState.stateName}");
                return;
            }
            
            // Create new transition
            var transition = new TransitionData
            {
                targetStateIndex = targetIndex,
                autoDetected = false
            };
            
            // Auto-suggest condition based on state names
            string suggestedCondition = SuggestConditionForTransition(fromState.stateName, toState.stateName);
            if (!string.IsNullOrEmpty(suggestedCondition))
            {
                AddConditionToTransition(transition, suggestedCondition);
            }
            
            fromState.transitions.Add(transition);
            
            Debug.Log($"✅ Created transition: {fromState.stateName} → {toState.stateName}" +
                     (string.IsNullOrEmpty(suggestedCondition) ? "" : $" ({suggestedCondition})"));
        }
        
        private static string SuggestConditionForTransition(string fromState, string toState)
        {
            string from = fromState.ToLower();
            string to = toState.ToLower();
            
            if (from.Contains("moving"))
            {
                return "ArrivedCondition";
            }
            else if (from.Contains("served") || from.Contains("service"))
            {
                return "ServiceCompletedCondition";
            }
            else if (from.Contains("waiting"))
            {
                if (to.Contains("leaving"))
                    return "PatienceExpiredCondition";
                else
                    return "QueuePositionChangedCondition";
            }
            
            return null;
        }
        
        private static void AddConditionToTransition(TransitionData transition, string conditionName)
        {
            string[] guids = AssetDatabase.FindAssets($"{conditionName} t:Script");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass()?.Name == conditionName)
                {
                    transition.conditionScripts.Add(script);
                    break;
                }
            }
        }
        
        public static bool IsInCreationMode()
        {
            return isCreatingTransition;
        }
        
        public static void ResetCreationMode()
        {
            isCreatingTransition = false;
            sourceState = null;
        }
    }
}