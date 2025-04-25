using System.Collections.Generic;
using UnityEngine;

namespace AIState.Runtime
{
    public abstract class StateMachine<T> where T : MonoBehaviour
    {
        private T owner;
        private IState currentState;
        private IState previousState;
        private Dictionary<System.Type, IState> stateCache;
    
        public StateMachine(T owner)
        {
            this.owner = owner;
            stateCache = new Dictionary<System.Type, IState>();
        }
    
        public void Update()
        {
            if (currentState != null)
            { 
                currentState.Execute();
                 
                IState newState = currentState.CheckTransitions();
                
                if (newState != null && newState != currentState)
                {
                    ChangeState(newState);
                }
            }
        }
     
        public void ChangeState(IState newState)
        {
            if (currentState != null)
                currentState.Exit();
                
            previousState = currentState;
            currentState = newState;
            
            if (currentState != null)
                currentState.Enter();
        }
     
        public void ChangeState<TState>() where TState : IState
        {
            System.Type stateType = typeof(TState);
            
            if (currentState != null && currentState.GetType() == stateType)
                return;
                
            IState newState;
            if (!stateCache.TryGetValue(stateType, out newState))
            {
                newState = System.Activator.CreateInstance(stateType, new object[] { owner, this }) as IState;
                stateCache[stateType] = newState;
            }
            
            ChangeState(newState);
        }
     
        public void RevertToPreviousState()
        {
            if (previousState != null)
            {
                ChangeState(previousState);
            }
        }
    }
}