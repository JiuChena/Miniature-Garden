using BehaviorCore;

/// <summary>
/// 默认攻击解析器。
/// 规则：
/// 1. 有 AttackEntry 时，首次起手优先播 AttackEntry。
/// 2. 持续攻击段优先播 AttackLoop，没有则回退到旧的 Attack。
/// 3. 仅当攻击结束后准备回 Idle 且配置了 AttackEnd 时，才额外播放收尾段。
/// </summary>
public sealed class DefaultCharacterAttackResolver : ICharacterAttackResolver
{
    private CharacterContext _context;

    public void Initialize(CharacterContext context)
    {
        _context = context;
    }

    public bool TryResolveEnterAttack(out CharacterAttackPlayRequest request)
    {
        request = default;
        if (_context == null || _context.Config == null)
            return false;

        bool isCrouching = _context.CurrentStance == CharacterStance.Crouching;
        string entryKey = isCrouching ? BehaviorKeys.CrouchAttackStart : BehaviorKeys.AttackStart;
        string loopKey = isCrouching ? BehaviorKeys.CrouchAttackLoop : BehaviorKeys.AttackLoop;
        bool hasAttackEntry = _context.Config.HasBehavior(entryKey);
        bool hasAttackLoop = _context.Config.HasBehavior(loopKey);
        bool hasAttackFallback = !isCrouching && _context.Config.HasBehavior(BehaviorKeys.Attack);

        if (hasAttackEntry)
        {
            request.BehaviorKey = entryKey;
            request.ClipIndex = 0;
            request.PlaybackStage = CharacterAttackPlaybackStage.Start;
            request.AttackStance = _context.CurrentStance;
            return true;
        }

        if (hasAttackLoop)
        {
            request.BehaviorKey = loopKey;
            request.ClipIndex = 0;
            request.PlaybackStage = CharacterAttackPlaybackStage.Loop;
            request.AttackStance = _context.CurrentStance;
            return true;
        }

        if (hasAttackFallback)
        {
            request.BehaviorKey = BehaviorKeys.Attack;
            request.ClipIndex = 0;
            request.PlaybackStage = CharacterAttackPlaybackStage.Loop;
            request.AttackStance = CharacterStance.Standing;
            return true;
        }

        return false;
    }

    public bool TryResolveLoopAttack(int currentLoopIndex, BehaviorClip completedClip, out CharacterAttackPlayRequest request)
    {
        request = default;
        string loopGroupKey = ResolveLoopGroupKey();
        if (string.IsNullOrWhiteSpace(loopGroupKey))
            return false;

        BehaviorClip[] loopGroup = _context.Config.GetBehaviorGroup(loopGroupKey);
        if (loopGroup == null || loopGroup.Length == 0)
            return false;

        request.BehaviorKey = loopGroupKey;
        request.ClipIndex = ResolveNextLoopClipIndex(currentLoopIndex, loopGroup.Length);
        request.PlaybackStage = CharacterAttackPlaybackStage.Loop;
        request.AttackStance = _context.CurrentStance;
        return true;
    }

    public bool TryResolveEndAttack(BehaviorClip completedClip, out CharacterAttackPlayRequest request)
    {
        request = default;
        if (_context == null || _context.Config == null)
            return false;

        string endKey = _context.CurrentStance == CharacterStance.Crouching
            ? BehaviorKeys.CrouchAttackEnd
            : BehaviorKeys.AttackEnd;
        if (!_context.Config.HasBehavior(endKey))
            return false;

        request.BehaviorKey = endKey;
        request.ClipIndex = 0;
        request.PlaybackStage = CharacterAttackPlaybackStage.End;
        request.AttackStance = _context.CurrentStance;
        return true;
    }

    public string ResolveLoopGroupKey()
    {
        if (_context == null || _context.Config == null)
            return string.Empty;

        if (_context.CurrentStance == CharacterStance.Crouching)
        {
            if (_context.Config.HasBehavior(BehaviorKeys.CrouchAttackLoop))
                return BehaviorKeys.CrouchAttackLoop;

            return string.Empty;
        }

        if (_context.Config.HasBehavior(BehaviorKeys.AttackLoop))
            return BehaviorKeys.AttackLoop;

        if (_context.Config.HasBehavior(BehaviorKeys.Attack))
            return BehaviorKeys.Attack;

        return string.Empty;
    }

    private static int ResolveNextLoopClipIndex(int currentLoopIndex, int groupLength)
    {
        if (groupLength <= 0)
            return 0;

        if (groupLength == 1 || currentLoopIndex < 0)
            return 0;

        int nextIndex = currentLoopIndex + 1;
        if (nextIndex >= groupLength)
            nextIndex = 0;

        return nextIndex;
    }
}
