using CoreFramework;

/// <summary>
/// 角色死亡状态。
/// </summary>
public sealed class DeathState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Death;

    public DeathState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        RequestBehavior(BehaviorKeys.Death);
    }

    public override void OnUpdate()
    {
        TickRuntime();
    }

    public override void OnExit() { }
}
