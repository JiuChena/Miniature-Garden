using System.Collections.Generic;
using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// Character 渚ц繍琛屾椂涓帶銆傝礋璐ｇ粺涓€鎸佹湁骞惰皟搴﹁鑹叉ā鍧楋紝瀵瑰鎻愪緵鍞竴璁块棶鍏ュ彛銆?/// </summary>
[RequireComponent(typeof(StatusData))]
[RequireComponent(typeof(UnitEffectController))]
[RequireComponent(typeof(BehaviorInterpreter))]
[RequireComponent(typeof(CharacterDebugModule))]
public class CharacterDriver : UnitDriverBase, IUnitBehaviorRequester, IUnitAbilityLevelProvider
{
    [Header("Config")]
    [SerializeField, Tooltip("当前角色使用的单位配置资源。类型为通用 UnitAssetInformation，可同时服务角色和敌人。")]
    private UnitAssetInformation config;

    [SerializeField, Tooltip("开启后在 Awake 中立即初始化，并切入初始待机状态。")]
    private bool startOnAwake = true;

    [SerializeField, Tooltip("开启后由 CharacterDriver 在 LateUpdate 中推进角色状态机。")]
    private bool autoTick = true;

    public bool IsInitialized { get; private set; }
    public bool IsDebugOverlayEnabled => _debugModule != null && _debugModule.IsOverlayEnabled;
    public bool IsPlayerControlled => _behaviorRuntime != null && _behaviorRuntime.IsPlayerControlled;
    public bool IsInIdleState => _behaviorRuntime != null && _behaviorRuntime.IsInIdleState;
    public bool IsInMoveState => _behaviorRuntime != null && _behaviorRuntime.IsInMoveState;
    public bool IsInVaultState => _behaviorRuntime != null && _behaviorRuntime.IsInVaultState;
    public bool IsInDeathState => _behaviorRuntime != null && _behaviorRuntime.IsInDeathState;
    public bool UsesDirectPoseInheritanceOnSwitch => _behaviorRuntime != null && _behaviorRuntime.UsesDirectPoseInheritanceOnSwitch;
    public bool RequiresStayOnSwitch => _behaviorRuntime != null && _behaviorRuntime.RequiresStayOnSwitch;
    public bool CanSwitchOut => IsInitialized && !IsInVaultState;
    public bool CanBeHiddenAfterSwitch => IsInitialized && !IsPlayerControlled && !RequiresStayOnSwitch;
    public CharacterContext Context { get; private set; }
    public UnitAssetInformation Config => config;
    public IUnitRuntimeDefinition RuntimeConfig => config;
    public IUnitDefinition RuntimeUnitDefinition => config;
    public StatusData DataPanel => statusData;
    public CharacterController LocalCharacterController
    {
        get
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();

            return _characterController;
        }
    }
    public override IUnitResourceSet Resources => Context != null ? Context.Resources : null;

    private readonly CharacterBehaviorEventReceiver _eventReceiver = new CharacterBehaviorEventReceiver();
    private readonly List<ICharacterModule> _runtimeModules = new List<ICharacterModule>(3);
    private CharacterBehaviorRuntime _behaviorRuntime;
    private PlayerController _controllingPlayer;
    private CharacterDataRuntime _dataRuntime;
    private CharacterDebugModule _debugModule;
    private CharacterController _characterController;
    private BehaviorInterpreter _interpreter;

    private void Reset()
    {
        EnsureRuntimeReferences();
        RebuildRuntimeList();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureRuntimeReferences();
        RebuildRuntimeList();
    }

    private void OnEnable()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].OnOwnerEnabled();

        if (!IsInitialized)
            return;

        RefreshRuntimeCharacterData(true);
        RequestDebugRefresh(true);
    }

    private void OnDisable()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].OnOwnerDisabled();
    }

    private void OnDestroy()
    {
        if (config != null && config.UnitId > 0)
            VFXPool.Instance.ClearOwner(config.UnitId);

        Context = null;
        IsInitialized = false;
        DisposeRuntimeModules();
    }

    protected override void Awake()
    {
        base.Awake();
        _characterController = GetComponent<CharacterController>();
        _interpreter = GetComponent<BehaviorInterpreter>();
        EnsureRuntimeReferences();
        RebuildRuntimeList();
        Initialize();
    }

    protected override void OnUnitTargetingProviderChanged()
    {
        SyncResolvedBindings();
    }

    private void LateUpdate()
    {
        if (!IsInitialized || !autoTick)
            return;

        Blackboard activeBoard = _behaviorRuntime != null ? _behaviorRuntime.ActiveBoard : null;
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].LateTick(activeBoard, Time.deltaTime);

        Tick(activeBoard, Time.deltaTime);
        _behaviorRuntime?.ResetFrameDataAfterAutoTick();
    }

    [ContextMenu("Auto Find References")]
    private void AutoFindReferences()
    {
        RefreshCoreBindings();
        Debug.Log("CharacterDriver 已尝试自动查找 Animator、AnimatorSegmentPlayer 与移动组件。", this);
    }

    public void RefreshMovementStrategyBinding()
    {
        RefreshCoreBindings();
    }

    [ContextMenu("Log Setup Summary")]
    private void LogSetupSummary()
    {
        IMovementStrategy movementStrategy = ResolveRuntimeMovementStrategy();
        string summary =
            $"Config: {(config != null ? config.name : "NULL")}\n" +
            $"Animator: {(_behaviorRuntime != null && _behaviorRuntime.Animator != null ? _behaviorRuntime.Animator.name : "NULL")}\n" +
            $"AnimatorSegmentPlayer: {(_behaviorRuntime != null && _behaviorRuntime.SegmentPlayer != null ? _behaviorRuntime.SegmentPlayer.name : "NULL")}\n" +
            $"MovementStrategy: {(movementStrategy != null ? movementStrategy.GetType().Name : "NULL")}\n" +
            $"AutoTick: {autoTick}\n" +
            $"IsPlayerControlled: {IsPlayerControlled}\n" +
            $"Initialized: {IsInitialized}";

        Debug.Log(summary, this);
    }

    public void ReceivePlayerControl(PlayerController playerController, Blackboard playerBoard)
    {
        if (!IsInitialized || playerController == null)
            return;

        if (statusData != null && statusData.IsDead)
            return;

        _controllingPlayer = playerController;
        _behaviorRuntime?.ReceivePlayerControl(playerBoard);
        ApplyPlayerMovementConfig();
        SyncResolvedBindings();
        RefreshInterpreterBindings();
        RequestDebugRefresh(true);
    }

    public void ReleasePlayerControl()
    {
        _behaviorRuntime?.ReleasePlayerControl();
        _controllingPlayer = null;
        SyncResolvedBindings();
        RefreshInterpreterBindings();
        RequestDebugRefresh(true);
    }

    public void PrepareForReactivationFromOffField()
    {
        if (!IsInitialized || Context == null)
            return;

        _behaviorRuntime?.ResetForOffField();
        _behaviorRuntime?.ResetBehaviorRuntime();
        _behaviorRuntime?.ResetAnimationRuntime();
        _behaviorRuntime?.ResetToIdle();
        RequestDebugRefresh(true);
    }

    public void ForceEnterDeathState()
    {
        if (!IsInitialized || statusData == null || !statusData.IsDead)
            return;

        ReleasePlayerControl();
        _behaviorRuntime?.ForceEnterDeathState();
        RequestDebugRefresh(true);
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        if (!IsInitialized || Context == null)
            return;

        if (statusData != null && statusData.IsDead && !_behaviorRuntime.IsInDeathState)
            _behaviorRuntime.ForceEnterDeathState();

        SyncResolvedBindings();
        Context.Board = board ?? (_behaviorRuntime != null ? _behaviorRuntime.IdleBoard : null);
        Context.DeltaTime = deltaTime;
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].Tick(Context.Board, deltaTime);
        RequestDebugRefresh();
    }

    public void Initialize()
    {
        if (config == null)
        {
            Debug.LogWarning("CharacterDriver 缺少 UnitAssetInformation，初始化已跳过。", this);
            Context = null;
            IsInitialized = false;
            return;
        }

        EnsureRuntimeReferences();
        RebuildRuntimeList();
        RefreshCoreBindings();
        Context = BuildContext();
        InitializeRuntimeModules();

        EffectController?.ClearAllRuntimeEffects();
        RefreshStatusData(false, true);
        _behaviorRuntime?.ResetForOffField();

        IBehaviorAnimationPlayer animationPlayer = _behaviorRuntime != null ? _behaviorRuntime.SegmentPlayer : null;
        animationPlayer?.Initialize(Context.Animator);
        RefreshInterpreterBindings();

        IsInitialized = true;
        _behaviorRuntime?.EnterInitialState(startOnAwake);
        RequestDebugRefresh(true);
    }

    public bool RequestBehavior(BehaviorClip clip)
    {
        return _behaviorRuntime != null && _behaviorRuntime.RequestBehavior(clip);
    }

    public bool RequestBehavior(string key, int clipIndex = 0)
    {
        return _behaviorRuntime != null && _behaviorRuntime.RequestBehavior(key, clipIndex);
    }

    public BehaviorClip GetBehavior(string key, int clipIndex = 0)
    {
        return _behaviorRuntime != null ? _behaviorRuntime.GetBehavior(key, clipIndex) : null;
    }

    public BehaviorClip[] GetBehaviorGroup(string key)
    {
        return _behaviorRuntime != null ? _behaviorRuntime.GetBehaviorGroup(key) : System.Array.Empty<BehaviorClip>();
    }

    public int GetAbilityLevel(UnitAbilityLevelGroup levelGroup)
    {
        return _dataRuntime != null ? _dataRuntime.GetAbilityLevel(levelGroup) : 1;
    }

    public void RefreshRuntimeCharacterData(bool preserveHealthRatio = true)
    {
        if (!IsInitialized || config == null || statusData == null)
            return;

        _dataRuntime?.RefreshRuntimeCharacterData(preserveHealthRatio);
    }

    public override bool TryResolveNumericValue(string numericKey, out float value)
    {
        if (_dataRuntime != null)
            return _dataRuntime.TryResolveNumericValue(numericKey, out value);

        value = 0f;
        return false;
    }

    public override bool TryBuildStatusSnapshot(out StatusDataSnapshot snapshot)
    {
        if (_dataRuntime != null)
            return _dataRuntime.TryBuildStatusSnapshot(out snapshot);

        snapshot = default;
        return false;
    }

    private void OnGUI()
    {
        _debugModule?.DrawOverlay();
    }

    public string GetCurrentStateName()
    {
        return _behaviorRuntime != null ? _behaviorRuntime.CurrentStateName : string.Empty;
    }

    public CharacterData GetCurrentRuntimeCharacterData()
    {
        return _dataRuntime != null
            ? _dataRuntime.GetRuntimeCharacterData()
            : new CharacterData();
    }

    public bool IsDeathBehaviorPlaybackFinished()
    {
        if (!IsInitialized || statusData == null || !statusData.IsDead)
            return false;

        if (!IsInDeathState)
            return false;

        if (_interpreter == null)
            return true;

        return !_interpreter.IsPlaying;
    }

    private void DisposeRuntimeModules()
    {
        for (int i = _runtimeModules.Count - 1; i >= 0; i--)
            _runtimeModules[i].Dispose();
    }

    internal void RequestDebugRefresh(bool force = false)
    {
        _debugModule?.Capture(force);
    }

    private void EnsureRuntimeReferences()
    {
        _debugModule = GetOrAddModule<CharacterDebugModule>();
        _behaviorRuntime = new CharacterBehaviorRuntime(this, _interpreter);
        _dataRuntime ??= new CharacterDataRuntime(this);
    }

    private void RebuildRuntimeList()
    {
        _runtimeModules.Clear();
        TryAddRuntimeModule(_behaviorRuntime);
        TryAddRuntimeModule(_dataRuntime);
        TryAddRuntimeModule(_debugModule);
    }

    private T GetOrAddModule<T>() where T : Component
    {
        if (!TryGetComponent(out T module))
            module = gameObject.AddComponent<T>();

        return module;
    }

    private void TryAddRuntimeModule(ICharacterModule module)
    {
        if (module != null)
            _runtimeModules.Add(module);
    }

    private void RefreshCoreBindings()
    {
        _behaviorRuntime?.RefreshBindings();
        SyncResolvedBindings();
        RefreshInterpreterBindings();
    }

    private CharacterContext BuildContext()
    {
        return new CharacterContext
        {
            Config = config,
            Cooldowns = new CharacterCooldowns(),
            Resources = new CharacterResources(RuntimeConfig != null ? RuntimeConfig.MaxEnergy : 0f),
            Interpreter = _interpreter,
            Data = DataPanel,
            Animator = _behaviorRuntime != null ? _behaviorRuntime.Animator : null,
            Controller = ResolveRuntimeController(),
            Transform = ResolveRuntimeTransform(),
            Board = _behaviorRuntime != null ? _behaviorRuntime.IdleBoard : null,
            MovementStrategy = ResolveRuntimeMovementStrategy(),
            BehaviorRequester = this,
            AbilityLevelProvider = this,
            UnitTargetingProvider = UnitTargetingProvider,
            InteractionSource = ResolveInteractionSource(),
            CurrentStance = CharacterStance.Standing,
        };
    }

    private void InitializeRuntimeModules()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].Initialize(this, Context);

        SyncResolvedBindings();
    }

    private void SyncResolvedBindings()
    {
        if (Context == null)
            return;

        Context.Animator = _behaviorRuntime != null ? _behaviorRuntime.Animator : null;
        Context.Controller = ResolveRuntimeController();
        Context.Transform = ResolveRuntimeTransform();
        Context.MovementStrategy = ResolveRuntimeMovementStrategy();
        Context.InteractionSource = ResolveInteractionSource();
        Context.UnitTargetingProvider = UnitTargetingProvider;
    }

    private CharacterController ResolveRuntimeController()
    {
        PlayerMovementModule movementModule = _controllingPlayer != null ? _controllingPlayer.MovementModule : null;
        if (IsPlayerControlled && movementModule != null && movementModule.Controller != null)
            return movementModule.Controller;

        return _characterController;
    }

    private Transform ResolveRuntimeTransform()
    {
        if (IsPlayerControlled && _controllingPlayer != null)
            return _controllingPlayer.transform;

        return transform;
    }

    private IMovementStrategy ResolveRuntimeMovementStrategy()
    {
        PlayerMovementModule movementModule = _controllingPlayer != null ? _controllingPlayer.MovementModule : null;
        if (IsPlayerControlled && movementModule != null)
            return movementModule.MovementStrategy;

        return null;
    }

    private ICharacterInteractionSource ResolveInteractionSource()
    {
        if (IsPlayerControlled && _controllingPlayer != null)
            return _controllingPlayer.CharacterInteractionSource;

        return null;
    }

    private void ApplyPlayerMovementConfig()
    {
        if (config == null || _controllingPlayer == null || _controllingPlayer.MovementModule == null)
            return;

        _controllingPlayer.MovementModule.ApplyUnitConfig(config);
        if (_characterController != null)
            _controllingPlayer.MovementModule.ApplyCharacterControllerTemplate(_characterController);
    }

    private void RefreshInterpreterBindings()
    {
        if (_interpreter == null)
            return;

        Animator animator = Context != null ? Context.Animator : null;
        IBehaviorAnimationPlayer animationPlayer = _behaviorRuntime != null ? _behaviorRuntime.SegmentPlayer : null;
        CharacterController runtimeController = Context != null ? Context.Controller : _characterController;

        _interpreter.Configure(animator, animationPlayer, runtimeController, DataPanel,
            _eventReceiver, RuntimeConfig != null ? RuntimeConfig.HitboxTargetLayers : ~0);
    }
}
