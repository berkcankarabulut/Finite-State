using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using FiniteState.Runtime;

namespace FiniteState.Editor
{
    public class StateMachineEditor : EditorWindow
    {
        private MonoBehaviour selectedOwner;
        private List<StateData> states = new List<StateData>();
        private Vector2 scrollPosition;
        private Vector2 diagramScrollPosition;
        private string stateMachineName = "MyStateMachine";
        
        // Visual properties
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle stateNodeStyle;
        private Color accentColor = new Color(0.3f, 0.7f, 1f, 1f);
        private bool stylesInitialized = false;
        
        // View modes
        public enum ViewMode { List, Diagram }
        private ViewMode currentViewMode = ViewMode.List;
        
        // Auto-detection properties
        private bool foundExistingStateMachine = false;
        private Type detectedStateMachineType;
        private GameObject selectedPrefab;
        
        // Diagram properties
        private Dictionary<string, Vector2> statePositions = new Dictionary<string, Vector2>();
        private Vector2 dragOffset;
        private string draggingState = null;
        private float nodeWidth = 120f;
        private float nodeHeight = 60f;
        
        [MenuItem("Tools/State Machine Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<StateMachineEditor>("State Machine Editor");
            window.minSize = new Vector2(600, 700);
        }
        
        private void OnGUI()
        {
            if (!stylesInitialized)
                InitializeStyles();
            
            DrawHeader();
            DrawOwnerSelection();
            
            if (selectedOwner == null)
            {
                EditorGUILayout.HelpBox("🎯 Please select a MonoBehaviour owner to continue", MessageType.Info);
                return;
            }
            
            DrawAutoDetectionSection();
            DrawViewModeToggle();
            DrawStateMachineNameField();
            
            if (currentViewMode == ViewMode.List)
            {
                DrawStatesSection();
            }
            else
            {
                DrawStateDiagram();
            }
            
            DrawGenerateButton();
        }
        
        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = accentColor },
                alignment = TextAnchor.MiddleCenter
            };
            
            boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };
            
            buttonStyle = new GUIStyle("button")
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            stateNodeStyle = new GUIStyle("box")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            stylesInitialized = true;
        }
        
        private void DrawHeader()
        {
            StateMachineUIDrawer.DrawHeader(headerStyle);
        }
        
        private void DrawOwnerSelection()
        {
            StateMachineUIDrawer.DrawOwnerSelection(
                ref selectedOwner, 
                ref selectedPrefab, 
                boxStyle, 
                OnOwnerChanged, 
                OnPrefabSelected
            );
        }
        
        private void DrawAutoDetectionSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔍 Auto Detection", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🔄 Re-scan", GUILayout.Width(80)))
            {
                PerformAutoDetection();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            if (foundExistingStateMachine)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"✅ Found: {detectedStateMachineType?.Name}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"📊 Auto-detected {states.Count(s => s.autoDetected)} states", EditorStyles.miniLabel);
                
                var loadButtonColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.6f, 0.8f);
                if (GUILayout.Button("📥 Load Detected Structure", GUILayout.Height(25)))
                {
                    LoadDetectedStructure();
                }
                GUI.backgroundColor = loadButtonColor;
            }
            else
            {
                EditorGUILayout.LabelField("❌ No existing State Machine found", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawViewModeToggle()
        {
            StateMachineUIDrawer.DrawViewModeToggle(
                ref currentViewMode, 
                boxStyle, 
                InitializeDiagramPositions
            );
        }
        
        private void DrawStateMachineNameField()
        {
            StateMachineUIDrawer.DrawStateMachineNameField(ref stateMachineName, boxStyle);
        }
        
        private void DrawStatesSection()
        {
            // Modern header with gradient background
            var headerRect = EditorGUILayout.GetControlRect(false, 35);
            DrawGradientRect(headerRect, new Color(0.25f, 0.25f, 0.35f), new Color(0.15f, 0.15f, 0.25f));
            
            // Header content using absolute positioning
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            
            EditorGUI.LabelField(new Rect(headerRect.x + 10, headerRect.y + 5, 150, 25), "🔄 State Configuration", titleStyle);
            
            // Stats and controls
            int totalStates = states.Count;
            int validStates = states.Count(s => s.script != null);
            int totalTransitions = states.Sum(s => s.transitions.Count);
            
            var statsStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.8f, 0.8f, 0.9f) },
                fontSize = 10
            };
            
            string statsText = $"States: {validStates}/{totalStates} | Transitions: {totalTransitions}";
            EditorGUI.LabelField(new Rect(headerRect.x + 170, headerRect.y + 5, 200, 25), statsText, statsStyle);
            
            // Initial State Info
            var initialState = states.FirstOrDefault(s => s.isInitialState);
            if (initialState != null)
            {
                var initialStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.4f, 1f, 0.4f) },
                    fontStyle = FontStyle.Bold,
                    fontSize = 10
                };
                string initialText = $"🏁 Entry: {(string.IsNullOrEmpty(initialState.stateName) ? "Unnamed" : initialState.stateName)}";
                EditorGUI.LabelField(new Rect(headerRect.x + 170, headerRect.y + 17, 200, 25), initialText, initialStyle);
            }
            else
            {
                var noInitialStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 0.6f, 0.6f) },
                    fontStyle = FontStyle.Bold,
                    fontSize = 10
                };
                EditorGUI.LabelField(new Rect(headerRect.x + 170, headerRect.y + 17, 200, 25), "⚠️ No Initial State", noInitialStyle);
            }
            
            // Add State Button
            var addButtonRect = new Rect(headerRect.x + headerRect.width - 110, headerRect.y + 8, 100, 20);
            DrawModernButton(addButtonRect, "➕ Add State", new Color(0.2f, 0.7f, 0.3f), () => {
                states.Add(new StateData());
            });
            
            EditorGUILayout.Space(5);
            
            // Content area
            if (states.Count == 0)
            {
                DrawEmptyStateMessage();
            }
            else
            {
                DrawStatesContent();
            }
        }
        
        private void DrawEmptyStateMessage()
        {
            var emptyRect = EditorGUILayout.GetControlRect(false, 80);
            DrawRoundedRect(emptyRect, new Color(0.2f, 0.2f, 0.3f, 0.3f), 8f);
            
            var messageStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.7f, 0.8f) }
            };
            
            var iconStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 24
            };
            
            EditorGUI.LabelField(new Rect(emptyRect.x, emptyRect.y + 10, emptyRect.width, 30), "🔧", iconStyle);
            EditorGUI.LabelField(new Rect(emptyRect.x, emptyRect.y + 35, emptyRect.width, 20), "No states configured yet", messageStyle);
            EditorGUI.LabelField(new Rect(emptyRect.x, emptyRect.y + 50, emptyRect.width, 20), "Click 'Add State' to begin building your state machine", messageStyle);
        }
        
        private void DrawStatesContent()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            
            for (int i = 0; i < states.Count; i++)
            {
                DrawModernStateEditor(i);
                if (i < states.Count - 1)
                {
                    DrawStateSeparator();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawStateSeparator()
        {
            EditorGUILayout.Space(3);
        }
        
        private void DrawModernStateEditor(int index)
        {
            var state = states[index];
            
            // Simplified card layout using EditorGUILayout
            var cardStyle = new GUIStyle("box");
            
            if (state.isInitialState)
                cardStyle.normal.background = StateMachineUIDrawer.MakeTex(2, 2, new Color(0.15f, 0.35f, 0.2f, 0.9f));
            else if (state.autoDetected)
                cardStyle.normal.background = StateMachineUIDrawer.MakeTex(2, 2, new Color(0.25f, 0.15f, 0.35f, 0.9f));
            else
                cardStyle.normal.background = StateMachineUIDrawer.MakeTex(2, 2, new Color(0.2f, 0.25f, 0.35f, 0.9f));
            
            EditorGUILayout.BeginVertical(cardStyle);
            
            // Header
            DrawStateHeader(state, index);
            
            // Expanded content
            if (state.expanded)
            {
                EditorGUILayout.Space(5);
                DrawStateContent(state, index);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
        
        private void DrawStateHeader(StateData state, int index)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Expand button
            string expandIcon = state.expanded ? "▼" : "▶";
            if (GUILayout.Button(expandIcon, GUILayout.Width(24), GUILayout.Height(24)))
            {
                state.expanded = !state.expanded;
            }
            
            // State icon
            string stateIcon = state.isInitialState ? "🏁" : state.autoDetected ? "🔍" : "⚙️";
            EditorGUILayout.LabelField(stateIcon, GUILayout.Width(30));
            
            // Title and status
            string titleText = !string.IsNullOrEmpty(state.stateName) ? state.stateName : $"State {index + 1}";
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            
            EditorGUILayout.LabelField(titleText, titleStyle, GUILayout.Width(150));
            
            // Status badges
            if (state.isInitialState)
            {
                var initialBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.3f, 1f, 0.4f) },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField("INITIAL", initialBadgeStyle, GUILayout.Width(50));
            }
            
            if (state.autoDetected)
            {
                var autoBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.4f, 1f) },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField("AUTO", autoBadgeStyle, GUILayout.Width(40));
            }
            
            GUILayout.FlexibleSpace();
            
            // Action buttons
            Color initialColor = state.isInitialState ? new Color(0.3f, 1f, 0.4f) : new Color(0.5f, 0.5f, 0.6f);
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = initialColor;
            
            if (GUILayout.Button("🏁", GUILayout.Width(24), GUILayout.Height(20)))
            {
                if (!state.isInitialState)
                {
                    foreach (var s in states) s.isInitialState = false;
                    state.isInitialState = true;
                }
            }
            
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️", GUILayout.Width(24), GUILayout.Height(20)))
            {
                states.RemoveAt(index);
                if (statePositions.ContainsKey(state.stateName))
                    statePositions.Remove(state.stateName);
                GUI.backgroundColor = originalBg;
                EditorGUILayout.EndHorizontal();
                return;
            }
            GUI.backgroundColor = originalBg;
            
            EditorGUILayout.EndHorizontal();
            
            // Script field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Script:", GUILayout.Width(50));
            EditorGUI.BeginChangeCheck();
            state.script = (MonoScript)EditorGUILayout.ObjectField(state.script, typeof(MonoScript), false);
            if (EditorGUI.EndChangeCheck() && state.script != null)
            {
                state.stateName = state.script.GetClass()?.Name ?? "Unknown";
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawStateContent(StateData state, int index)
        {
            // Transitions section
            EditorGUILayout.BeginHorizontal();
            
            var transitionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 1f) }
            };
            
            EditorGUILayout.LabelField($"🔗 Transitions ({state.transitions.Count})", transitionTitleStyle, GUILayout.Width(150));
            
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.8f);
            if (GUILayout.Button("➕ Add", GUILayout.Width(60), GUILayout.Height(18)))
            {
                state.transitions.Add(new TransitionData());
            }
            GUI.backgroundColor = originalBg;
            
            EditorGUILayout.EndHorizontal();
            
            // Transitions list
            if (state.transitions.Count == 0)
            {
                var noTransitionsStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.7f, 0.8f) },
                    fontStyle = FontStyle.Italic
                };
                EditorGUILayout.LabelField("    No transitions configured", noTransitionsStyle);
            }
            else
            {
                for (int i = 0; i < state.transitions.Count; i++)
                {
                    DrawTransitionItem(state, i);
                }
            }
        }
        
        private void DrawTransitionItem(StateData state, int transitionIndex)
        {
            var transition = state.transitions[transitionIndex];
            
            var transitionStyle = new GUIStyle("box")
            {
                padding = new RectOffset(5, 5, 3, 3),
                margin = new RectOffset(15, 5, 2, 2)
            };
            
            if (transition.autoDetected)
            {
                transitionStyle.normal.background = StateMachineUIDrawer.MakeTex(2, 2, new Color(0.3f, 0.2f, 0.4f, 0.5f));
            }
            
            EditorGUILayout.BeginVertical(transitionStyle);
            
            // Transition Header
            EditorGUILayout.BeginHorizontal();
            
            // Expand/Collapse for transition details
            string expandIcon = transition.expanded ? "▼" : "▶";
            if (GUILayout.Button(expandIcon, GUILayout.Width(18), GUILayout.Height(18)))
            {
                transition.expanded = !transition.expanded;
            }
            
            // Target state info
            string targetName = GetTargetStateName(transition.targetStateIndex);
            var targetStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.LabelField($"→ {targetName}", targetStyle, GUILayout.Width(100));
            
            // Target state dropdown
            EditorGUI.BeginChangeCheck();
            int newTargetIndex = EditorGUILayout.Popup(transition.targetStateIndex - 1, GetStateNames(), GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                transition.targetStateIndex = newTargetIndex + 1;
            }
            
            // Conditions count
            string conditionsText = transition.conditionScripts.Count > 0 ? 
                $"❓ {transition.conditionScripts.Count}" : "⚡ Always";
                
            EditorGUILayout.LabelField(conditionsText, targetStyle, GUILayout.Width(60));
            
            GUILayout.FlexibleSpace();
            
            // Remove transition
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(18)))
            {
                state.transitions.RemoveAt(transitionIndex);
                GUI.backgroundColor = originalBg;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = originalBg;
            
            EditorGUILayout.EndHorizontal();
            
            // Expanded transition content - CONDITIONS
            if (transition.expanded)
            {
                EditorGUILayout.Space(3);
                DrawConditionsSection(transition);
            }
            
            EditorGUILayout.EndVertical();
        }
        
      
        
        private void DrawConditionItem(TransitionData transition, int conditionIndex)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Condition icon
            string conditionIcon = GetConditionIcon(transition.conditionScripts[conditionIndex]);
            EditorGUILayout.LabelField(conditionIcon, GUILayout.Width(20));
            
            // Condition Script Field
            EditorGUI.BeginChangeCheck();
            var newCondition = (MonoScript)EditorGUILayout.ObjectField(
                transition.conditionScripts[conditionIndex], 
                typeof(MonoScript), 
                false,
                GUILayout.Width(150)
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                transition.conditionScripts[conditionIndex] = newCondition;
            }
            
            // Condition name display
            if (transition.conditionScripts[conditionIndex] != null)
            {
                string conditionName = transition.conditionScripts[conditionIndex].GetClass()?.Name ?? "Unknown";
                var nameStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.8f, 0.8f, 0.9f) },
                    fontSize = 9
                };
                EditorGUILayout.LabelField(conditionName.Replace("Condition", ""), nameStyle, GUILayout.Width(80));
            }
            
            GUILayout.FlexibleSpace();
            
            // Remove Condition Button
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(16)))
            {
                transition.conditionScripts.RemoveAt(conditionIndex);
                GUI.backgroundColor = originalBg;
                EditorGUILayout.EndHorizontal();
                return;
            }
            GUI.backgroundColor = originalBg;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawStateHeader(StateData state, int index, Rect headerRect)
        {
            // Expand button
            var expandRect = new Rect(headerRect.x, headerRect.y, 24, 24);
            string expandIcon = state.expanded ? "▼" : "▶";
            DrawModernButton(expandRect, expandIcon, new Color(0.4f, 0.4f, 0.5f), () => {
                state.expanded = !state.expanded;
            });
            
            // State icon and title
            var iconRect = new Rect(headerRect.x + 30, headerRect.y, 30, 24);
            string stateIcon = state.isInitialState ? "🏁" : state.autoDetected ? "🔍" : "⚙️";
            
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(iconRect, stateIcon, iconStyle);
            
            // Title and status
            var titleRect = new Rect(headerRect.x + 65, headerRect.y, 200, 24);
            string titleText = !string.IsNullOrEmpty(state.stateName) ? state.stateName : $"State {index + 1}";
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            
            EditorGUI.LabelField(titleRect, titleText, titleStyle);
            
            // Status badges
            var badgeX = titleRect.x + titleStyle.CalcSize(new GUIContent(titleText)).x + 10;
            
            if (state.isInitialState)
            {
                DrawStatusBadge(new Rect(badgeX, headerRect.y + 2, 60, 16), "INITIAL", new Color(0.3f, 1f, 0.4f));
                badgeX += 65;
            }
            
            if (state.autoDetected)
            {
                DrawStatusBadge(new Rect(badgeX, headerRect.y + 2, 50, 16), "AUTO", new Color(0.7f, 0.4f, 1f));
            }
            
            // Script field
            var scriptRect = new Rect(headerRect.x + 65, headerRect.y + 22, 200, 18);
            EditorGUI.BeginChangeCheck();
            state.script = (MonoScript)EditorGUI.ObjectField(scriptRect, state.script, typeof(MonoScript), false);
            if (EditorGUI.EndChangeCheck() && state.script != null)
            {
                state.stateName = state.script.GetClass()?.Name ?? "Unknown";
            }
            
            // Action buttons
            var buttonX = headerRect.width - 120;
            
            // Initial toggle
            var initialRect = new Rect(headerRect.x + buttonX, headerRect.y + 2, 24, 20);
            Color initialColor = state.isInitialState ? new Color(0.3f, 1f, 0.4f) : new Color(0.5f, 0.5f, 0.6f);
            DrawModernButton(initialRect, "🏁", initialColor, () => {
                if (!state.isInitialState)
                {
                    foreach (var s in states) s.isInitialState = false;
                    state.isInitialState = true;
                }
            });
            
            // Remove button
            var removeRect = new Rect(headerRect.x + buttonX + 30, headerRect.y + 2, 24, 20);
            DrawModernButton(removeRect, "🗑️", new Color(1f, 0.4f, 0.4f), () => {
                states.RemoveAt(index);
                if (statePositions.ContainsKey(state.stateName))
                    statePositions.Remove(state.stateName);
            });
        }
        
        private void DrawStateContent(StateData state, int index, Rect contentRect)
        {
            // Transitions count and add button
            var transitionsHeaderRect = new Rect(contentRect.x, contentRect.y + 5, contentRect.width, 20);
            
            var transitionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 1f) }
            };
            
            string transitionText = $"🔗 Transitions ({state.transitions.Count})";
            EditorGUI.LabelField(new Rect(transitionsHeaderRect.x, transitionsHeaderRect.y, 150, 20), transitionText, transitionTitleStyle);
            
            // Add transition button
            var addTransitionRect = new Rect(transitionsHeaderRect.x + 150, transitionsHeaderRect.y, 80, 18);
            DrawModernButton(addTransitionRect, "➕ Add", new Color(0.2f, 0.7f, 0.8f), () => {
                state.transitions.Add(new TransitionData());
            });
            
            // Transitions list
            var transitionsRect = new Rect(contentRect.x, contentRect.y + 30, contentRect.width, contentRect.height - 35);
            
            if (state.transitions.Count == 0)
            {
                var noTransitionsStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.7f, 0.8f) },
                    fontStyle = FontStyle.Italic
                };
                EditorGUI.LabelField(new Rect(transitionsRect.x + 10, transitionsRect.y, transitionsRect.width, 20), 
                    "No transitions configured", noTransitionsStyle);
            }
            else
            {
                DrawTransitionsList(state, transitionsRect);
            }
        }
        
        private void DrawTransitionsList(StateData state, Rect transitionsRect)
        {
            float yOffset = 0;
            
            for (int i = 0; i < state.transitions.Count; i++)
            {
                var transition = state.transitions[i];
                var transitionRect = new Rect(transitionsRect.x + 5, transitionsRect.y + yOffset, transitionsRect.width - 10, 25);
                
                // Transition background
                Color transitionBg = transition.autoDetected ? 
                    new Color(0.3f, 0.2f, 0.4f, 0.5f) : 
                    new Color(0.25f, 0.3f, 0.4f, 0.5f);
                    
                DrawRoundedRect(transitionRect, transitionBg, 4f);
                
                // Target state
                string targetName = GetTargetStateName(transition.targetStateIndex);
                var targetStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.white },
                    fontSize = 10
                };
                
                EditorGUI.LabelField(new Rect(transitionRect.x + 5, transitionRect.y + 3, 100, 20), 
                    $"→ {targetName}", targetStyle);
                
                // Conditions count
                string conditionsText = transition.conditionScripts.Count > 0 ? 
                    $"❓ {transition.conditionScripts.Count}" : "⚡ Always";
                    
                EditorGUI.LabelField(new Rect(transitionRect.x + 110, transitionRect.y + 3, 60, 20), 
                    conditionsText, targetStyle);
                
                // Remove transition
                var removeTransitionRect = new Rect(transitionRect.x + transitionRect.width - 25, transitionRect.y + 3, 20, 18);
                DrawModernButton(removeTransitionRect, "×", new Color(1f, 0.4f, 0.4f), () => {
                    state.transitions.RemoveAt(i);
                });
                
                yOffset += 30;
            }
        }
        
        private void DrawStatusBadge(Rect rect, string text, Color color)
        {
            DrawRoundedRect(rect, color, 8f);
            
            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black }
            };
            
            EditorGUI.LabelField(rect, text, badgeStyle);
        }
        
        private void DrawModernButton(Rect rect, string text, Color color, System.Action onClick)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            
            var buttonStyle = new GUIStyle("button")
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            if (GUI.Button(rect, text, buttonStyle) && onClick != null)
            {
                onClick.Invoke();
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        private void DrawRoundedRect(Rect rect, Color color, float cornerRadius)
        {
            // Simple rounded rect simulation with multiple overlapping rects
            EditorGUI.DrawRect(new Rect(rect.x + cornerRadius, rect.y, rect.width - cornerRadius * 2, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + cornerRadius, rect.width, rect.height - cornerRadius * 2), color);
            
            // Corner approximation
            EditorGUI.DrawRect(new Rect(rect.x + cornerRadius, rect.y + cornerRadius, cornerRadius, cornerRadius), color);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - cornerRadius * 2, rect.y + cornerRadius, cornerRadius, cornerRadius), color);
            EditorGUI.DrawRect(new Rect(rect.x + cornerRadius, rect.y + rect.height - cornerRadius * 2, cornerRadius, cornerRadius), color);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - cornerRadius * 2, rect.y + rect.height - cornerRadius * 2, cornerRadius, cornerRadius), color);
        }
        
        private void DrawGradientRect(Rect rect, Color startColor, Color endColor)
        {
            int steps = 10;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps;
                Color currentColor = Color.Lerp(startColor, endColor, t);
                float y = rect.y + (rect.height / steps) * i;
                float height = rect.height / steps + 1;
                
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, height), currentColor);
            }
        }
        
        private void DrawTransitionsSection(StateData state, int stateIndex)
        {
            EditorGUILayout.Space(5);
            
            // Transitions Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔗 Transitions", EditorStyles.boldLabel, GUILayout.Width(100));
            
            // Add Transition Button
            var addTransitionColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.8f);
            if (GUILayout.Button("➕ Add", GUILayout.Width(50), GUILayout.Height(18)))
            {
                state.transitions.Add(new TransitionData());
            }
            GUI.backgroundColor = addTransitionColor;
            
            EditorGUILayout.EndHorizontal();
            
            // Draw each transition
            for (int i = 0; i < state.transitions.Count; i++)
            {
                DrawTransitionEditor(state.transitions[i], i, stateIndex);
            }
            
            if (state.transitions.Count == 0)
            {
                EditorGUILayout.LabelField("  No transitions defined", EditorStyles.miniLabel);
            }
        }
        
        private void DrawTransitionEditor(TransitionData transition, int transitionIndex, int stateIndex)
        {
            var transitionStyle = new GUIStyle("box")
            {
                padding = new RectOffset(5, 5, 3, 3),
                margin = new RectOffset(15, 5, 2, 2)
            };
            
            if (transition.autoDetected)
            {
                transitionStyle.normal.background = StateMachineUIDrawer.MakeTex(2, 2, new Color(0.6f, 0.4f, 0.8f, 0.15f));
            }
            
            EditorGUILayout.BeginVertical(transitionStyle);
            
            // Transition Header
            EditorGUILayout.BeginHorizontal();
            
            // Expand/Collapse
            string expandIcon = transition.expanded ? "▼" : "▶";
            if (GUILayout.Button(expandIcon, GUILayout.Width(18), GUILayout.Height(18)))
            {
                transition.expanded = !transition.expanded;
            }
            
            // Transition info
            string targetName = GetTargetStateName(transition.targetStateIndex);
            string autoIcon = transition.autoDetected ? "🔍" : "🔗";
            EditorGUILayout.LabelField($"{autoIcon} → {targetName}", EditorStyles.boldLabel, GUILayout.Width(120));
            
            // Target State Dropdown
            EditorGUI.BeginChangeCheck();
            int newTargetIndex = EditorGUILayout.Popup(transition.targetStateIndex - 1, GetStateNames(), GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck())
            {
                transition.targetStateIndex = newTargetIndex + 1;
            }
            
            // Remove Transition Button
            var removeTransitionColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️", GUILayout.Width(25), GUILayout.Height(18)))
            {
                var state = states[stateIndex];
                state.transitions.RemoveAt(transitionIndex);
                GUI.backgroundColor = removeTransitionColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = removeTransitionColor;
            
            EditorGUILayout.EndHorizontal();
            
            // Expanded transition content
            if (transition.expanded)
            {
                EditorGUILayout.Space(3);
                DrawConditionsSection(transition);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawConditionsSection(TransitionData transition)
        {
            // Conditions Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("❓ Conditions", EditorStyles.boldLabel, GUILayout.Width(80));
            
            // Add Condition Button
            var addConditionColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.6f, 0.2f);
            if (GUILayout.Button("➕", GUILayout.Width(25), GUILayout.Height(16)))
            {
                transition.conditionScripts.Add(null);
            }
            GUI.backgroundColor = addConditionColor;
            
            EditorGUILayout.EndHorizontal();
            
            // Draw each condition
            for (int i = 0; i < transition.conditionScripts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Condition icon
                string conditionIcon = GetConditionIcon(transition.conditionScripts[i]);
                EditorGUILayout.LabelField(conditionIcon, GUILayout.Width(20));
                
                // Condition Script Field
                EditorGUI.BeginChangeCheck();
                transition.conditionScripts[i] = (MonoScript)EditorGUILayout.ObjectField(
                    transition.conditionScripts[i], 
                    typeof(MonoScript), 
                    false,
                    GUILayout.Width(120)
                );
                
                // Remove Condition Button
                var removeConditionColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(16)))
                {
                    transition.conditionScripts.RemoveAt(i);
                    GUI.backgroundColor = removeConditionColor;
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                GUI.backgroundColor = removeConditionColor;
                
                EditorGUILayout.EndHorizontal();
            }
            
            if (transition.conditionScripts.Count == 0)
            {
                EditorGUILayout.LabelField("    No conditions (always true)", EditorStyles.miniLabel);
            }
        }
        
        private string GetTargetStateName(int targetIndex)
        {
            if (targetIndex <= 0 || targetIndex > states.Count)
                return "None";
            
            var targetState = states[targetIndex - 1];
            return string.IsNullOrEmpty(targetState.stateName) ? $"State {targetIndex}" : targetState.stateName;
        }
        
        private string[] GetStateNames()
        {
            var names = new string[states.Count];
            for (int i = 0; i < states.Count; i++)
            {
                names[i] = string.IsNullOrEmpty(states[i].stateName) ? $"State {i + 1}" : states[i].stateName;
            }
            return names;
        }
        
        private string GetConditionIcon(MonoScript conditionScript)
        {
            if (conditionScript == null) return "❓";
            
            string className = conditionScript.GetClass()?.Name ?? "";
            
            if (className.Contains("Arrived")) return "📍";
            if (className.Contains("ServiceCompleted")) return "✅";
            if (className.Contains("PatienceExpired")) return "⏰";
            if (className.Contains("QueuePositionChanged")) return "🔄";
            
            return "❓";
        }
        
        private void DrawStateDiagram()
        {
            StateMachineDiagramRenderer.DrawStateDiagram(
                states,
                statePositions,
                ref diagramScrollPosition,
                ref draggingState,
                ref dragOffset,
                boxStyle
            );
        }
        
        private void DrawGenerateButton()
        {
            bool canGenerate = StateMachineCodeGenerator.CanGenerate(states);
            
            StateMachineUIDrawer.DrawGenerateButton(
                canGenerate,
                buttonStyle,
                () => StateMachineCodeGenerator.GenerateStateMachine(stateMachineName, selectedOwner, states)
            );
            
            if (canGenerate)
            {
                EditorGUILayout.LabelField(StateMachineCodeGenerator.GetGenerationSummary(states), EditorStyles.miniLabel);
            }
        }
        
        private void PerformAutoDetection()
        {
            foundExistingStateMachine = StateMachineAutoDetector.PerformAutoDetection(
                selectedOwner,
                out detectedStateMachineType,
                out stateMachineName,
                states
            );
        }
        
        private void LoadDetectedStructure()
        {
            // Clear manual states, keep auto-detected ones
            states.RemoveAll(s => !s.autoDetected);
            
            // Auto-detect transitions if possible (only if no transitions exist)
            if (states.All(s => s.transitions.Count == 0))
            {
                StateMachineTransitionDetector.AutoDetectTransitions(states);
            }
            
            // Set first state as initial if none set
            if (states.Count > 0 && !states.Any(s => s.isInitialState))
            {
                var startingState = states.FirstOrDefault(s => 
                    s.stateName.Contains("Idle") || 
                    s.stateName.Contains("Moving") || 
                    s.stateName.Contains("Enter") || 
                    s.stateName.Contains("Start"));
                    
                if (startingState != null)
                {
                    startingState.isInitialState = true;
                }
                else
                {
                    states[0].isInitialState = true;
                }
            }
            
            InitializeDiagramPositions();
            Repaint();
        }
        
        private void InitializeDiagramPositions()
        {
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!statePositions.ContainsKey(state.stateName))
                {
                    statePositions[state.stateName] = new Vector2(
                        (i % 4) * 150 + 50,
                        (i / 4) * 100 + 50
                    );
                }
            }
        }
        
        private void OnOwnerChanged()
        {
            if (selectedOwner != null)
            {
                selectedPrefab = null;
                PerformAutoDetection();
            }
        }
        
        private void OnPrefabSelected()
        {
            if (selectedPrefab != null)
            {
                var components = selectedPrefab.GetComponents<MonoBehaviour>();
                if (components.Length > 0)
                {
                    selectedOwner = components[0];
                    PerformAutoDetection();
                }
                else
                {
                    EditorGUILayout.HelpBox("⚠️ Selected prefab has no MonoBehaviour components", MessageType.Warning);
                }
            }
        }
    }
}