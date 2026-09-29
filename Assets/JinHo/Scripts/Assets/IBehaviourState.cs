/// <summary>Defines the lifecycle of a monster behaviour state.</summary>
public interface IBehaviourState
{
    void Enter();
    void Tick();
    void Exit();
}
