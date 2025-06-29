using System.Collections.Generic;
using UnityEditor;

namespace FiniteState.Editor
{
    [System.Serializable]
    public class TransitionData
    {
        public int targetStateIndex;
        public List<MonoScript> conditionScripts = new List<MonoScript>();
        public bool expanded = false;
        public bool autoDetected = false;
    }
}