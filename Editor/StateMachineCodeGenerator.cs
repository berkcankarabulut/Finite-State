using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiniteState.Editor
{
    public static class StateMachineCodeGenerator
    {
        public static void GenerateStateMachine(
            string stateMachineName,
            MonoBehaviour selectedOwner,
            List<StateData> states)
        {
            string path = EditorUtility.SaveFilePanel("Save State Machine", "Assets", stateMachineName, "cs");
            if (string.IsNullOrEmpty(path)) return;
            
            path = "Assets" + path.Substring(Application.dataPath.Length);
            
            string code = GenerateStateMachineCode(stateMachineName, selectedOwner, states);
            System.IO.File.WriteAllText(path, code);
            
            AssetDatabase.Refresh();
            
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            Debug.Log($"✅ State Machine '{stateMachineName}' generated successfully at: {path}");
        }
        
        private static string GenerateStateMachineCode(
            string stateMachineName,
            MonoBehaviour selectedOwner,
            List<StateData> states)
        {
            var code = new StringBuilder();
            
            // Header
            GenerateHeader(code);
            
            // Class declaration
            GenerateClassDeclaration(code, stateMachineName, selectedOwner);
            
            // Constructor
            GenerateConstructor(code, stateMachineName, selectedOwner);
            
            // Initialize method
            GenerateInitializeMethod(code, states);
            
            // Close class
            code.AppendLine("}");
            
            return code.ToString();
        }
        
        private static void GenerateHeader(StringBuilder code)
        {
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using FiniteState.Runtime;");
            code.AppendLine();
        }
        
        private static void GenerateClassDeclaration(StringBuilder code, string stateMachineName, MonoBehaviour selectedOwner)
        {
            code.AppendLine($"public class {stateMachineName} : StateMachine<{selectedOwner.GetType().Name}>");
            code.AppendLine("{");
        }
        
        private static void GenerateConstructor(StringBuilder code, string stateMachineName, MonoBehaviour selectedOwner)
        {
            code.AppendLine($"    public {stateMachineName}({selectedOwner.GetType().Name} owner) : base(owner)");
            code.AppendLine("    {");
            code.AppendLine("        InitializeStates();");
            code.AppendLine("    }");
            code.AppendLine();
        }
        
        private static void GenerateInitializeMethod(StringBuilder code, List<StateData> states)
        {
            code.AppendLine("    private void InitializeStates()");
            code.AppendLine("    {");
            
            // Generate state creation code
            GenerateStateCreation(code, states);
            
            // Generate transition code
            GenerateTransitions(code, states);
            
            // Set initial state
            GenerateInitialState(code, states);
            
            code.AppendLine("    }");
        }
        
        private static void GenerateStateCreation(StringBuilder code, List<StateData> states)
        {
            var validStates = GetValidStates(states);
            
            if (validStates.Count == 0)
            {
                code.AppendLine("        // No valid states found");
                return;
            }
            
            code.AppendLine("        // Create states");
            foreach (var state in validStates)
            {
                string variableName = GetVariableName(state.stateName);
                code.AppendLine($"        var {variableName} = new {state.stateName}(owner, this);");
            }
            code.AppendLine();
        }
        
        private static void GenerateTransitions(StringBuilder code, List<StateData> states)
        {
            var validStates = GetValidStates(states);
            
            if (validStates.Count == 0) return;
            
            bool hasTransitions = validStates.Any(s => s.transitions.Count > 0);
            if (!hasTransitions)
            {
                code.AppendLine("        // No transitions defined");
                return;
            }
            
            code.AppendLine("        // Add transitions");
            
            for (int i = 0; i < validStates.Count; i++)
            {
                var state = validStates[i];
                if (state.transitions.Count == 0) continue;
                
                string variableName = GetVariableName(state.stateName);
                
                foreach (var transition in state.transitions)
                {
                    if (IsValidTransition(transition, validStates))
                    {
                        GenerateTransitionCode(code, transition, state, validStates, i);
                    }
                }
            }
            
            code.AppendLine();
        }
        
        private static void GenerateTransitionCode(
            StringBuilder code, 
            TransitionData transition, 
            StateData fromState, 
            List<StateData> validStates, 
            int fromIndex)
        {
            var targetState = validStates[transition.targetStateIndex - 1];
            string fromVariableName = GetVariableName(fromState.stateName);
            string targetVariableName = GetVariableName(targetState.stateName);
            string transitionVariableName = $"transition_{fromIndex}_{transition.targetStateIndex}";
            
            // Create transition
            code.AppendLine($"        var {transitionVariableName} = new Transition({targetVariableName});");
            
            // Add conditions
            var validConditions = GetValidConditions(transition);
            foreach (var conditionScript in validConditions)
            {
                string conditionName = conditionScript.GetClass().Name;
                bool needsOwnerParameter = DoesConditionNeedOwner(conditionScript);
                
                if (needsOwnerParameter)
                {
                    code.AppendLine($"        {transitionVariableName}.AddCondition(new {conditionName}(owner));");
                }
                else
                {
                    code.AppendLine($"        {transitionVariableName}.AddCondition(new {conditionName}());");
                }
            }
            
            // Add transition to state
            code.AppendLine($"        {fromVariableName}.AddTransition({transitionVariableName});");
            
            // Add comment for clarity
            string conditionInfo = validConditions.Count > 0 ? 
                $" // Conditions: {string.Join(", ", validConditions.Select(c => c.GetClass().Name))}" : 
                " // No conditions (always true)";
            
            code.AppendLine($"        // {fromState.stateName} → {targetState.stateName}{conditionInfo}");
            code.AppendLine();
        }
        
        private static void GenerateInitialState(StringBuilder code, List<StateData> states)
        {
            var validStates = GetValidStates(states);
            var initialState = validStates.FirstOrDefault(s => s.isInitialState);
            
            if (initialState != null)
            {
                code.AppendLine("        // Set initial state");
                string variableName = GetVariableName(initialState.stateName);
                code.AppendLine($"        ChangeState({variableName});");
            }
            else if (validStates.Count > 0)
            {
                code.AppendLine("        // No initial state set, using first state");
                string firstVariableName = GetVariableName(validStates[0].stateName);
                code.AppendLine($"        ChangeState({firstVariableName});");
            }
        }
        
        private static List<StateData> GetValidStates(List<StateData> states)
        {
            return states.Where(s => s.script != null && StateMachineAutoDetector.IsValidStateScript(s.script)).ToList();
        }
        
        private static List<MonoScript> GetValidConditions(TransitionData transition)
        {
            return transition.conditionScripts
                .Where(c => c != null && StateMachineAutoDetector.IsValidConditionScript(c))
                .ToList();
        }
        
        private static bool IsValidTransition(TransitionData transition, List<StateData> validStates)
        {
            return transition.targetStateIndex > 0 && transition.targetStateIndex <= validStates.Count;
        }
        
        private static string GetVariableName(string stateName)
        {
            // Convert PascalCase to camelCase
            if (string.IsNullOrEmpty(stateName)) return "unknown";
            
            return char.ToLower(stateName[0]) + stateName.Substring(1);
        }
        
        private static bool DoesConditionNeedOwner(MonoScript conditionScript)
        {
            try
            {
                var conditionType = conditionScript.GetClass();
                if (conditionType == null) return false;
                
                // Check if condition has a constructor that takes owner parameter
                var constructors = conditionType.GetConstructors();
                return constructors.Any(c => 
                {
                    var parameters = c.GetParameters();
                    return parameters.Length == 1 && 
                           (typeof(MonoBehaviour).IsAssignableFrom(parameters[0].ParameterType) ||
                            parameters[0].ParameterType.Name.Contains("Customer"));
                });
            }
            catch
            {
                return false;
            }
        }
        
        public static bool CanGenerate(List<StateData> states)
        {
            return states.Count > 0 && 
                   states.Any(s => s.script != null && StateMachineAutoDetector.IsValidStateScript(s.script));
        }
        
        public static string GetGenerationSummary(List<StateData> states)
        {
            var validStates = GetValidStates(states);
            var totalTransitions = validStates.Sum(s => s.transitions.Count);
            var totalConditions = validStates.Sum(s => s.transitions.Sum(t => GetValidConditions(t).Count));
            
            return $"Will generate: {validStates.Count} states, {totalTransitions} transitions, {totalConditions} conditions";
        }
    }
}