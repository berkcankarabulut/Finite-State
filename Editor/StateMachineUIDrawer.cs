using UnityEngine;
using UnityEditor; 

namespace FiniteState.Editor
{
    public static class StateMachineUIDrawer
    {
        public static void DrawHeader(GUIStyle headerStyle)
        {
            EditorGUILayout.Space(10);
            
            var rect = EditorGUILayout.GetControlRect(false, 50);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            
            var titleRect = new Rect(rect.x, rect.y + 10, rect.width, 30);
            EditorGUI.LabelField(titleRect, "🎮 State Machine Creator", headerStyle);
            
            EditorGUILayout.Space(10);
        }
        
        public static void DrawOwnerSelection(
            ref MonoBehaviour selectedOwner, 
            ref GameObject selectedPrefab,
            GUIStyle boxStyle,
            System.Action onOwnerChanged,
            System.Action onPrefabSelected)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.LabelField("📋 Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Owner Selection
            var ownerRect = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(ownerRect.x, ownerRect.y, 150, ownerRect.height);
            var fieldRect = new Rect(ownerRect.x + 155, ownerRect.y, ownerRect.width - 155, ownerRect.height);
            
            EditorGUI.LabelField(labelRect, "🎯 Owner (MonoBehaviour):");
            
            EditorGUI.BeginChangeCheck();
            selectedOwner = (MonoBehaviour)EditorGUI.ObjectField(fieldRect, selectedOwner, typeof(MonoBehaviour), true);
            if (EditorGUI.EndChangeCheck())
            {
                onOwnerChanged?.Invoke();
            }
            
            // Prefab selection
            EditorGUILayout.Space(5);
            var prefabRect = EditorGUILayout.GetControlRect();
            var prefabLabelRect = new Rect(prefabRect.x, prefabRect.y, 150, prefabRect.height);
            var prefabFieldRect = new Rect(prefabRect.x + 155, prefabRect.y, prefabRect.width - 155, prefabRect.height);
            
            EditorGUI.LabelField(prefabLabelRect, "🎲 Or select Prefab:");
            
            EditorGUI.BeginChangeCheck();
            selectedPrefab = (GameObject)EditorGUI.ObjectField(prefabFieldRect, selectedPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && selectedPrefab != null)
            {
                onPrefabSelected?.Invoke();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        public static void DrawViewModeToggle(
            ref StateMachineEditor.ViewMode currentViewMode,
            GUIStyle boxStyle,
            System.Action onDiagramModeSelected)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("👁️ View Mode", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            var listColor = currentViewMode == StateMachineEditor.ViewMode.List ? 
                new Color(0.4f, 0.8f, 0.4f) : Color.white;
            var diagramColor = currentViewMode == StateMachineEditor.ViewMode.Diagram ? 
                new Color(0.4f, 0.8f, 0.4f) : Color.white;
            
            GUI.backgroundColor = listColor;
            if (GUILayout.Button("📝 List", GUILayout.Width(60)))
            {
                currentViewMode = StateMachineEditor.ViewMode.List;
            }
            
            GUI.backgroundColor = diagramColor;
            if (GUILayout.Button("🔗 Diagram", GUILayout.Width(80)))
            {
                currentViewMode = StateMachineEditor.ViewMode.Diagram;
                onDiagramModeSelected?.Invoke();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        public static void DrawStateMachineNameField(ref string stateMachineName, GUIStyle boxStyle)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            var nameRect = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(nameRect.x, nameRect.y, 150, nameRect.height);
            var fieldRect = new Rect(nameRect.x + 155, nameRect.y, nameRect.width - 155, nameRect.height);
            
            EditorGUI.LabelField(labelRect, "📝 State Machine Name:");
            stateMachineName = EditorGUI.TextField(fieldRect, stateMachineName);
            
            EditorGUILayout.EndVertical();
        }
        
        public static void DrawGenerateButton(
            bool canGenerate,
            GUIStyle buttonStyle,
            System.Action onGenerate)
        {
            EditorGUILayout.Space(10);
            
            if (!canGenerate)
            {
                EditorGUILayout.HelpBox("⚠️ Add at least one valid state script to generate the State Machine", MessageType.Warning);
            }
            
            var generateRect = EditorGUILayout.GetControlRect(false, 40);
            var buttonRect = new Rect(generateRect.x + 50, generateRect.y, generateRect.width - 100, generateRect.height);
            
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = canGenerate ? new Color(0.2f, 0.8f, 0.2f) : Color.gray;
            
            GUI.enabled = canGenerate;
            if (GUI.Button(buttonRect, "🚀 Generate State Machine", buttonStyle))
            {
                onGenerate?.Invoke();
            }
            GUI.enabled = true;
            GUI.backgroundColor = originalColor;
        }
        
        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}