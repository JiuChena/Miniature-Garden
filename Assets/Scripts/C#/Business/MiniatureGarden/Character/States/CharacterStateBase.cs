using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// 角色状态基类。
/// </summary>
public abstract class CharacterStateBase : StateBase
{
    protected readonly CharacterContext Ctx;
    protected CoreFramework.Blackboard Board => Ctx.Board;
    protected abstract CharacterStateId StateId { get; }

    protected CharacterStateBase(HSM hsm, CharacterContext context) : base(hsm)
    {
        Ctx = context;
    }

    protected void TickRuntime()
    {
        Ctx.Cooldowns.Tick(Ctx.DeltaTime);
        Ctx.Interpreter.Tick(Ctx.DeltaTime);
    }

    protected bool RequestBehavior(BehaviorClip clip)
    {
        return Ctx.BehaviorRequester != null && Ctx.BehaviorRequester.RequestBehavior(clip);
    }

    protected bool RequestBehavior(string key, int clipIndex = 0)
    {
        return Ctx.BehaviorRequester != null && Ctx.BehaviorRequester.RequestBehavior(key, clipIndex);
    }

    protected BehaviorClip GetBehavior(string key, int clipIndex = 0)
    {
        return Ctx.BehaviorRequester != null ? Ctx.BehaviorRequester.GetBehavior(key, clipIndex) : null;
    }

    protected BehaviorClip[] GetBehaviorGroup(string key)
    {
        return Ctx.BehaviorRequester != null
            ? Ctx.BehaviorRequester.GetBehaviorGroup(key)
            : System.Array.Empty<BehaviorClip>();
    }

    protected string ResolveIdleBehaviorKey()
    {
        if (Ctx.CurrentStance == CharacterStance.Crouching && Ctx.Config != null && Ctx.Config.HasBehavior(BehaviorKeys.CrouchIdle))
            return BehaviorKeys.CrouchIdle;

        return BehaviorKeys.Idle;
    }

    protected string ResolveReloadBehaviorKey()
    {
        if (Ctx.CurrentStance == CharacterStance.Crouching && Ctx.Config != null && Ctx.Config.HasBehavior(BehaviorKeys.CrouchReload))
            return BehaviorKeys.CrouchReload;

        return BehaviorKeys.Reload;
    }

    protected void SetStance(CharacterStance stance)
    {
        Ctx.CurrentStance = stance;
    }

    protected bool TryResolveToggledStance(out CharacterStance nextStance)
    {
        nextStance = Ctx.CurrentStance;

        if (Ctx.CurrentStance == CharacterStance.Crouching)
        {
            if (Ctx.Conditions != null && !Ctx.Conditions.CanStandUp(out string standReason))
            {
                Ctx.LastTransitionRejectReason = standReason;
                return false;
            }

            nextStance = CharacterStance.Standing;
            return true;
        }

        if (Ctx.Conditions != null && !Ctx.Conditions.CanEnterCrouch(out string crouchReason))
        {
            Ctx.LastTransitionRejectReason = crouchReason;
            return false;
        }

        nextStance = CharacterStance.Crouching;
        return true;
    }

    protected bool TryHandleDeath()
    {
        if (Ctx.Data != null && Ctx.Data.IsDead)
        {
            Hsm.SwitchState<DeathState>(InterruptPriority.Death);
            return true;
        }

        return false;
    }

    protected bool TryHandlePolicyTransition()
    {
        if (Ctx.TransitionPolicy == null)
            return false;

        if (!Ctx.TransitionPolicy.TryResolveTransition(StateId, out CharacterTransitionRequest request))
            return false;

        Ctx.PendingBehaviorTransitionDuration = request.CrossFadeDuration;
        bool switched = SwitchState(request.NextState, request.Priority, request.BypassBehaviorInterruptGuard);
        if (!switched)
        {
            Ctx.PendingBehaviorTransitionDuration = -1f;
            if (string.IsNullOrWhiteSpace(Ctx.LastTransitionRejectReason))
            {
                if (Ctx.Interpreter != null && !Ctx.Interpreter.CanBeInterruptedBy(request.Priority))
                {
                    Ctx.LastTransitionRejectReason =
                        $"当前行为优先级不允许被 {request.NextState} 打断，所需优先级为 {request.Priority}。";
                }
                else
                {
                    Ctx.LastTransitionRejectReason = $"状态机拒绝切换到 {request.NextState}。";
                }
            }
        }
        else
        {
            string behaviorKey = string.IsNullOrWhiteSpace(request.TargetBehaviorKey)
                ? request.NextState.ToString()
                : request.TargetBehaviorKey;
            Ctx.LastAcceptedTransitionDescription =
                $"{StateId} -> {request.NextState} | BehaviorKey={behaviorKey} | Priority={request.Priority} | CrossFade={request.CrossFadeDuration:P0}";
            Ctx.LastTransitionRejectReason = string.Empty;
        }

        return switched;
    }

    protected void StopMovementImmediately()
    {
        if (Ctx.MovementStrategy is PlayerMovementModule playerMovement)
        {
            playerMovement.StopMovementImmediately();
        }

        if (Ctx.Transform != null && Ctx.Transform.TryGetComponent(out EnemyNavigationModule enemyNavigationModule))
            enemyNavigationModule.StopMovementImmediately();
    }

    protected void TryFaceProjectileTarget()
    {
        if (Ctx == null || Ctx.Data == null || Ctx.Transform == null)
            return;

        if (!Ctx.EnableAutomaticProjectileFacing)
        {
            Ctx.LastTargetFacingApplied = false;
            Ctx.LastTargetFacingDirection = Ctx.Transform.forward;
            return;
        }

        int targetingScopeId = Ctx.Interpreter != null ? Ctx.Interpreter.PeekNextTargetingScopeId() : 0;
        Vector3 beforeForward = Ctx.Transform.forward;
        bool applied = CharacterTargetingUtility.TryFaceProjectileTarget(Ctx.Data, Ctx.Transform, targetingScopeId);
        Ctx.LastTargetFacingApplied = applied;
        Ctx.LastTargetFacingDirection = applied ? Ctx.Transform.forward : beforeForward;
    }

    private bool SwitchState(CharacterStateId nextState, InterruptPriority priority, bool bypassBehaviorInterruptGuard = false)
    {
        switch (nextState)
        {
            case CharacterStateId.Load:
                return Hsm.SwitchState<LoadState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Idle:
                return Hsm.SwitchState<IdleState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Move:
                return Hsm.SwitchState<MoveState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Attack:
                return Hsm.SwitchState<AttackState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Talent:
                return Hsm.SwitchState<TalentState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Burst:
                return Hsm.SwitchState<BurstState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Reload:
                return Hsm.SwitchState<ReloadState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Vault:
                return Hsm.SwitchState<VaultState>(priority, bypassBehaviorInterruptGuard);
            case CharacterStateId.Death:
                return Hsm.SwitchState<DeathState>(priority, bypassBehaviorInterruptGuard);
            default:
                return false;
        }
    }
}
