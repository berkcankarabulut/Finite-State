namespace FiniteState.Runtime
{  
    public interface IState
    {
        void Enter();
        void Execute();
        void Exit();
        void AddTransition(Transition transition);
        IState CheckTransitions();
    }
}
