using UnityEngine;
using CoreFramework;
using System.Collections.Generic;
using UnityEngine.Serialization;
using BehaviorCore;

/// <summary>
/// 玩家侧运行时中控。负责统一持有并调度玩家相关模块，对外提供唯一访问入口。
/// </summary>
[RequireComponent(typeof(PlayerInputModule))]
[RequireComponent(typeof(PlayerPartyModule))]
[RequireComponent(typeof(PlayerCameraModule))]
[RequireComponent(typeof(PlayerSwitchPlacementModule))]
[RequireComponent(typeof(PlayerMovementModule))]
[RequireComponent(typeof(PlayerTargetingModule))]
[RequireComponent(typeof(InteractionReceiver))]
public class PlayerController : MonoBehaviour, IUnitCombatProxyProvider
{
    [Header("Legacy Migration")]
    [FormerlySerializedAs("controllableCharacters")]
    [SerializeField, HideInInspector]
    private CharacterDriver[] legacyControllableCharacters = System.Array.Empty<CharacterDriver>();

    [FormerlySerializedAs("syncCameraToCurrentCharacter")]
    [SerializeField, HideInInspector]
    private bool legacySyncCameraToCurrentCharacter = true;

    /// <summary>全局单例</summary>
    public static PlayerController Instance { get; private set; }
    /// <summary>玩家输入黑板，每帧 Update 被写入原始输入</summary>
    public Blackboard Board => _board;
    /// <summary>当前玩家控制的角色驱动</summary>
    public CharacterDriver CurrentCharacter => _partyModule != null ? _partyModule.CurrentCharacter : null;
    /// <summary>当前角色在列表中的索引</summary>
    public int CurrentCharacterIndex => _partyModule != null ? _partyModule.CurrentCharacterIndex : -1;
    public IReadOnlyList<CharacterDriver> ConfiguredCharacters =>
        _partyModule != null ? _partyModule.ConfiguredCharacters : System.Array.Empty<CharacterDriver>();
    public int ConfiguredCharacterCount => _partyModule != null ? _partyModule.ConfiguredCharacterCount : 0;
    /// <summary>切人占位模块</summary>
    public PlayerSwitchPlacementModule SwitchPlacementModule => _switchPlacementModule;
    /// <summary>单位索敌提供器的中性命名访问口</summary>
    public IUnitTargetingProvider UnitTargetingProvider => _unitTargetingProvider;
    public PlayerMovementModule MovementModule => _movementModule;
    public ICharacterInteractionSource CharacterInteractionSource => _inputModule;
    public InteractionReceiver InteractionReceiver => _interactionReceiver;

    /// <summary>玩家输入黑板实例</summary>
    private Blackboard _board;
    /// <summary>玩家运行时上下文</summary>
    private PlayerContext _context;
    /// <summary>按初始化顺序排列的模块列表</summary>
    private readonly List<IPlayerModule> _modules = new List<IPlayerModule>(8);
    /// <summary>输入模块引用</summary>
    private PlayerInputModule _inputModule;
    /// <summary>编队模块引用</summary>
    private PlayerPartyModule _partyModule;
    /// <summary>相机模块引用</summary>
    private PlayerCameraModule _cameraModule;
    /// <summary>切人占位模块引用</summary>
    private PlayerSwitchPlacementModule _switchPlacementModule;
    private PlayerMovementModule _movementModule;
    private InteractionReceiver _interactionReceiver;
    /// <summary>玩家索敌模块引用（同时作为投射物索敌提供器）</summary>
    private PlayerTargetingModule _unitTargetingProvider;

    /// <summary>
    /// [Editor] 重置组件时自动补充缺失的 Module 并迁移旧序列化字段。
    /// </summary>
    private void Reset()
    {
        EnsureEditorContextExists();
        EnsureModuleReferences(true);
        MigrateLegacySerializedFields();
        RebuildModuleList();
    }

    /// <summary>
    /// [Editor] Inspector 值变更时自动补充缺失的 Module 并迁移旧序列化字段。
    /// </summary>
    private void OnValidate()
    {
        EnsureEditorContextExists();
        EnsureModuleReferences(false);
        MigrateLegacySerializedFields();
        RebuildModuleList();
    }

    /// <summary>
    /// 初始化单例引用、黑板、PlayerContext，补充 Module 引用并完成所有模块的 Initialize + Enable。
    /// </summary>
    private void Awake()
    {
        Instance = this;
        _board = new Blackboard();
        _context = new PlayerContext
        {
            Transform = transform,
            Board = _board,
        };
        EnsureModuleReferences(true);
        MigrateLegacySerializedFields();
        RebuildModuleList();
        InitializeModules();
    }

    /// <summary>
    /// 首帧把相机跟随目标固定到 Player 根节点。需在 Awake 完成初始化和角色选择之后执行。
    /// </summary>
    private void Start()
    {
        _cameraModule?.FollowPlayerRoot();
    }

    /// <summary>
    /// 每帧清空黑板帧数据，然后推进所有 PlayerModule 的 Tick，最后分发输入到当前角色。
    /// </summary>
    private void Update()
    {
        _board.ClearAllData();
        TickModules(Time.deltaTime);
    }

    /// <summary>
    /// 释放当前角色的玩家操控权，停用所有模块，清理静态单例引用。
    /// </summary>
    private void OnDestroy()
    {
        CurrentCharacter?.ReleasePlayerControl();
        DisableModules();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 切换到指定索引的可控角色。force=true 时允许同一角色重复切换。
    /// </summary>
    /// <param name="index">角色列表索引</param>
    /// <param name="force">是否强制切换，即使目标已经是当前角色</param>
    public bool SetCurrentCharacter(int index, bool force = false)
    {
        return _partyModule != null && _partyModule.SetCurrentCharacter(index, force);
    }

    /// <summary>
    /// 用当前持续输入（移动方向、按住攻击等）预推进一次当前角色的状态机，用于切人时让新角色立即响应已按下的按键。
    /// </summary>
    public void PrimeCurrentCharacterForCurrentInput()
    {
        _inputModule?.PrimeCurrentCharacterForCurrentInput();
    }

    /// <summary>
    /// 统一控制玩家玩法输入是否启用。禁用后会停止接收新的移动、攻击、技能、交互与切人输入。
    /// </summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        _inputModule?.SetGameplayInputEnabled(enabled);
    }

    /// <summary>
    /// 让玩家相机始终跟随 Player 根节点。
    /// </summary>
    public void FollowPlayerRoot()
    {
        _cameraModule?.FollowPlayerRoot();
    }

    /// <summary>
    /// 立即把当前受控角色对齐到 Player 根节点的当前世界位姿。
    /// </summary>
    public void SyncControlledCharacterToPlayerRoot()
    {
        _movementModule?.SyncControlledCharacterToPlayerRoot();
    }

    public StatusData ResolveCombatStatusData()
    {
        return CurrentCharacter != null ? CurrentCharacter.DataPanel : null;
    }

    public IBehaviorUnit ResolvebehaviorUnit()
    {
        return ResolveCombatStatusData();
    }

    public IDamageable ResolveDamageable()
    {
        return ResolveCombatStatusData();
    }

    public UnitEffectController ResolveEffectController()
    {
        return CurrentCharacter != null ? CurrentCharacter.EffectController : null;
    }

    /// <summary>
    /// 立即设置 Player 根节点位姿。当前受控角色会在位姿同步模块中跟随该结果。
    /// </summary>
    public void SetRootPose(Vector3 position, Quaternion rotation)
    {
        if (_movementModule != null)
        {
            _movementModule.SetRootPose(position, rotation);
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// 确保五个 Module 组件都存在，缺失则自动 Add。仅补引用，不触发初始化。
    /// </summary>
    private void EnsureModuleReferences(bool createMissing)
    {
        _inputModule = GetOrAddModule<PlayerInputModule>(createMissing);
        _partyModule = GetOrAddModule<PlayerPartyModule>(createMissing);
        _cameraModule = GetOrAddModule<PlayerCameraModule>(createMissing);
        _switchPlacementModule = GetOrAddModule<PlayerSwitchPlacementModule>(createMissing);
        _movementModule = GetOrAddModule<PlayerMovementModule>(createMissing);
        _interactionReceiver = GetOrAddModule<InteractionReceiver>(createMissing);
        _unitTargetingProvider = EnsureExactPlayerTargetingModule(createMissing);
        SyncContextBindings();
    }

    private PlayerTargetingModule EnsureExactPlayerTargetingModule(bool createMissing)
    {
        PlayerTargetingModule[] targetingModules = GetComponents<PlayerTargetingModule>();
        PlayerTargetingModule exactPlayerTargetingModule = null;
        for (int i = 0; i < targetingModules.Length; i++)
        {
            PlayerTargetingModule targetingModule = targetingModules[i];
            if (targetingModule == null)
                continue;

            if (targetingModule.GetType() == typeof(PlayerTargetingModule))
            {
                exactPlayerTargetingModule = targetingModule;
                continue;
            }

            RemoveLegacyPlayerTargetingShell(targetingModule);
        }

        if (exactPlayerTargetingModule == null && createMissing)
            exactPlayerTargetingModule = gameObject.AddComponent<PlayerTargetingModule>();

        return exactPlayerTargetingModule;
    }

    private void RemoveLegacyPlayerTargetingShell(PlayerTargetingModule targetingModule)
    {
        if (targetingModule == null)
            return;

        if (Application.isPlaying)
            Destroy(targetingModule);
        else
            DestroyImmediate(targetingModule);
    }

    private void EnsureEditorContextExists()
    {
        _context ??= new PlayerContext();
        _context.Transform = transform;
        if (_board == null)
            _board = new Blackboard();

        _context.Board = _board;
    }

    private void SyncContextBindings()
    {
        if (_context == null)
            return;

        _context.Transform = transform;
        _context.Board = _board;
        _context.Controller = _movementModule != null ? _movementModule.Controller : null;
        _context.MovementStrategy = _movementModule != null ? _movementModule.MovementStrategy : null;
        _context.InteractionSource = _inputModule;
    }

    /// <summary>
    /// 将旧版序列化字段（legacyControllableCharacters、legacySyncCameraToCurrentCharacter）的值迁移到 Module 配置中。迁移成功后旧字段不再生效。
    /// </summary>
    private void MigrateLegacySerializedFields()
    {
        if (_partyModule != null &&
            (legacyControllableCharacters != null && legacyControllableCharacters.Length > 0) &&
            !_partyModule.HasConfiguredCharacters)
        {
            _partyModule.SetConfiguredCharacters(legacyControllableCharacters, rebuildPartyState: false);
        }

        if (_cameraModule != null)
            _cameraModule.SetFollowSyncEnabled(legacySyncCameraToCurrentCharacter);
    }

    /// <summary>
    /// 按顺序重建模块列表，用于 InitializeModules 和 TickModules 遍历。新增模块时需在此注册。
    /// </summary>
    private void RebuildModuleList()
    {
        _modules.Clear();
        if (_inputModule != null)
            _modules.Add(_inputModule);
        if (_partyModule != null)
            _modules.Add(_partyModule);
        if (_cameraModule != null)
            _modules.Add(_cameraModule);
        if (_switchPlacementModule != null)
            _modules.Add(_switchPlacementModule);
        if (_movementModule != null)
            _modules.Add(_movementModule);
    }

    /// <summary>
    /// 获取指定类型的 Module 组件，不存在则 Add 后返回。
    /// </summary>
    private T GetOrAddModule<T>(bool createMissing) where T : Component
    {
        if (!TryGetComponent(out T module) && createMissing)
            module = gameObject.AddComponent<T>();

        return module;
    }

    /// <summary>
    /// 按模块列表顺序依次 Initialize 并 Enable 所有 PlayerModule。
    /// </summary>
    private void InitializeModules()
    {
        for (int i = 0; i < _modules.Count; i++)
        {
            _modules[i].Initialize(this, _context);
            _modules[i].Enable();
        }
    }

    /// <summary>
    /// 每帧按模块列表顺序 Tick 所有 PlayerModule，最后分发当前输入到当前角色。
    /// </summary>
    private void TickModules(float deltaTime)
    {
        for (int i = 0; i < _modules.Count; i++)
            _modules[i].Tick(_board, deltaTime);

        _inputModule?.DispatchCurrentCharacterControl(_board);
    }

    /// <summary>
    /// 按模块列表顺序依次 Disable 所有 PlayerModule。
    /// </summary>
    private void DisableModules()
    {
        for (int i = 0; i < _modules.Count; i++)
            _modules[i].Disable();
    }
}
