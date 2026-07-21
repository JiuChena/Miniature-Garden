using CoreFramework;
using BehaviorCore;

/// <summary>
/// 默认角色行为转换策略，提供通用的待机、移动、攻击、天赋、爆发与装填切换规则。
/// </summary>
public sealed class DefaultCharacterTransitionPolicy : ICharacterTransitionPolicy
{
    private CharacterContext _context;

    public void Initialize(CharacterContext context)
    {
        _context = context;
    }

    public bool TryResolveTransition(CharacterStateId currentState, out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.Priority = InterruptPriority.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;

        if (_context == null || _context.Board == null || _context.Conditions == null)
            return false;

        if (_context.Data != null && _context.Data.IsDead)
        {
            _context.LastTransitionRejectReason = "角色已死亡，普通行为切换已停止。";
            return false;
        }

        Blackboard board = _context.Board;
        switch (currentState)
        {
            case CharacterStateId.Idle:
            case CharacterStateId.Move:
                if (TryResolvePrimaryAction(board, out request))
                    return true;

                if (TryResolveLocomotion(currentState, board, out request))
                {
                    return true;
                }

                return false;

            case CharacterStateId.Attack:
                if (TryResolveAttackCancel(board, out request))
                    return true;

                return false;

            case CharacterStateId.Talent:
                if (TryResolveTalentCancel(board, out request))
                    return true;

                return false;

            default:
                return false;
        }
    }

    private bool TryResolvePrimaryAction(Blackboard board, out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;

        if (board.BurstPressed)
        {
            if (!_context.Conditions.CanBurst(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Burst, CharacterStateId.Burst, InterruptPriority.Burst,
                out request);
        }

        if (board.TalentPressed)
        {
            if (!_context.Conditions.CanTalent(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Talent, CharacterStateId.Talent, InterruptPriority.Talent,
                out request);
        }

        if (board.ReloadPressed)
        {
            if (!_context.Conditions.CanReload(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(ResolveReloadBehaviorKey(), CharacterStateId.Reload, InterruptPriority.Normal,
                out request);
        }

        if (board.JumpPressed)
        {
            if (!_context.Conditions.CanVault(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.MoveJump, CharacterStateId.Vault, InterruptPriority.Normal,
                out request);
        }

        if (board.AttackPressed || board.AttackHeld)
        {
            if (!_context.Conditions.CanAttack(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Attack, CharacterStateId.Attack, InterruptPriority.Normal,
                out request);
        }

        return false;
    }

    private bool TryResolveLocomotion(CharacterStateId currentState, Blackboard board,
        out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;

        if (_context.CurrentStance == CharacterStance.Crouching)
        {
            if (board.MoveInput.sqrMagnitude > 0.01f)
                _context.LastTransitionRejectReason = "当前处于蹲下姿态，不能执行普通移动。";

            return false;
        }

        bool hasMoveInput = board.MoveInput.sqrMagnitude > 0.01f;
        if (!hasMoveInput && currentState != CharacterStateId.Move)
            return false;

        if (!_context.Conditions.CanMove(out string reason))
        {
            if (hasMoveInput)
                _context.LastTransitionRejectReason = reason;

            return false;
        }

        if (currentState == CharacterStateId.Idle && hasMoveInput)
        {
            request.NextState = CharacterStateId.Move;
            request.Priority = InterruptPriority.Movement;
            request.TargetBehaviorKey = BehaviorKeys.Move;
            request.BypassBehaviorInterruptGuard = true;
            return true;
        }

        if (currentState == CharacterStateId.Move && !hasMoveInput)
        {
            request.NextState = CharacterStateId.Idle;
            request.Priority = InterruptPriority.Movement;
            request.TargetBehaviorKey = BehaviorKeys.Idle;
            request.BypassBehaviorInterruptGuard = true;
            return true;
        }

        return false;
    }

    private bool TryResolveAttackCancel(Blackboard board, out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;

        if (board.BurstPressed)
        {
            if (!_context.Conditions.CanBurst(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Burst, CharacterStateId.Burst, InterruptPriority.Burst,
                out request);
        }

        if (board.TalentPressed)
        {
            if (!_context.Conditions.CanTalent(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Talent, CharacterStateId.Talent, InterruptPriority.Talent,
                out request);
        }

        if (board.ReloadPressed)
        {
            if (!_context.Conditions.CanReload(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(ResolveReloadBehaviorKey(), CharacterStateId.Reload, InterruptPriority.Normal,
                out request);
        }

        return false;
    }

    private bool TryResolveTalentCancel(Blackboard board, out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;

        if (board.BurstPressed)
        {
            if (!_context.Conditions.CanBurst(out string reason))
                return Reject(reason, out request);

            return TryCreateActionRequest(BehaviorKeys.Burst, CharacterStateId.Burst, InterruptPriority.Burst,
                out request);
        }

        return false;
    }

    private string ResolveReloadBehaviorKey()
    {
        if (_context == null || _context.Config == null)
            return BehaviorKeys.Reload;

        if (_context.CurrentStance == CharacterStance.Crouching &&
            _context.Config.HasBehavior(BehaviorKeys.CrouchReload))
        {
            return BehaviorKeys.CrouchReload;
        }

        return BehaviorKeys.Reload;
    }

    private bool TryCreateActionRequest(string behaviorKey, CharacterStateId nextState, InterruptPriority priority,
        out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = nextState;
        request.Priority = priority;
        request.CrossFadeDuration = -1f;
        request.TargetBehaviorKey = behaviorKey;
        request.BypassBehaviorInterruptGuard = false;

        BehaviorClip currentClip = _context.Interpreter != null ? _context.Interpreter.CurrentClip : null;
        if (currentClip == null || !currentClip.HasTransitionDefinitions)
            return true;

        float currentTime = _context.Interpreter.ElapsedTime;
        if (!currentClip.TryGetTransitionDefinition(behaviorKey, currentTime, out BehaviorTransitionDefinition definition))
        {
            _context.LastTransitionRejectReason =
                $"当前行为 '{currentClip.name}' 在 {currentTime:F2}s 未开放切换到行为 key '{behaviorKey}' 的时间窗。";
            return false;
        }

        request.CrossFadeDuration = definition.crossFadeDuration;
        return true;
    }

    private bool Reject(string reason, out CharacterTransitionRequest request)
    {
        request = default;
        request.NextState = CharacterStateId.None;
        request.Priority = InterruptPriority.None;
        request.CrossFadeDuration = -1f;
        request.BypassBehaviorInterruptGuard = false;
        _context.LastTransitionRejectReason = reason;
        return false;
    }
}
