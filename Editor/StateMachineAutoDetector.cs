using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using FiniteState.Runtime;

namespace FiniteState.Editor
{
    public static class StateMachineAutoDetector
    {
        public static bool PerformAutoDetection(
            MonoBehaviour selectedOwner,
            out Type detectedStateMachineType,
            out string stateMachineName,
            List<StateData> states)
        {
            detectedStateMachineType = null;
            stateMachineName = "MyStateMachine";
            bool foundExistingStateMachine = false;
            
            if (selectedOwner == null) return false;
            
            // Look for StateMachine field
            var ownerType = selectedOwner.GetType();
            var fields = ownerType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                if (IsStateMachineType(field.FieldType))
                {
                    foundExistingStateMachine = true;
                    detectedStateMachineType = field.FieldType;
                    stateMachineName = field.FieldType.Name;
                    break;
                }
            }
            
            // Auto-detect common state classes
            if (foundExistingStateMachine)
            {
                AutoDetectStateClasses(selectedOwner, states);
                
                // Auto-detect transitions first
                StateMachineTransitionDetector.AutoDetectTransitions(states);
                
                // Then auto-assign conditions to those transitions
                AutoDetectTransitionsWithConditions(states);
            }
            
            return foundExistingStateMachine;
        }
        
        private static void AutoDetectStateClasses(MonoBehaviour selectedOwner, List<StateData> states)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var ownerType = selectedOwner.GetType();
            var foundStateNames = new HashSet<string>();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (IsStateClassForOwner(type, ownerType))
                        {
                            if (!foundStateNames.Contains(type.Name))
                            {
                                AddAutoDetectedState(type, states);
                                foundStateNames.Add(type.Name);
                            }
                        }
                    }
                }
                catch (System.Exception) { }
            }
        }
        
        private static void AutoDetectTransitionsWithConditions(List<StateData> states)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var conditionScripts = new List<MonoScript>();
            
            Debug.Log("=== Auto-Detecting Conditions ===");
            
            // Find all condition scripts
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (IsConditionType(type))
                        {
                            Debug.Log($"Found condition type: {type.Name}");
                            
                            // Find the script for this condition
                            string[] guids = AssetDatabase.FindAssets($"{type.Name} t:Script");
                            foreach (string guid in guids)
                            {
                                string path = AssetDatabase.GUIDToAssetPath(guid);
                                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                                if (script != null && script.GetClass() == type)
                                {
                                    conditionScripts.Add(script);
                                    Debug.Log($"Added condition script: {script.name}");
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex) 
                { 
                    Debug.LogWarning($"Error scanning assembly: {ex.Message}");
                }
            }
            
            Debug.Log($"Total condition scripts found: {conditionScripts.Count}");
            
            // Auto-assign conditions to transitions based on state names
            foreach (var state in states)
            {
                AutoAssignConditionsToTransitions(state, conditionScripts);
            }
        }
        
        private static bool IsConditionType(Type type)
        {
            if (type.IsAbstract || type.IsInterface) return false;
            
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "Condition")
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            
            return false;
        }
        
        private static void AutoAssignConditionsToTransitions(StateData state, List<MonoScript> conditionScripts)
        {
            Debug.Log($"Auto-assigning conditions for state: {state.stateName} (has {state.transitions.Count} transitions)");
            
            foreach (var transition in state.transitions)
            {
                if (transition.conditionScripts.Count > 0) 
                {
                    Debug.Log($"Transition already has {transition.conditionScripts.Count} conditions, skipping");
                    continue; // Already has conditions
                }
                
                // Determine appropriate condition based on state names
                var appropriateConditions = GetAppropriateConditions(state.stateName, transition, conditionScripts);
                
                Debug.Log($"Found {appropriateConditions.Count} appropriate conditions for {state.stateName}");
                
                foreach (var condition in appropriateConditions)
                {
                    transition.conditionScripts.Add(condition);
                    Debug.Log($"Added condition: {condition.GetClass()?.Name} to transition from {state.stateName}");
                }
                
                if (appropriateConditions.Count > 0)
                {
                    transition.autoDetected = true;
                }
            }
        }
        
        private static List<MonoScript> GetAppropriateConditions(string stateName, TransitionData transition, List<MonoScript> allConditions)
        {
            var result = new List<MonoScript>();
            
            if (string.IsNullOrEmpty(stateName)) return result;
            
            string lowerStateName = stateName.ToLower();
            
            // Moving states → ArrivedCondition
            if (lowerStateName.Contains("moving"))
            {
                var arrivedCondition = allConditions.FirstOrDefault(c => 
                    c.GetClass()?.Name == "ArrivedCondition");
                if (arrivedCondition != null)
                    result.Add(arrivedCondition);
            }
            
            // BeingServed state → ServiceCompletedCondition
            else if (lowerStateName.Contains("served") || lowerStateName.Contains("service"))
            {
                var serviceCondition = allConditions.FirstOrDefault(c => 
                    c.GetClass()?.Name == "ServiceCompletedCondition");
                if (serviceCondition != null)
                    result.Add(serviceCondition);
            }
            
            // Waiting state → QueuePositionChangedCondition or PatienceExpiredCondition
            else if (lowerStateName.Contains("waiting"))
            {
                var queueCondition = allConditions.FirstOrDefault(c => 
                    c.GetClass()?.Name == "QueuePositionChangedCondition");
                if (queueCondition != null)
                    result.Add(queueCondition);
                    
                var patienceCondition = allConditions.FirstOrDefault(c => 
                    c.GetClass()?.Name == "PatienceExpiredCondition");
                if (patienceCondition != null)
                    result.Add(patienceCondition);
            }
            
            // Leaving state → ArrivedCondition (to exit)
            else if (lowerStateName.Contains("leaving"))
            {
                var arrivedCondition = allConditions.FirstOrDefault(c => 
                    c.GetClass()?.Name == "ArrivedCondition");
                if (arrivedCondition != null)
                    result.Add(arrivedCondition);
            }
            
            return result;
        }
        
        private static bool IsStateClassForOwner(Type type, Type ownerType)
        {
            if (type.IsAbstract || type.IsInterface) return false;
            
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name.Contains("StateBase"))
                {
                    var genericArgs = baseType.GetGenericArguments();
                    if (genericArgs.Length > 0 && genericArgs[0] == ownerType)
                    {
                        return true;
                    }
                }
                baseType = baseType.BaseType;
            }
            
            return false;
        }
        
        private static void AddAutoDetectedState(Type stateType, List<StateData> states)
        {
            if (states.Any(s => s.stateName == stateType.Name)) return;
            
            var stateData = new StateData
            {
                stateName = stateType.Name,
                autoDetected = true
            };
            
            // Try to find the script
            string[] guids = AssetDatabase.FindAssets($"{stateType.Name} t:Script");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == stateType)
                {
                    stateData.script = script;
                    break;
                }
            }
            
            states.Add(stateData);
        }
        
        private static bool IsStateMachineType(Type type)
        {
            if (type == null) return false;
            
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name.Contains("StateMachine"))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            
            return false;
        }
        
        public static bool IsValidStateScript(MonoScript script)
        {
            if (script == null) return false;
            
            var type = script.GetClass();
            if (type == null) return false;
            
            return typeof(IState).IsAssignableFrom(type);
        }
        
        public static bool IsValidConditionScript(MonoScript script)
        {
            if (script == null) return false;
            
            var type = script.GetClass();
            if (type == null) return false;
            
            return typeof(Condition).IsAssignableFrom(type) && !type.IsAbstract;
        }
    }
}