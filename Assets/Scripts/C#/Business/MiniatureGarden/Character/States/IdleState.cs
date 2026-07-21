using CoreFramework;
using BehaviorCore;

/// <summary>
/// 角色待机状态。
/// </summary>
public sealed class IdleState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Idle;

    public IdleState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        RequestBehavior(ResolveIdleBehaviorKey());
    }

    public override void OnUpdate()
    {
        TickRuntime();
        if (TryHandleDeath()) return;
        if (TryHandleStanceToggle()) return;
        TryHandlePolicyTransition();
    }

    public override void OnExit() { }

    private bool TryHandleStanceToggle()
    {
        if (Board == null || !Board.CrouchPressed)
            return false;

        if (!TryResolveToggledStance(out CharacterStance nextStance))
            return false;

        SetStance(nextStance);
        StopMovementImmediately();
        RequestBehavior(ResolveIdleBehaviorKey());
        return true;
    }
}
