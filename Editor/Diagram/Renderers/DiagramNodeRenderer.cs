using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FiniteState.Editor
{
    public static class DiagramNodeRenderer
    {
        public static void DrawStateNode(Vector2 nodePos, StateData state)
        {
            Rect nodeRect = new Rect(nodePos.x, nodePos.y, 120, 70);
            
            // Background color
            Color bgColor = GetNodeColor(state);
            EditorGUI.DrawRect(nodeRect, bgColor);
            
            // Border
            DrawNodeBorder(nodeRect, state);
            
            // Icon and text
            DrawNodeContent(nodeRect, state);
        }
        
        public static void DrawConditionNode(Vector2 nodePos, string conditionName)
        {
            // Simple diamond shape for conditions
            Vector2 center = nodePos + new Vector2(45, 30);
            
            Vector3[] points = new Vector3[]
            {
                new Vector3(center.x, center.y - 25, 0),
                new Vector3(center.x + 35, center.y, 0),
                new Vector3(center.x, center.y + 25, 0),
                new Vector3(center.x - 35, center.y, 0)
            };
            
            Handles.BeginGUI();
            
            // Fill
            Color conditionColor = GetConditionColor(conditionName);
            Handles.color = conditionColor;
            Handles.DrawAAConvexPolygon(points);
            
            // Border
            Handles.color = Color.black;
            for (int i = 0; i < 4; i++)
            {
                Handles.DrawLine(points[i], points[(i + 1) % 4]);
            }
            
            Handles.EndGUI();
            
            // Label
            string shortName = conditionName.Replace("Condition", "");
            if (shortName.Length > 8) shortName = shortName.Substring(0, 6) + "..";
            
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                fontSize = 8
            };
            
            GUI.Label(new Rect(nodePos.x + 5, nodePos.y + 20, 80, 20), shortName, labelStyle);
        }
        
        public static NodeStyle GetNodeStyle(StateData state)
        {
            // Simple node style info
            return new NodeStyle 
            { 
                size = new Vector2(120, 70),
                shape = state.isInitialState ? NodeShape.Circle : NodeShape.Rectangle
            };
        }
        
        private static Color GetNodeColor(StateData state)
        {
            if (state.isInitialState)
                return new Color(0.2f, 0.8f, 0.2f, 0.9f);
            else if (state.stateName != null && state.stateName.Contains("Leaving"))
                return new Color(0.6f, 0.6f, 0.6f, 0.9f);
            else if (state.autoDetected)
                return new Color(0.6f, 0.4f, 0.8f, 0.9f);
            else
                return new Color(0.4f, 0.6f, 0.8f, 0.9f);
        }
        
        public static Color GetConditionColor(string conditionName)
        {
            if (conditionName.Contains("Arrived"))
                return new Color(0.2f, 0.8f, 0.6f, 0.9f);
            else if (conditionName.Contains("ServiceCompleted"))
                return new Color(0.8f, 0.6f, 0.2f, 0.9f);
            else if (conditionName.Contains("PatienceExpired"))
                return new Color(0.8f, 0.2f, 0.2f, 0.9f);
            else if (conditionName.Contains("QueuePositionChanged"))
                return new Color(0.6f, 0.2f, 0.8f, 0.9f);
            else
                return new Color(0.5f, 0.5f, 0.5f, 0.9f);
        }
        
        private static void DrawNodeBorder(Rect nodeRect, StateData state)
        {
            Color borderColor = state.isInitialState ? Color.white : Color.black;
            EditorGUI.DrawRect(new Rect(nodeRect.x - 2, nodeRect.y - 2, nodeRect.width + 4, nodeRect.height + 4), borderColor);
        }
        
        private static void DrawNodeContent(Rect nodeRect, StateData state)
        {
            // Icon
            string icon = GetStateIcon(state);
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(nodeRect.x + 5, nodeRect.y + 5, 30, 30), icon, iconStyle);
            
            // Name
            string displayName = state.stateName?.Replace("State", "") ?? "Unknown";
            if (displayName.Length > 10) displayName = displayName.Substring(0, 8) + "..";
            
            var nameStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(nodeRect.x, nodeRect.y + 25, nodeRect.width, 30), displayName, nameStyle);
            
            // Transition count badge
            if (state.transitions.Count > 0)
            {
                var badgeRect = new Rect(nodeRect.x + nodeRect.width - 20, nodeRect.y + 5, 18, 18);
                EditorGUI.DrawRect(badgeRect, Color.yellow);
                
                var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.black },
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(badgeRect, state.transitions.Count.ToString(), badgeStyle);
            }
        }
        
        private static string GetStateIcon(StateData state)
        {
            if (state.isInitialState) return "🏁";
            if (state.stateName?.Contains("Leaving") ?? false) return "🚪";
            if (state.stateName?.Contains("Moving") ?? false) return "🚶";
            if (state.stateName?.Contains("Waiting") ?? false) return "⏳";
            if (state.stateName?.Contains("Service") ?? false) return "🛠️";
            if (state.autoDetected) return "🔍";
            return "⚙️";
        }
    } 
 
}