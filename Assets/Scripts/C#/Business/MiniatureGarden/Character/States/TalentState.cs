using BehaviorCore;
using CoreFramework;

/// <summary>
/// 角色天赋技能状态。
/// </summary>
public sealed class TalentState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Talent;

    public TalentState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        SetStance(CharacterStance.Standing);
        BehaviorClip talentClip = GetBehavior(BehaviorKeys.Talent);
        if (talentClip == null)
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        StopMovementImmediately();

        if (Ctx.Config != null)
            Ctx.Cooldowns.StartCD("Talent", Ctx.Config.TalentCooldown);

        Ctx.Interpreter.OnCompleted += OnCompleted;
        TryFaceProjectileTarget();
        RequestBehavior(BehaviorKeys.Talent);
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
