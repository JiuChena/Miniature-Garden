using System.Collections.Generic;
using CoreFramework;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// 鏁屼汉渚ц繍琛屾椂涓帶銆?/// 澶嶇敤鐜版湁琛屼负缂栬緫鍣ㄤ笌鐘舵€佹満锛屾柊澧?AI 鍐崇瓥鍜屽鑸ā鍧椾綔涓烘晫浜鸿緭鍏ユ潵婧愩€?/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StatusData))]
[RequireComponent(typeof(UnitEffectController))]
[RequireComponent(typeof(BehaviorInterpreter))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NonPlayerTargetingModule))]
[RequireComponent(typeof(EnemyBrainModule))]
[RequireComponent(typeof(EnemyNavigationModule))]
public class EnemyDriver : UnitDriverBase, IUnitBehaviorRequester, IUnitAbilityLevelProvider,
    ICharacterInteractionSource, ICharacterInteractionVolumeReceiver
{
    [Header("Config")]
    [SerializeField, Tooltip("敌人当前使用的通用单位配置资源，包含行为、数值和能力定义。")]
    private UnitAssetInformation config;

    [SerializeField, Tooltip("开启后在 Awake 中立即初始化，并切入 Load 或 Idle。")]
    private bool startOnAwake = true;

    [SerializeField, Tooltip("开启后由 EnemyDriver 在 Update 中推进 AI 与行为状态机。")]
    private bool autoTick = true;

    [Header("Levels")]
    [SerializeField, Min(1), Tooltip("敌人的单位等级，用于基础生命、攻击和防御成长公式。")]
    private int unitLevel = 1;

    [SerializeField, Min(1), Tooltip("敌人的普通攻击等级，用于数值表中 NormalAttack 组的解析。")]
    private int normalAttackLevel = 1;

    [SerializeField, Min(1), Tooltip("敌人的天赋技能等级，用于数值表中 Talent 组的解析。")]
    private int talentLevel = 1;

    [SerializeField, Min(1), Tooltip("敌人的爆发技能等级，用于数值表中 Burst 组的解析。")]
    private int burstLevel = 1;

    private readonly CharacterBehaviorEventReceiver _eventReceiver = new CharacterBehaviorEventReceiver();
    private readonly List<IEnemyModule> _runtimeModules = new List<IEnemyModule>(2);
    private readonly List<ICharacterInteractionVolume> _activeInteractionVolumes =
        new List<ICharacterInteractionVolume>(4);
    private readonly Blackboard _board = new Blackboard();

    private EnemyBehaviorRuntime _behaviorRuntime;
    private EnemyBrainModule _brainModule;
    private EnemyNavigationModule _navigationModule;
    private NonPlayerTargetingModule _nonPlayerTargetingProvider;
    private BehaviorInterpreter _interpreter;
    private CharacterController _characterController;
    private int _runtimeUnitId;

    public bool IsInitialized { get; private set; }
    public CharacterContext Context { get; private set; }
    public UnitAssetInformation Config => config;
    public EnemyBrainModule BrainModule => _brainModule;
    public EnemyNavigationModule NavigationModule => _navigationModule;
    public NonPlayerTargetingModule NonPlayerTargetingProvider => _nonPlayerTargetingProvider;
    public override IUnitResourceSet Resources => Context != null ? Context.Resources : null;
    public bool IsInIdleState => _behaviorRuntime != null && _behaviorRuntime.IsInIdleState;
    public bool IsInMoveState => _behaviorRuntime != null && _behaviorRuntime.IsInMoveState;
    public bool IsInVaultState => _behaviorRuntime != null && _behaviorRuntime.IsInVaultState;
    public bool RequiresNavigationSuppression => _behaviorRuntime != null && _behaviorRuntime.RequiresNavigationSuppression;
    public string CurrentStateName => _behaviorRuntime != null ? _behaviorRuntime.CurrentStateName : string.Empty;

    private void Reset()
    {
        EnsureRuntimeReferences();
        RebuildRuntimeList();
    }

    protected override void Awake()
    {
        base.Awake();
        _runtimeUnitId = GetInstanceID();
        _interpreter = GetComponent<BehaviorInterpreter>();
        _characterController = GetComponent<CharacterController>();
        EnsureRuntimeReferences();
        RebuildRuntimeList();
        Initialize();
    }

    private void OnEnable()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].OnOwnerEnabled();
    }

    private void OnDisable()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].OnOwnerDisabled();
    }

    private void OnDestroy()
    {
        if (_runtimeUnitId != 0)
            VFXPool.Instance.ClearOwner(_runtimeUnitId);

        DisposeRuntimeModules();
        Context = null;
        IsInitialized = false;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureRuntimeReferences();
        RebuildRuntimeList();
        unitLevel = Mathf.Max(1, unitLevel);
        normalAttackLevel = Mathf.Max(1, normalAttackLevel);
        talentLevel = Mathf.Max(1, talentLevel);
        burstLevel = Mathf.Max(1, burstLevel);
    }

    protected override void OnUnitTargetingProviderChanged()
    {
        SyncResolvedBindings();
    }

    private void Update()
    {
        if (!IsInitialized || !autoTick)
            return;

        TickRuntime(Time.deltaTime);
    }

    private void TickRuntime(float deltaTime)
    {
        _board.ClearAllData();

        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].Tick(_board, deltaTime);

        if (Context != null)
        {
            Context.Board = _board;
            Context.DeltaTime = deltaTime;
        }

        _behaviorRuntime?.Tick(deltaTime);
    }

    private void LateUpdate()
    {
        if (!IsInitialized || !autoTick)
            return;

        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].LateTick(_board, Time.deltaTime);
    }

    public void Initialize()
    {
        if (config == null)
        {
            Debug.LogWarning("EnemyDriver 缺少 UnitAssetInformation，初始化已跳过。", this);
            Context = null;
            IsInitialized = false;
            return;
        }

        EnsureRuntimeReferences();
        RebuildRuntimeList();
        RefreshCoreBindings();
        Context = BuildContext();
        _behaviorRuntime.Initialize(Context);
        InitializeRuntimeModules();

        EffectController?.ClearAllRuntimeEffects();
        RefreshStatusData(false, true);

        IBehaviorAnimationPlayer animationPlayer = _behaviorRuntime.SegmentPlayer;
        animationPlayer?.Initialize(Context.Animator);
        RefreshInterpreterBindings();

        IsInitialized = true;
        _behaviorRuntime.EnterInitialState(startOnAwake);
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
        switch (levelGroup)
        {
            case UnitAbilityLevelGroup.NormalAttack:
                return Mathf.Max(1, normalAttackLevel);
            case UnitAbilityLevelGroup.Talent:
                return Mathf.Max(1, talentLevel);
            case UnitAbilityLevelGroup.Burst:
                return Mathf.Max(1, burstLevel);
            default:
                return 1;
        }
    }

    public override bool TryResolveNumericValue(string numericKey, out float value)
    {
        value = 0f;
        if (config == null || string.IsNullOrWhiteSpace(numericKey))
            return false;

        return config.TryResolveNumericValue(numericKey, this, out value);
    }

    public override bool TryBuildStatusSnapshot(out StatusDataSnapshot snapshot)
    {
        snapshot = default;
        if (config == null)
            return false;

        int safeLevel = Mathf.Max(1, unitLevel);
        snapshot.hasFullStatus = true;
        snapshot.unitId = _runtimeUnitId;
        snapshot.unitAlignment = config.UnitAlignment;
        snapshot.unitLevel = safeLevel;
        snapshot.baseHealth = config.ResolveBaseHealth(safeLevel);
        snapshot.baseAttackPower = config.ResolveBaseAttack(safeLevel);
        snapshot.baseDefense = config.ResolveBaseDefense(safeLevel);
        snapshot.baseCritRate = config.BaseCritRate;
        snapshot.baseCritDamage = config.BaseCritDamage;
        snapshot.baseDamageBonus = config.BaseDamageBonus;
        snapshot.basePenetration = config.BasePenetration;
        return true;
    }

    public void RegisterInteractionVolume(ICharacterInteractionVolume volume)
    {
        if (volume == null || _activeInteractionVolumes.Contains(volume))
            return;

        _activeInteractionVolumes.Add(volume);
    }

    public void UnregisterInteractionVolume(ICharacterInteractionVolume volume)
    {
        if (volume == null)
            return;

        _activeInteractionVolumes.Remove(volume);
    }

    public bool IsInCoverInteractionRange(CharacterContext context)
    {
        CleanupInvalidInteractionVolumes();
        for (int i = _activeInteractionVolumes.Count - 1; i >= 0; i--)
        {
            ICharacterInteractionVolume volume = _activeInteractionVolumes[i];
            if (volume != null && volume.AllowsCover)
                return true;
        }

        return false;
    }

    public bool TryGetVaultRequest(CharacterContext context, out CharacterVaultRequest request)
    {
        request = default;
        CleanupInvalidInteractionVolumes();
        for (int i = _activeInteractionVolumes.Count - 1; i >= 0; i--)
        {
            ICharacterInteractionVolume volume = _activeInteractionVolumes[i];
            if (volume == null || !volume.AllowsVault)
                continue;

            return volume.TryBuildVaultRequest(context, out request);
        }

        return false;
    }

    public bool TryGetVaultRequestForDirection(CharacterContext context, Vector3 desiredDirection,
        float maxApproachAngleDegrees, out CharacterVaultRequest request)
    {
        request = default;
        CleanupInvalidInteractionVolumes();
        for (int i = _activeInteractionVolumes.Count - 1; i >= 0; i--)
        {
            ICharacterInteractionVolume volume = _activeInteractionVolumes[i];
            if (volume == null || !volume.AllowsVault)
                continue;

            if (volume is CharacterInteractionVolume traversalVolume)
            {
                if (traversalVolume.TryBuildVaultRequestForApproach(
                        context, desiredDirection, maxApproachAngleDegrees, out request))
                {
                    return true;
                }

                continue;
            }

            if (volume.TryBuildVaultRequest(context, out request))
                return true;
        }

        return false;
    }

    private void EnsureRuntimeReferences()
    {
        _interpreter ??= GetComponent<BehaviorInterpreter>();
        _characterController ??= GetComponent<CharacterController>();
        _brainModule = GetOrAddModule<EnemyBrainModule>();
        _navigationModule = GetOrAddModule<EnemyNavigationModule>();
        _nonPlayerTargetingProvider = GetOrAddModule<NonPlayerTargetingModule>();
        _behaviorRuntime ??= new EnemyBehaviorRuntime(this, _interpreter);
    }

    private void RebuildRuntimeList()
    {
        _runtimeModules.Clear();
        TryAddRuntimeModule(_brainModule);
        TryAddRuntimeModule(_navigationModule);
    }

    private T GetOrAddModule<T>() where T : Component
    {
        if (!TryGetComponent(out T module))
            module = gameObject.AddComponent<T>();

        return module;
    }

    private void TryAddRuntimeModule(IEnemyModule module)
    {
        if (module != null)
            _runtimeModules.Add(module);
    }

    private void InitializeRuntimeModules()
    {
        for (int i = 0; i < _runtimeModules.Count; i++)
            _runtimeModules[i].Initialize(this, Context);

        SyncResolvedBindings();
    }

    private void DisposeRuntimeModules()
    {
        for (int i = _runtimeModules.Count - 1; i >= 0; i--)
            _runtimeModules[i].Dispose();

        _behaviorRuntime?.Dispose();
    }

    private void RefreshCoreBindings()
    {
        if (_nonPlayerTargetingProvider != null)
            SetUnitTargetingProviderOverride(_nonPlayerTargetingProvider);

        _behaviorRuntime.RefreshBindings();
        SyncResolvedBindings();
        RefreshInterpreterBindings();
    }

    private CharacterContext BuildContext()
    {
        return new CharacterContext
        {
            Config = config,
            Cooldowns = new CharacterCooldowns(),
            Resources = new CharacterResources(config != null ? config.MaxEnergy : 0f),
            Interpreter = _interpreter,
            Data = StatusData,
            Animator = _behaviorRuntime.Animator,
            Controller = _characterController,
            Transform = transform,
            Board = _board,
            MovementStrategy = null,
            BehaviorRequester = this,
            AbilityLevelProvider = this,
            UnitTargetingProvider = UnitTargetingProvider,
            InteractionSource = this,
            CurrentStance = CharacterStance.Standing,
            EnableAutomaticProjectileFacing = false,
        };
    }

    private void SyncResolvedBindings()
    {
        if (Context == null)
            return;

        Context.Animator = _behaviorRuntime != null ? _behaviorRuntime.Animator : null;
        Context.Controller = _characterController;
        Context.Transform = transform;
        Context.MovementStrategy = null;
        Context.InteractionSource = this;
        Context.UnitTargetingProvider = UnitTargetingProvider;
        Context.EnableAutomaticProjectileFacing = false;
    }

    private void RefreshInterpreterBindings()
    {
        if (_interpreter == null)
            return;

        Animator animator = Context != null ? Context.Animator : null;
        IBehaviorAnimationPlayer animationPlayer = _behaviorRuntime != null ? _behaviorRuntime.SegmentPlayer : null;
        CharacterController runtimeController = _characterController;

        _interpreter.Configure(animator, animationPlayer, runtimeController, StatusData,
            _eventReceiver, config != null ? config.HitboxTargetLayers : ~0);
    }

    private void CleanupInvalidInteractionVolumes()
    {
        for (int i = _activeInteractionVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeInteractionVolumes[i] == null)
                _activeInteractionVolumes.RemoveAt(i);
        }
    }
}
