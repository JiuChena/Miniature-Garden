using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// CharacterDriver 内部行为运行时。
/// 负责控制权、状态机、行为播放、规则装配与动画绑定。
/// </summary>
internal sealed class CharacterBehaviorRuntime
    : ICharacterModule
{
    private readonly DefaultCharacterTransitionPolicy _defaultTransitionPolicy = new DefaultCharacterTransitionPolicy();
    private readonly DefaultCharacterAttackResolver _defaultAttackResolver = new DefaultCharacterAttackResolver();
    private readonly Blackboard _playerCommandBoard = new Blackboard();
    private readonly Blackboard _idleBoard = new Blackboard();

    private readonly CharacterDriver _owner;
    private readonly BehaviorInterpreter _interpreter;
    private CharacterContext _context;
    private HSM _hsm;
    private CharacterConditions _conditions;
    private ICharacterTransitionPolicy _transitionPolicy;
    private ICharacterAttackResolver _attackResolver;
    private Animator _resolvedAnimator;
    private AnimatorSegmentPlayer _resolvedSegmentPlayer;

    public CharacterBehaviorRuntime(
        CharacterDriver owner,
        BehaviorInterpreter interpreter)
    {
        _owner = owner;
        _interpreter = interpreter;
    }

    public bool IsPlayerControlled { get; private set; }
    public bool IsInIdleState => _hsm != null && _hsm.Current is IdleState;
    public bool IsInMoveState => _hsm != null && _hsm.Current is MoveState;
    public bool IsInVaultState => _hsm != null && _hsm.Current is VaultState;
    public bool IsInDeathState => _hsm != null && _hsm.Current is DeathState;
    public bool UsesDirectPoseInheritanceOnSwitch =>
        _hsm != null && (_hsm.Current is IdleState || _hsm.Current is MoveState);
    public bool RequiresStayOnSwitch
    {
        get
        {
            if (_hsm == null || _hsm.Current == null)
                return false;

            return _hsm.Current is AttackState ||
                   _hsm.Current is TalentState ||
                   _hsm.Current is BurstState ||
                   _hsm.Current is ReloadState ||
                   _hsm.Current is VaultState;
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

    public Blackboard IdleBoard => _idleBoard;
    public Blackboard ActiveBoard => IsPlayerControlled ? _playerCommandBoard : _idleBoard;
    public Animator Animator => _resolvedAnimator;
    public AnimatorSegmentPlayer SegmentPlayer => _resolvedSegmentPlayer;
    public CharacterConditions Conditions => _conditions;
    public ICharacterTransitionPolicy TransitionPolicy => _transitionPolicy;
    public ICharacterAttackResolver AttackResolver => _attackResolver;

    public void Initialize(CharacterDriver owner, CharacterContext context)
    {
        _context = context;
        RefreshBindings();
        ConfigureRuntimeRules();
        ConfigureStateMachine();
        ResetForOffField();
    }

    public void OnOwnerEnabled()
    {
    }

    public void OnOwnerDisabled()
    {
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

    public void ReceivePlayerControl(Blackboard playerBoard)
    {
        IsPlayerControlled = true;
        _playerCommandBoard.CopyFrom(playerBoard);
    }

    public void ReleasePlayerControl()
    {
        IsPlayerControlled = false;
        _playerCommandBoard.ClearAllData();
    }

    public void ForceEnterDeathState()
    {
        ReleasePlayerControl();
        _idleBoard.ClearAllData();
        if (_context != null)
            _context.Board = _idleBoard;
        if (_hsm == null)
            return;
        _hsm.SwitchState<DeathState>(InterruptPriority.Death, true);
    }

    public void ResetForOffField()
    {
        ReleasePlayerControl();
        _playerCommandBoard.ClearAllData();
        _idleBoard.ClearAllData();
        if (_context == null)
            return;

        _context.Board = _idleBoard;
        _context.DeltaTime = 0f;
    }

    public void ResetFrameDataAfterAutoTick()
    {
        if (IsPlayerControlled)
            _playerCommandBoard.ResetFrameData();
    }

    public void ResetBehaviorRuntime()
    {
        _interpreter?.Stop();
        if (_context == null)
            return;

        _context.PendingBehaviorTransitionDuration = -1f;
        _context.LastTransitionRejectReason = string.Empty;
        _context.LastRequestedBehaviorKey = string.Empty;
        _context.LastRequestedBehaviorClipName = string.Empty;
        _context.CurrentBehaviorKey = string.Empty;
    }

    public void ResetAnimationRuntime()
    {
        _resolvedSegmentPlayer?.ResetAnimatorState();
    }

    public void ResetToIdle()
    {
        if (_hsm == null)
            return;

        if (_hsm.Current is IdleState idleState)
        {
            idleState.OnEnter();
            return;
        }

        _hsm.SwitchState<IdleState>(InterruptPriority.None);
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        _hsm?.Tick();
    }

    public void LateTick(Blackboard board, float deltaTime)
    {
    }

    public void EnterInitialState(bool startOnAwake)
    {
        if (startOnAwake && _hsm != null)
            _hsm.SwitchState<IdleState>(InterruptPriority.None);
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
        _owner.RequestDebugRefresh(true);
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

    private static Animator ResolveAnimator(CharacterDriver owner)
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

    private static void DisableResidualPreviewDirectors(CharacterDriver owner)
    {
        UnityEngine.Playables.PlayableDirector[] directors = owner.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
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
