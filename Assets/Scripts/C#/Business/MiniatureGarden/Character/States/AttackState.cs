using CoreFramework;
using BehaviorCore;

/// <summary>
/// 角色普通攻击状态，支持起手、持续攻击与结束收尾。
/// </summary>
public sealed class AttackState : CharacterStateBase
{
    protected override CharacterStateId StateId => CharacterStateId.Attack;

    private CharacterAttackPlaybackStage _playbackStage;
    private CharacterStance _attackStance;
    private int _loopIndex;
    private bool _loopQueued;
    private float _queuedLoopTransitionDuration = -1f;

    public AttackState(HSM hsm, CharacterContext context) : base(hsm, context) { }

    public override void OnEnter()
    {
        _playbackStage = CharacterAttackPlaybackStage.None;
        _attackStance = Ctx.CurrentStance;
        _loopIndex = -1;
        _loopQueued = false;
        _queuedLoopTransitionDuration = -1f;
        Ctx.HasQueuedStanceChangeAfterAttack = false;
        Ctx.QueuedStanceAfterAttack = Ctx.CurrentStance;
        StopMovementImmediately();
        Ctx.Interpreter.OnCompleted += OnCompleted;
        if (!PlayEnterAttack())
            Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
    }

    public override void OnUpdate()
    {
        TickRuntime();
        if (TryHandleDeath()) return;

        HandleAttackStanceInput();
        HandleAttackLoopInput();

        TryHandlePolicyTransition();
    }

    public override void OnExit()
    {
        Ctx.Interpreter.OnCompleted -= OnCompleted;
        _playbackStage = CharacterAttackPlaybackStage.None;
        _attackStance = CharacterStance.Standing;
        _loopIndex = -1;
        _loopQueued = false;
        _queuedLoopTransitionDuration = -1f;
        Ctx.HasQueuedStanceChangeAfterAttack = false;
        Ctx.QueuedStanceAfterAttack = CharacterStance.Standing;
    }

    private void OnCompleted(BehaviorClip completedClip)
    {
        switch (_playbackStage)
        {
            case CharacterAttackPlaybackStage.Start:
            case CharacterAttackPlaybackStage.Loop:
                if (TryResolveNextAttackPlayback(completedClip))
                    return;

                SwitchToPostAttackState();
                return;

            case CharacterAttackPlaybackStage.End:
            default:
                SwitchToPostAttackState();
                return;
        }
    }

    private bool PlayEnterAttack()
    {
        if (Ctx.AttackResolver == null || !Ctx.AttackResolver.TryResolveEnterAttack(out CharacterAttackPlayRequest request))
            return false;

        return PlayAttackRequest(request);
    }

    private bool PlayLoopAttack(BehaviorClip completedClip)
    {
        if (Ctx.AttackResolver == null ||
            !Ctx.AttackResolver.TryResolveLoopAttack(_loopIndex, completedClip, out CharacterAttackPlayRequest request))
        {
            return false;
        }

        if (_queuedLoopTransitionDuration >= 0f)
            Ctx.PendingBehaviorTransitionDuration = _queuedLoopTransitionDuration;

        _loopQueued = false;
        _queuedLoopTransitionDuration = -1f;
        return PlayAttackRequest(request);
    }

    private bool PlayEndAttack(BehaviorClip completedClip)
    {
        _loopQueued = false;
        _queuedLoopTransitionDuration = -1f;
        if (Ctx.AttackResolver == null ||
            !Ctx.AttackResolver.TryResolveEndAttack(completedClip, out CharacterAttackPlayRequest request))
        {
            return false;
        }

        return PlayAttackRequest(request);
    }

    private bool PlayAttackRequest(CharacterAttackPlayRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BehaviorKey))
            return false;

        BehaviorClip[] targetGroup = GetBehaviorGroup(request.BehaviorKey);
        if (targetGroup.Length == 0)
            return false;

        int safeIndex = UnityEngine.Mathf.Clamp(request.ClipIndex, 0, targetGroup.Length - 1);
        TryFaceProjectileTarget();
        bool played = RequestBehavior(request.BehaviorKey, safeIndex);
        if (!played)
            return false;

        _playbackStage = request.PlaybackStage;
        _attackStance = request.AttackStance;
        SetStance(request.AttackStance);
        _loopIndex = request.PlaybackStage == CharacterAttackPlaybackStage.Loop ? safeIndex : -1;
        return true;
    }

    private bool TryResolveNextAttackPlayback(BehaviorClip completedClip)
    {
        bool stanceChangedAfterAttack = TryConsumeQueuedStanceAfterAttack();
        bool wantsContinueAttack = ShouldContinueAttack();

        if (wantsContinueAttack)
        {
            if (stanceChangedAfterAttack)
            {
                if (PlayEnterAttack())
                    return true;
            }
            else if (PlayLoopAttack(completedClip))
            {
                return true;
            }
        }

        if (ShouldPlayAttackEnd() && PlayEndAttack(completedClip))
            return true;

        return false;
    }

    private void HandleAttackLoopInput()
    {
        if (_playbackStage == CharacterAttackPlaybackStage.End)
        {
            TryInterruptAttackEnd();
            return;
        }

        bool wantsContinueAttack = Board != null && (Board.AttackPressed || Board.AttackHeld);
        if (!wantsContinueAttack)
            return;

        if (!HasLoopGroup())
        {
            Ctx.LastTransitionRejectReason = "当前角色没有配置可持续攻击的 AttackLoop/Attack 行为组。";
            return;
        }

        if (IsInAttackQueueWindow())
        {
            _loopQueued = true;
            _queuedLoopTransitionDuration = ResolveAttackTransitionDuration();
            return;
        }

        Ctx.LastTransitionRejectReason = "当前不在持续攻击输入窗口内。";
    }

    private void HandleAttackStanceInput()
    {
        if (Board == null || !Board.CrouchPressed)
            return;

        if (_attackStance != CharacterStance.Crouching)
        {
            Ctx.LastTransitionRejectReason = "站立攻击期间不会立即切换为蹲下攻击，请在攻击结束后再切换姿态。";
            return;
        }

        Ctx.HasQueuedStanceChangeAfterAttack = true;
        Ctx.QueuedStanceAfterAttack = CharacterStance.Standing;
    }

    private bool IsInAttackQueueWindow()
    {
        BehaviorClip currentClip = Ctx.Interpreter.CurrentClip;
        if (currentClip == null)
            return false;

        return currentClip.HasTransitionDefinitions &&
               currentClip.TryGetTransitionDefinition(BehaviorKeys.Attack, Ctx.Interpreter.ElapsedTime, out _);
    }

    private float ResolveAttackTransitionDuration()
    {
        BehaviorClip currentClip = Ctx.Interpreter.CurrentClip;
        if (currentClip != null &&
            currentClip.TryGetTransitionDefinition(BehaviorKeys.Attack, Ctx.Interpreter.ElapsedTime, out BehaviorTransitionDefinition definition))
        {
            return definition.crossFadeDuration;
        }

        return -1f;
    }

    private bool ShouldContinueAttack()
    {
        return _loopQueued || (Board != null && (Board.AttackHeld || Board.AttackPressed));
    }

    private bool TryConsumeQueuedStanceAfterAttack()
    {
        if (!Ctx.HasQueuedStanceChangeAfterAttack)
            return false;

        SetStance(Ctx.QueuedStanceAfterAttack);
        _attackStance = Ctx.CurrentStance;
        Ctx.HasQueuedStanceChangeAfterAttack = false;
        return true;
    }

    private bool HasLoopGroup()
    {
        return GetLoopGroup().Length > 0;
    }

    private BehaviorClip[] GetLoopGroup()
    {
        if (Ctx.AttackResolver == null)
            return System.Array.Empty<BehaviorClip>();

        string loopGroupKey = Ctx.AttackResolver.ResolveLoopGroupKey();
        return string.IsNullOrWhiteSpace(loopGroupKey)
            ? System.Array.Empty<BehaviorClip>()
            : GetBehaviorGroup(loopGroupKey);
    }

    private void SwitchToPostAttackState()
    {
        if (Board != null &&
            Board.MoveInput.sqrMagnitude > 0.01f &&
            Ctx.Conditions != null &&
            Ctx.Conditions.CanMove())
        {
            Hsm.SwitchState<MoveState>(InterruptPriority.Normal);
            return;
        }

        Hsm.SwitchState<IdleState>(InterruptPriority.Normal);
    }

    private bool ShouldPlayAttackEnd()
    {
        if (Board == null)
            return true;

        if (Board.AttackPressed || Board.AttackHeld || Board.TalentPressed || Board.BurstPressed || Board.ReloadPressed)
            return false;

        return Board.MoveInput.sqrMagnitude <= 0.01f;
    }

    private void TryInterruptAttackEnd()
    {
        if (Board == null)
            return;

        if (Board.AttackPressed || Board.AttackHeld)
        {
            if (Ctx.Conditions != null && Ctx.Conditions.CanAttack())
                Hsm.SwitchState<AttackState>(InterruptPriority.Normal);
            return;
        }

        if (Board.MoveInput.sqrMagnitude > 0.01f)
        {
            if (Ctx.Conditions != null && Ctx.Conditions.CanMove())
                Hsm.SwitchState<MoveState>(InterruptPriority.Normal);
        }
    }
}
