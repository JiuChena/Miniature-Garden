using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// EnemyDriver 内部行为运行时。
/// 复用现有 CharacterContext、状态类、BehaviorInterpreter 与攻击解析器，只把“输入来源”改成 AI 黑板。
/// </summary>
internal sealed class EnemyBehaviorRuntime
{
    private readonly EnemyTransitionPolicy _defaultTransitionPolicy = new EnemyTransitionPolicy();
    private readonly DefaultCharacterAttackResolver _defaultAttackResolver = new DefaultCharacterAttackResolver();

    private readonly EnemyDriver _owner;
    private readonly BehaviorInterpreter _interpreter;
    private CharacterContext _context;
    private HSM _hsm;
    private CharacterConditions _conditions;
    private ICharacterTransitionPolicy _transitionPolicy;
    private ICharacterAttackResolver _attackResolver;
    private Animator _resolvedAnimator;
    private AnimatorSegmentPlayer _resolvedSegmentPlayer;

    public EnemyBehaviorRuntime(
        EnemyDriver owner,
        BehaviorInterpreter interpreter)
    {
        _owner = owner;
        _interpreter = interpreter;
    }

    public bool IsInIdleState => _hsm != null && _hsm.Current is IdleState;
    public bool IsInMoveState => _hsm != null && _hsm.Current is MoveState;
    public bool IsInVaultState => _hsm != null && _hsm.Current is VaultState;
    public bool IsInLoadState => _hsm != null && _hsm.Current is LoadState;
    public bool RequiresNavigationSuppression
    {
        get
        {
            if (_hsm == null || _hsm.Current == null)
                return false;

            return _hsm.Current is LoadState ||
                   _hsm.Current is AttackState ||
                   _hsm.Current is TalentState ||
                   _hsm.Current is BurstState ||
                   _hsm.Current is ReloadState ||
                   _hsm.Current is VaultState ||
                   _hsm.Current is DeathState ||
                   (_context != null && _context.CurrentStance == CharacterStance.Crouching);
        }
    }

    public string CurrentStateName
    {
        get
        {
            if (_hsm == null || _hsm.Current == null)
                return string.Empty;

            System.Type stateType = _hsm.Current.GetType();
            return stateType != null ? stateType.Name : string.Empty;
        }
    }

    public Animator Animator => _resolvedAnimator;
    public AnimatorSegmentPlayer SegmentPlayer => _resolvedSegmentPlayer;

    public void Initialize(CharacterContext context)
    {
        _context = context;
        RefreshBindings();
        ConfigureRuntimeRules();
        ConfigureStateMachine();
    }

    public void Dispose()
    {
        _hsm = null;
        _context = null;
        _conditions = null;
        _transitionPolicy = null;
        _attackResolver = null;
    }

    public void RefreshBindings()
    {
        _resolvedAnimator = ResolveAnimator(_owner);
        _resolvedSegmentPlayer = ResolveSegmentPlayer(_resolvedAnimator);
        DisableResidualPreviewDirectors(_owner);
        SyncResolvedBindings();
    }

    public void EnterInitialState(bool startOnAwake)
    {
        if (!startOnAwake || _hsm == null)
            return;

        if (_context != null &&
            _context.Config != null)
        {
            BehaviorClip loadClip = GetBehavior(BehaviorKeys.Load);
            if (loadClip != null && loadClip.wrapMode == WrapMode.Once)
            {
                _hsm.SwitchState<LoadState>(InterruptPriority.None);
                return;
            }
        }

        _hsm.SwitchState<IdleState>(InterruptPriority.None);
    }

    public void Tick(float deltaTime)
    {
        _hsm?.Tick();
    }

    public bool RequestBehavior(BehaviorClip clip)
    {
        if (_context == null || _interpreter == null || _owner == null || !_owner.IsInitialized)
            return false;

        _context.LastRequestedBehaviorClipName = clip != null ? clip.name : string.Empty;
        if (clip == null && string.IsNullOrWhiteSpace(_context.LastTransitionRejectReason))
        {
            string requestedKey = string.IsNullOrWhiteSpace(_context.LastRequestedBehaviorKey)
                ? "<Unknown>"
                : _context.LastRequestedBehaviorKey;
            _context.LastTransitionRejectReason = $"行为请求失败：行为 key '{requestedKey}' 没有可播放的 BehaviorClip。";
        }

        float transitionDuration = _context.PendingBehaviorTransitionDuration;
        _context.LastAppliedTransitionDuration = transitionDuration;
        _context.PendingBehaviorTransitionDuration = -1f;
        if (clip != null)
        {
            if (string.IsNullOrWhiteSpace(_context.CurrentBehaviorKey))
                _context.CurrentBehaviorKey = _context.LastRequestedBehaviorKey;

            _context.LastTransitionRejectReason = string.Empty;
        }

        _interpreter.Play(clip, transitionDuration);
        return clip != null;
    }

    public bool RequestBehavior(string key, int clipIndex = 0)
    {
        BehaviorClip clip = GetBehavior(key, clipIndex);
        if (_context != null)
        {
            _context.LastRequestedBehaviorKey = key ?? string.Empty;
            _context.LastRequestedBehaviorClipName = clip != null ? clip.name : string.Empty;
            _context.CurrentBehaviorKey = key ?? string.Empty;
        }

        return RequestBehavior(clip);
    }

    public BehaviorClip GetBehavior(string key, int clipIndex = 0)
    {
        return _context != null && _context.Config != null ? _context.Config.GetBehavior(key, clipIndex) : null;
    }

    public BehaviorClip[] GetBehaviorGroup(string key)
    {
        return _context != null && _context.Config != null
            ? _context.Config.GetBehaviorGroup(key)
            : System.Array.Empty<BehaviorClip>();
    }

    private void ConfigureRuntimeRules()
    {
        if (_context == null)
            return;

        _conditions = ResolveConditions();
        _transitionPolicy = ResolveTransitionPolicy();
        _attackResolver = ResolveAttackResolver();

        _context.Conditions = _conditions;
        _context.TransitionPolicy = _transitionPolicy;
        _context.AttackResolver = _attackResolver;

        _context.TransitionPolicy?.Initialize(_context);
        _context.AttackResolver?.Initialize(_context);
    }

    private void ConfigureStateMachine()
    {
        _hsm = new HSM
        {
            TransitionGuard = _context.Interpreter.CanBeInterruptedBy,
        };

        _hsm.AddState(new LoadState(_hsm, _context));
        _hsm.AddState(new IdleState(_hsm, _context));
        _hsm.AddState(new MoveState(_hsm, _context));
        _hsm.AddState(new AttackState(_hsm, _context));
        _hsm.AddState(new TalentState(_hsm, _context));
        _hsm.AddState(new BurstState(_hsm, _context));
        _hsm.AddState(new ReloadState(_hsm, _context));
        _hsm.AddState(new VaultState(_hsm, _context));
        _hsm.AddState(new DeathState(_hsm, _context));
    }

    private CharacterConditions ResolveConditions()
    {
        if (_context == null || _context.Config == null)
            return new CharacterConditions(_context);

        if (_context.Config.ConditionSourceAsset == null)
            return new CharacterConditions(_context);

        CharacterConditions configuredConditions = _context.Config.ConditionSourceAsset.CreateConditions(_context);
        return configuredConditions ?? new CharacterConditions(_context);
    }

    private ICharacterTransitionPolicy ResolveTransitionPolicy()
    {
        if (_context == null || _context.Config == null || _context.Config.TransitionPolicyAsset == null)
            return _defaultTransitionPolicy;

        return _context.Config.TransitionPolicyAsset;
    }

    private ICharacterAttackResolver ResolveAttackResolver()
    {
        if (_context == null || _context.Config == null || _context.Config.AttackResolverAsset == null)
            return _defaultAttackResolver;

        return _context.Config.AttackResolverAsset;
    }

    private void SyncResolvedBindings()
    {
        if (_context == null)
            return;

        _context.Animator = _resolvedAnimator;
    }

    private static Animator ResolveAnimator(EnemyDriver owner)
    {
        return owner != null ? owner.GetComponentInChildren<Animator>() : null;
    }

    private static AnimatorSegmentPlayer ResolveSegmentPlayer(Animator resolvedAnimator)
    {
        if (resolvedAnimator == null)
            return null;

        AnimatorSegmentPlayer segmentPlayer = resolvedAnimator.GetComponent<AnimatorSegmentPlayer>();
        if (segmentPlayer == null)
            segmentPlayer = resolvedAnimator.gameObject.AddComponent<AnimatorSegmentPlayer>();

        return segmentPlayer;
    }

    private static void DisableResidualPreviewDirectors(EnemyDriver owner)
    {
        UnityEngine.Playables.PlayableDirector[] directors =
            owner.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
        {
            UnityEngine.Playables.PlayableDirector director = directors[i];
            if (director == null)
                continue;

            director.playOnAwake = false;
            director.time = 0d;
            director.Stop();
        }
    }
}
