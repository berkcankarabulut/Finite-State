using System.Collections.Generic;
using UnityEngine;

namespace FiniteState.Runtime
{
    public abstract class StateMachine<T> where T : MonoBehaviour
    {
        protected T owner;
        private IState _currentState;
        private IState _previousState;
        private Dictionary<System.Type, IState> _stateCache;
        public IState CurrentState => _currentState;
        
        protected StateMachine(T owner)
        {
            this.owner = owner;
            _stateCache = new Dictionary<System.Type, IState>();
        }
    
        public void Update()
        {
            if (_currentState == null) return;
            _currentState.Execute();
                 
            IState newState = _currentState.CheckTransitions(); 
            if (newState == null || newState == _currentState) return;
            ChangeState(newState);
        }

        // Public method to change state with IState instance
        public void ChangeState(IState newState)
        {
            if (_currentState != null)
                _currentState.Exit();
                
            _previousState = _currentState;
            _currentState = newState;
            
            if (_currentState != null)
                _currentState.Enter();
        }
     
        public void ChangeState<TState>() where TState : IState
        {
            System.Type stateType = typeof(TState);
            
            if (_currentState != null && _currentState.GetType() == stateType)
                return;
                
            IState newState;
            if (!_stateCache.TryGetValue(stateType, out newState))
            {
                newState = System.Activator.CreateInstance(stateType, new object[] { owner, this }) as IState;
                _stateCache[stateType] = newState;
            }
            
            ChangeState(newState);
        }
     
        public void RevertToPreviousState()
        {
            if (_previousState != null)
            {
                ChangeState(_previousState);
            }
        }
    }
}