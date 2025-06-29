using System.Collections.Generic;
using UnityEditor;

namespace FiniteState.Editor
{
    public class TransitionGroup
    {
        public StateData sourceState;      
        public StateData targetState;      
        public TransitionData transition; 
        public List<MonoScript> conditions; 
    }
}