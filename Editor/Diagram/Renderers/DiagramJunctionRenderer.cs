using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace FiniteState.Editor
{
    public static class DiagramJunctionRenderer
    {
        public static void DrawJunctionConnections(List<StateData> states, Dictionary<string, Vector2> statePositions)
        {
            foreach (var state in states)
            {
                if (!IsValidState(state, statePositions)) continue;
                
                var transitionGroups = GroupTransitions(state, states);
                
                if (transitionGroups.Count > 1)
                {
                    DrawJunctionFlow(state, transitionGroups, statePositions);
                }
                else if (transitionGroups.Count == 1)
                {
                    DrawDirectConnection(state, transitionGroups[0], statePositions);
                }
            }
        }
        
        private static List<TransitionGroup> GroupTransitions(StateData state, List<StateData> states)
        {
            return state.transitions
                .Where(t => t.targetStateIndex > 0 && t.targetStateIndex <= states.Count)
                .Select(t => new TransitionGroup
                {
                    sourceState = state,
                    targetState = states[t.targetStateIndex - 1],
                    transition = t,
                    conditions = t.conditionScripts.Where(c => c != null).ToList()
                })
                .ToList();
        }
        
        private static void DrawJunctionFlow(StateData sourceState, List<TransitionGroup> transitions, Dictionary<string, Vector2> statePositions)
        {
            Vector2 sourcePos = statePositions[sourceState.stateName] + new Vector2(60, 35);
            Vector2 junctionPos = CalculateJunctionPosition(sourceState, transitions, statePositions);
            
            // Line to junction
            Handles.BeginGUI();
            Handles.color = new Color(0.7f, 0.7f, 0.9f, 1f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Vector2.Perpendicular((junctionPos - sourcePos).normalized) * (i - 1);
                Handles.DrawLine(sourcePos + offset, junctionPos + offset);
            }
            Handles.EndGUI();
            
            // Junction node
            DrawJunctionNode(junctionPos, transitions);
            
            // Lines from junction to targets
            foreach (var transitionGroup in transitions)
            {
                if (statePositions.ContainsKey(transitionGroup.targetState.stateName))
                {
                    Vector2 targetPos = statePositions[transitionGroup.targetState.stateName] + new Vector2(60, 35);
                    DrawJunctionToTarget(junctionPos, targetPos, transitionGroup);
                }
            }
        }
        
        private static void DrawDirectConnection(StateData sourceState, TransitionGroup transitionGroup, Dictionary<string, Vector2> statePositions)
        {
            if (!statePositions.ContainsKey(transitionGroup.targetState.stateName)) return;
            
            Vector2 sourcePos = statePositions[sourceState.stateName] + new Vector2(60, 35);
            Vector2 targetPos = statePositions[transitionGroup.targetState.stateName] + new Vector2(60, 35);
            
            Vector2 direction = (targetPos - sourcePos).normalized;
            Vector2 adjustedFrom = sourcePos + direction * 40;
            Vector2 adjustedTo = targetPos - direction * 40;
            
            Handles.BeginGUI();
            
            // Draw line with condition
            if (transitionGroup.conditions.Count > 0)
            {
                Vector2 midPoint = (adjustedFrom + adjustedTo) / 2f;
                Vector2 conditionPos = midPoint - new Vector2(45, 30);
                
                Handles.color = Color.cyan;
                Handles.DrawLine(adjustedFrom, midPoint);
                Handles.DrawLine(midPoint, adjustedTo);
                
                DrawArrowHead(adjustedTo, direction);
                Handles.EndGUI();
                
                // Draw condition node
                string conditionName = transitionGroup.conditions[0].GetClass()?.Name ?? "Unknown";
                DiagramNodeRenderer.DrawConditionNode(conditionPos, conditionName);
            }
            else
            {
                // No condition - simple line
                Handles.color = Color.yellow;
                Handles.DrawLine(adjustedFrom, adjustedTo);
                DrawArrowHead(adjustedTo, direction);
                Handles.EndGUI();
            }
        }
        
        private static void DrawJunctionNode(Vector2 junctionPos, List<TransitionGroup> transitions)
        {
            float size = 40f;
            Rect junctionRect = new Rect(junctionPos.x - size/2, junctionPos.y - size/2, size, size);
            
            // Hexagon shape
            Vector2 center = junctionPos;
            float radius = size / 2;
            
            Vector3[] hexPoints = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                hexPoints[i] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    0
                );
            }
            
            Handles.BeginGUI();
            
            // Fill
            Handles.color = new Color(0.3f, 0.3f, 0.5f, 0.9f);
            Handles.DrawAAConvexPolygon(hexPoints);
            
            // Border
            Handles.color = new Color(0.6f, 0.6f, 0.8f, 1f);
            for (int i = 0; i < 6; i++)
            {
                Handles.DrawLine(hexPoints[i], hexPoints[(i + 1) % 6]);
            }
            
            Handles.EndGUI();
            
            // Icon and count
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            GUI.Label(new Rect(junctionRect.x, junctionRect.y - 5, junctionRect.width, 20), "⚡", iconStyle);
            
            var countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = Color.cyan }
            };
            
            GUI.Label(new Rect(junctionRect.x, junctionRect.y + 15, junctionRect.width, 15), transitions.Count.ToString(), countStyle);
            
            // Tooltip on hover
            if (junctionRect.Contains(Event.current.mousePosition))
            {
                string tooltip = $"⚡ Junction\n{transitions.Count} branches";
                foreach (var t in transitions)
                {
                    string condition = t.conditions.Count > 0 ? 
                        t.conditions[0].GetClass()?.Name.Replace("Condition", "") : "ELSE";
                    tooltip += $"\n• {condition} → {t.targetState.stateName.Replace("State", "")}";
                }
                
                GUI.Box(new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y, 200, 80), tooltip, EditorStyles.helpBox);
            }
        }
        
        private static void DrawJunctionToTarget(Vector2 junctionPos, Vector2 targetPos, TransitionGroup transitionGroup)
        {
            Vector2 direction = (targetPos - junctionPos).normalized;
            Vector2 adjustedTarget = targetPos - direction * 40;
            
            Handles.BeginGUI();
            
            // Color based on condition
            Color lineColor = transitionGroup.conditions.Count > 0 ? GetConditionLineColor(transitionGroup.conditions[0]) : Color.gray;
            Handles.color = lineColor;
            
            Handles.DrawLine(junctionPos, adjustedTarget);
            DrawArrowHead(adjustedTarget, direction);
            
            Handles.EndGUI();
            
            // Condition label
            if (transitionGroup.conditions.Count > 0)
            {
                Vector2 labelPos = Vector2.Lerp(junctionPos, adjustedTarget, 0.6f);
                DrawConditionLabel(labelPos, transitionGroup.conditions[0]);
            }
            else
            {
                // ELSE label
                Vector2 labelPos = Vector2.Lerp(junctionPos, adjustedTarget, 0.6f);
                Rect labelRect = new Rect(labelPos.x - 30, labelPos.y - 8, 60, 16);
                EditorGUI.DrawRect(labelRect, new Color(0.6f, 0.6f, 0.6f, 0.9f));
                EditorGUI.DrawRect(new Rect(labelRect.x-1, labelRect.y-1, labelRect.width+2, labelRect.height+2), Color.black);
                
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold,
                    fontSize = 8
                };
                
                GUI.Label(labelRect, "ELSE", labelStyle);
            }
        }
        
        private static void DrawConditionLabel(Vector2 position, MonoScript condition)
        {
            string conditionName = condition.GetClass()?.Name ?? "Unknown";
            string shortName = conditionName.Replace("Condition", "");
            
            Vector2 labelSize = new Vector2(60, 16);
            Rect labelRect = new Rect(position.x - labelSize.x/2, position.y - labelSize.y/2, labelSize.x, labelSize.y);
            
            Color bgColor = GetConditionLineColor(condition);
            EditorGUI.DrawRect(labelRect, bgColor);
            EditorGUI.DrawRect(new Rect(labelRect.x-1, labelRect.y-1, labelRect.width+2, labelRect.height+2), Color.black);
            
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                fontSize = 8
            };
            
            GUI.Label(labelRect, shortName, labelStyle);
        }
        
        private static Color GetConditionLineColor(MonoScript condition)
        {
            string conditionName = condition.GetClass()?.Name ?? "";
            return DiagramNodeRenderer.GetConditionColor(conditionName);
        }
        
        private static Vector2 CalculateJunctionPosition(StateData sourceState, List<TransitionGroup> transitions, Dictionary<string, Vector2> statePositions)
        {
            Vector2 sourcePos = statePositions[sourceState.stateName] + new Vector2(60, 35);
            
            Vector2 averageTargetPos = Vector2.zero;
            int validTargets = 0;
            
            foreach (var transition in transitions)
            {
                if (statePositions.ContainsKey(transition.targetState.stateName))
                {
                    averageTargetPos += statePositions[transition.targetState.stateName] + new Vector2(60, 35);
                    validTargets++;
                }
            }
            
            if (validTargets > 0)
            {
                averageTargetPos /= validTargets;
                return Vector2.Lerp(sourcePos, averageTargetPos, 0.4f);
            }
            
            return sourcePos + Vector2.right * 150;
        }
        
        private static void DrawArrowHead(Vector2 tip, Vector2 direction)
        {
            Vector2 arrowHead1 = tip - direction * 12 + Vector2.Perpendicular(direction) * 8;
            Vector2 arrowHead2 = tip - direction * 12 - Vector2.Perpendicular(direction) * 8;
            
            Vector3[] arrowPoints = new Vector3[]
            {
                new Vector3(tip.x, tip.y, 0),
                new Vector3(arrowHead1.x, arrowHead1.y, 0),
                new Vector3(arrowHead2.x, arrowHead2.y, 0)
            };
            
            Handles.DrawAAConvexPolygon(arrowPoints);
        }
        
        private static bool IsValidState(StateData state, Dictionary<string, Vector2> statePositions)
        {
            return state != null && 
                   !string.IsNullOrEmpty(state.stateName) && 
                   statePositions.ContainsKey(state.stateName) && 
                   state.script != null;
        }
    }
     
}