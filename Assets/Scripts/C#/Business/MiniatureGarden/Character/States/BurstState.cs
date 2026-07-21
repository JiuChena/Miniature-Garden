using CoreFramework;
using BehaviorCore;

/// <summary>
/// 角色爆发技能状态。
/// </summary>
public sealed class BurstState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Burst;

    public BurstState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        SetStance(CharacterStance.Standing);
        BehaviorClip burstClip = GetBehavior(BehaviorKeys.Burst);
        if (burstClip == null)
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        StopMovementImmediately();

        if (Ctx.Config != null)
            Ctx.Resources.TryConsume(Ctx.Config.BurstCost);

        if (Ctx.Config != null)
            Ctx.Cooldowns.StartCD("Burst", Ctx.Config.BurstCooldown);

        Ctx.Interpreter.OnCompleted += OnCompleted;
        TryFaceProjectileTarget();
        RequestBehavior(BehaviorKeys.Burst);
    }

    public override void OnUpdate()
    {
        TickRuntime();
        if (TryHandleDeath()) return;
        TryHandlePolicyTransition();
    }

    public override void OnExit()
    {
        Ctx.Interpreter.OnCompleted -= OnCompleted;
    }

    private void OnCompleted(BehaviorClip completedClip)
    {
        Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
    }
}
