using CoreFramework;
using BehaviorCore;

/// <summary>
/// 通用加载/入场状态。
/// 主要给敌人这类需要先播加载或入场行为的单位使用；若未配置 Load，则自动回退到 Idle。
/// </summary>
public sealed class LoadState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Load;

    public LoadState(HSM hsm, CharacterContext context) : base(hsm, context)
    {
    }

    public override void OnEnter()
    {
        SetStance(CharacterStance.Standing);
        BehaviorClip loadClip = GetBehavior(BehaviorKeys.Load);
        if (loadClip == null || loadClip.wrapMode != UnityEngine.WrapMode.Once)
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        Ctx.Interpreter.OnCompleted += OnCompleted;
        RequestBehavior(BehaviorKeys.Load);
    }

    public override void OnUpdate()
    {
        TickRuntime();
        if (TryHandleDeath()) return;
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
