using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace FiniteState.Editor
{
    public static class StateMachineTransitionDetector
    {
        public static void AutoDetectTransitions(List<StateData> states)
        {
            AnalyzeExistingTransitions(states);
            
            // Add basic transitions based on common patterns
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.transitions.Count > 0) continue;
                
                AddCommonTransitions(state, states);
            }
            
            ConnectOrphanedStates(states);
            CreateReverseConnections(states);
        }
        
        private static void AnalyzeExistingTransitions(List<StateData> states)
        {
            Debug.Log("=== Analyzing Existing Transitions ===");
            foreach (var state in states)
            {
                if (state.transitions.Count > 0)
                {
                    foreach (var transition in state.transitions)
                    {
                        if (transition.targetStateIndex > 0 && transition.targetStateIndex <= states.Count)
                        {
                            var targetState = states[transition.targetStateIndex - 1];
                            var conditionNames = transition.conditionScripts
                                .Where(c => c != null)
                                .Select(c => c.GetClass()?.Name ?? "Unknown")
                                .ToList();
                            
                            string conditionInfo = conditionNames.Count > 0 
                                ? $"[{string.Join(", ", conditionNames)}]" 
                                : "[No Conditions]";
                            
                            Debug.Log($"Existing: {state.stateName} → {targetState.stateName} {conditionInfo}");
                        }
                    }
                }
            }
        }
        
        private static void AddCommonTransitions(StateData state, List<StateData> states)
        {
            if (state.stateName.Contains("MovingToQueue"))
            {
                var waitingState = states.FirstOrDefault(s => s.stateName.Contains("WaitingInQueue"));
                if (waitingState != null && !HasTransitionTo(state, waitingState, states))
                {
                    AddAutoTransition(state, waitingState, "ArrivedCondition", states);
                }
            }
            else if (state.stateName.Contains("MovingToService"))
            {
                var beingServedState = states.FirstOrDefault(s => s.stateName.Contains("BeingServed"));
                if (beingServedState != null && !HasTransitionTo(state, beingServedState, states))
                {
                    AddAutoTransition(state, beingServedState, "ArrivedCondition", states);
                }
            }
            else if (state.stateName.Contains("BeingServed"))
            {
                var leavingState = states.FirstOrDefault(s => s.stateName.Contains("Leaving"));
                if (leavingState != null && !HasTransitionTo(state, leavingState, states))
                {
                    AddAutoTransition(state, leavingState, "ServiceCompletedCondition", states);
                }
            }
            else if (state.stateName.Contains("WaitingInQueue"))
            {
                // WaitingInQueue should have TWO transitions
                
                // 1. Normal flow: WaitingInQueue → MovingToService
                var movingToServiceState = states.FirstOrDefault(s => s.stateName.Contains("MovingToService"));
                if (movingToServiceState != null && !HasTransitionTo(state, movingToServiceState, states))
                {
                    AddAutoTransition(state, movingToServiceState, "QueuePositionChangedCondition", states);
                }
                
                // 2. Patience expired: WaitingInQueue → LeavingState (CRITICAL: Must go to Leaving!)
                var leavingState = states.FirstOrDefault(s => s.stateName.Contains("Leaving"));
                if (leavingState != null && !HasTransitionTo(state, leavingState, states))
                {
                    AddAutoTransition(state, leavingState, "PatienceExpiredCondition", states);
                    Debug.Log($"🔥 CRITICAL: Added PatienceExpired transition: {state.stateName} → {leavingState.stateName}");
                }
                else
                {
                    Debug.LogError($"❌ FAILED: Could not find LeavingState for PatienceExpired transition from {state.stateName}");
                    Debug.Log($"Available states: {string.Join(", ", states.Select(s => s.stateName))}");
                }
            }
            else if (state.stateName.Contains("Idle"))
            {
                // IdleState should have TWO transitions for VIP/Normal flow
                
                // 1. Normal flow: Idle → MovingToQueue
                var movingToQueueState = states.FirstOrDefault(s => s.stateName.Contains("MovingToQueue"));
                if (movingToQueueState != null && !HasTransitionTo(state, movingToQueueState, states))
                {
                    AddAutoTransition(state, movingToQueueState, null, states); // No condition (automatic)
                }
                
                // 2. VIP flow: Idle → MovingToService (for A class customers)
                var movingToServiceState = states.FirstOrDefault(s => s.stateName.Contains("MovingToService"));
                if (movingToServiceState != null && !HasTransitionTo(state, movingToServiceState, states))
                {
                    AddAutoTransition(state, movingToServiceState, null, states); // VIP logic in IdleState
                }
            }
            else if (state.stateName.Contains("Leaving"))
            {
                // LeavingState → Exit (destroy customer)
                // This is handled as null target in CustomerStateMachine
                Debug.Log($"LeavingState {state.stateName} should transition to exit/destroy");
            }
        }
        
        private static void ConnectOrphanedStates(List<StateData> states)
        {
            var unconnectedStates = states.Where(s => s.transitions.Count == 0 && !s.isInitialState).ToList();
            
            foreach (var orphanState in unconnectedStates)
            {
                StateData targetState = FindLogicalTarget(orphanState, states);
                
                if (targetState != null && !HasTransitionTo(orphanState, targetState, states))
                {
                    string conditionName = DetermineConditionType(orphanState, targetState);
                    AddAutoTransition(orphanState, targetState, conditionName, states);
                    Debug.Log($"Auto-connected orphaned state: {orphanState.stateName} → {targetState.stateName}");
                }
                else
                {
                    var nextState = FindNextStateInFlow(orphanState, states);
                    if (nextState != null && !HasTransitionTo(orphanState, nextState, states))
                    {
                        AddAutoTransition(orphanState, nextState, null, states);
                        Debug.Log($"Flow-connected orphaned state: {orphanState.stateName} → {nextState.stateName}");
                    }
                }
            }
            
            // Connect initial state if it has no transitions
            var initialState = states.FirstOrDefault(s => s.isInitialState);
            if (initialState != null && initialState.transitions.Count == 0)
            {
                var firstFlow = FindFirstFlowState(states);
                if (firstFlow != null && !HasTransitionTo(initialState, firstFlow, states))
                {
                    AddAutoTransition(initialState, firstFlow, null, states);
                    Debug.Log($"Connected initial state: {initialState.stateName} → {firstFlow.stateName}");
                }
            }
        }
        
        private static void CreateReverseConnections(List<StateData> states)
        {
            Debug.Log("=== Creating Reverse/Missing Connections ===");
            
            var connectedPairs = new HashSet<string>();
            
            // Record all existing connections
            foreach (var state in states)
            {
                foreach (var transition in state.transitions)
                {
                    if (transition.targetStateIndex > 0 && transition.targetStateIndex <= states.Count)
                    {
                        var targetState = states[transition.targetStateIndex - 1];
                        connectedPairs.Add($"{state.stateName}→{targetState.stateName}");
                    }
                }
            }
            
            // Look for missing connections
            foreach (var state in states)
            {
                foreach (var transition in state.transitions)
                {
                    if (transition.targetStateIndex > 0 && transition.targetStateIndex <= states.Count)
                    {
                        var targetState = states[transition.targetStateIndex - 1];
                        
                        var potentialNext = FindPotentialNextState(targetState, connectedPairs, states);
                        if (potentialNext != null && !HasTransitionTo(targetState, potentialNext, states))
                        {
                            var conditionType = DetermineConditionType(targetState, potentialNext);
                            AddAutoTransition(targetState, potentialNext, conditionType, states);
                            connectedPairs.Add($"{targetState.stateName}→{potentialNext.stateName}");
                            Debug.Log($"Added missing connection: {targetState.stateName} → {potentialNext.stateName}");
                        }
                    }
                }
            }
            
            ConnectReferencedStates(connectedPairs, states);
        }
        
        private static StateData FindLogicalTarget(StateData sourceState, List<StateData> states)
        {
            var stateName = sourceState.stateName.ToLower();
            
            if (stateName.Contains("moving") && stateName.Contains("queue"))
            {
                return states.FirstOrDefault(s => s.stateName.ToLower().Contains("waiting"));
            }
            else if (stateName.Contains("moving") && stateName.Contains("service"))
            {
                return states.FirstOrDefault(s => s.stateName.ToLower().Contains("served"));
            }
            else if (stateName.Contains("waiting"))
            {
                return states.FirstOrDefault(s => s.stateName.ToLower().Contains("moving") && s.stateName.ToLower().Contains("service"));
            }
            else if (stateName.Contains("served") || stateName.Contains("service"))
            {
                return states.FirstOrDefault(s => s.stateName.ToLower().Contains("leaving") || s.stateName.ToLower().Contains("exit"));
            }
            else if (stateName.Contains("idle") || stateName.Contains("start"))
            {
                return states.FirstOrDefault(s => s.stateName.ToLower().Contains("moving"));
            }
            
            return null;
        }
        
        private static StateData FindNextStateInFlow(StateData sourceState, List<StateData> states)
        {
            var flowOrder = new[]
            {
                "start", "idle", "spawn",
                "moving", "queue",
                "waiting", "queue",
                "moving", "service", 
                "served", "service",
                "leaving", "exit", "destroy"
            };
            
            string sourceName = sourceState.stateName.ToLower();
            int sourceIndex = -1;
            
            for (int i = 0; i < flowOrder.Length; i++)
            {
                if (sourceName.Contains(flowOrder[i]))
                {
                    sourceIndex = i;
                    break;
                }
            }
            
            if (sourceIndex == -1) return null;
            
            for (int i = sourceIndex + 1; i < flowOrder.Length; i++)
            {
                var nextState = states.FirstOrDefault(s => 
                    s.stateName.ToLower().Contains(flowOrder[i]) && s != sourceState);
                if (nextState != null)
                {
                    return nextState;
                }
            }
            
            return null;
        }
        
        private static StateData FindFirstFlowState(List<StateData> states)
        {
            var firstCandidates = new[] { "moving", "spawn", "enter", "start" };
            
            foreach (var candidate in firstCandidates)
            {
                var state = states.FirstOrDefault(s => 
                    s.stateName.ToLower().Contains(candidate) && !s.isInitialState);
                if (state != null) return state;
            }
            
            return states.FirstOrDefault(s => !s.isInitialState);
        }
        
        private static StateData FindPotentialNextState(StateData currentState, HashSet<string> existingConnections, List<StateData> states)
        {
            if (currentState.transitions.Count > 0) return null;
            
            var currentName = currentState.stateName.ToLower();
            
            if (currentName.Contains("waiting") && currentName.Contains("queue"))
            {
                return states.FirstOrDefault(s => 
                    s.stateName.ToLower().Contains("moving") && 
                    s.stateName.ToLower().Contains("service") &&
                    !existingConnections.Contains($"{currentState.stateName}→{s.stateName}"));
            }
            else if (currentName.Contains("moving") && currentName.Contains("queue"))
            {
                return states.FirstOrDefault(s => 
                    s.stateName.ToLower().Contains("waiting") &&
                    !existingConnections.Contains($"{currentState.stateName}→{s.stateName}"));
            }
            else if (currentName.Contains("moving") && currentName.Contains("service"))
            {
                return states.FirstOrDefault(s => 
                    (s.stateName.ToLower().Contains("served") || s.stateName.ToLower().Contains("service")) &&
                    !s.stateName.ToLower().Contains("moving") &&
                    !existingConnections.Contains($"{currentState.stateName}→{s.stateName}"));
            }
            else if (currentName.Contains("served") || (currentName.Contains("service") && !currentName.Contains("moving")))
            {
                return states.FirstOrDefault(s => 
                    s.stateName.ToLower().Contains("leaving") &&
                    !existingConnections.Contains($"{currentState.stateName}→{s.stateName}"));
            }
            
            return null;
        }
        
        private static void ConnectReferencedStates(HashSet<string> existingConnections, List<StateData> states)
        {
            Debug.Log("=== Connecting Referenced States ===");
            
            var targetStates = new HashSet<StateData>();
            
            foreach (var state in states)
            {
                foreach (var transition in state.transitions)
                {
                    if (transition.targetStateIndex > 0 && transition.targetStateIndex <= states.Count)
                    {
                        targetStates.Add(states[transition.targetStateIndex - 1]);
                    }
                }
            }
            
            foreach (var targetState in targetStates)
            {
                if (targetState.transitions.Count == 0)
                {
                    var nextState = FindLogicalTarget(targetState, states);
                    if (nextState != null && !existingConnections.Contains($"{targetState.stateName}→{nextState.stateName}"))
                    {
                        var conditionType = DetermineConditionType(targetState, nextState);
                        AddAutoTransition(targetState, nextState, conditionType, states);
                        existingConnections.Add($"{targetState.stateName}→{nextState.stateName}");
                        Debug.Log($"Connected referenced state: {targetState.stateName} → {nextState.stateName}");
                    }
                }
            }
        }
        
        private static string DetermineConditionType(StateData fromState, StateData toState)
        {
            string fromName = fromState.stateName.ToLower();
            string toName = toState.stateName.ToLower();
            
            if (fromName.Contains("moving"))
            {
                return "ArrivedCondition";
            }
            else if (fromName.Contains("served") || fromName.Contains("service"))
            {
                return "ServiceCompletedCondition";
            }
            else if (fromName.Contains("waiting"))
            {
                if (toName.Contains("leaving"))
                {
                    return "PatienceExpiredCondition";
                }
                else
                {
                    return "QueuePositionChangedCondition";
                }
            }
            else if (fromName.Contains("idle") && toName.Contains("leaving"))
            {
                return "PatienceExpiredCondition";
            }
            
            return null;
        }
        
        private static bool HasTransitionTo(StateData fromState, StateData toState, List<StateData> states)
        {
            var targetIndex = states.IndexOf(toState) + 1;
            return fromState.transitions.Any(t => t.targetStateIndex == targetIndex);
        }
        
        private static void AddAutoTransition(StateData fromState, StateData toState, string conditionName, List<StateData> states)
        {
            var targetIndex = states.IndexOf(toState) + 1;
            
            var transition = new TransitionData
            {
                targetStateIndex = targetIndex,
                autoDetected = true
            };
            
            if (!string.IsNullOrEmpty(conditionName))
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
            
            fromState.transitions.Add(transition);
            
            Debug.Log($"Auto-detected transition: {fromState.stateName} → {toState.stateName}" + 
                     (string.IsNullOrEmpty(conditionName) ? " (Unconditional)" : $" (Condition: {conditionName})"));
        }
    }
}