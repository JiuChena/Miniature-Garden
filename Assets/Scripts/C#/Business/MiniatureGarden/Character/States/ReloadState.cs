using BehaviorCore;
using CoreFramework;

/// <summary>
/// 角色装填状态。
/// </summary>
public sealed class ReloadState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Reload;

    public ReloadState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        string reloadBehaviorKey = ResolveReloadBehaviorKey();
        BehaviorClip reloadClip = GetBehavior(reloadBehaviorKey);
        if (reloadClip == null)
        {
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
            return;
        }

        StopMovementImmediately();

        Ctx.Interpreter.OnCompleted += OnCompleted;
        RequestBehavior(reloadBehaviorKey);
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
