using System.Collections.Generic;

namespace AIState.Runtime
{
    public class Transition
    {
        public IState TargetState { get; private set; }
        public List<Condition> Conditions { get; private set; }
    
        public Transition(IState targetState)
        {
            TargetState = targetState;
            Conditions = new List<Condition>();
        }
    
        public void AddCondition(Condition condition)
        {
            Conditions.Add(condition);
        }
    
        public bool CanTransition()
        {
            foreach (Condition condition in Conditions)
            {
                if (!condition.Evaluate())
                    return false;
            }
            return true;
        }
    }
}