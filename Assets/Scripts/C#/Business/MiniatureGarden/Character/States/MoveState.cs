using CoreFramework;
using BehaviorCore;

/// <summary>
/// 角色移动状态。
/// </summary>
public sealed class MoveState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Move;

    public MoveState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        if (Ctx.CurrentStance == CharacterStance.Crouching)
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        RequestBehavior(BehaviorKeys.Move);
    }

    public override void OnUpdate()
    {
        TickRuntime();
        if (TryHandleDeath()) return;
        if (TryHandleMoveToCrouch()) return;

        bool wantsActionTransition = Board != null &&
                                     (Board.TalentPressed || Board.BurstPressed || Board.ReloadPressed ||
                                      Board.AttackPressed || Board.AttackHeld);
        if (wantsActionTransition && TryHandlePolicyTransition())
            return;

        TryHandlePolicyTransition();
    }

    public override void OnExit() { }

    private bool TryHandleMoveToCrouch()
    {
        if (Board == null || !Board.CrouchPressed)
            return false;

        if (!TryResolveToggledStance(out CharacterStance nextStance))
            return false;

        StopMovementImmediately();
        SetStance(nextStance);
        Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
        return true;
    }
}
