using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FiniteState.Editor
{
    [System.Serializable]
    public class StateData
    {
        public MonoScript script;
        public string stateName;
        public bool isInitialState;
        public List<TransitionData> transitions = new List<TransitionData>();
        public bool expanded = true;
        public bool autoDetected = false;
        public Vector2 diagramPosition = Vector2.zero;
    }

    public enum NodeShape
    {
        Rectangle,
        Circle, 
        Diamond,
        Hexagon
    }
    
    public struct NodeStyle
    {
        public NodeShape shape;
        public Color primaryColor;
        public Color secondaryColor;
        public Color borderColor;
        public Vector2 size;
        public string icon;
        public bool isState; // true for States, false for Conditions
    }
    
    public struct DetectionResult
    {
        public bool found;
        public System.Type stateMachineType;
    }
}