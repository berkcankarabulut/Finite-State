using System.Collections.Generic;
using UnityEngine;

namespace FiniteState.Runtime
{
    public abstract class StateBase<T> : IState where T : MonoBehaviour
    {
        protected T owner;
        protected StateMachine<T> stateMachine;
        protected List<Transition> transitions;
    
        public StateBase(T owner, StateMachine<T> stateMachine)
        {
            this.owner = owner;
            this.stateMachine = stateMachine;
            transitions = new List<Transition>();
        }
    
        public virtual void Enter() { }
        public virtual void Execute() { }
        public virtual void Exit() { }
    
        public void AddTransition(Transition transition)
        {
            transitions.Add(transition);
        }
    
        public IState CheckTransitions()
        {
            foreach (Transition transition in transitions)
            {
                if (transition.CanTransition())
                {
                    return transition.TargetState;
                }
            }
            return null;
        }
    }
}