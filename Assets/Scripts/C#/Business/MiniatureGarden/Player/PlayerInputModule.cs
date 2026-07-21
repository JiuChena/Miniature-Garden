using CoreFramework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家输入模块：全权负责读取 Unity Input System 并写入玩家黑板。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInputModule : MonoBehaviour, IPlayerModule, IInputProvider, ICharacterInteractionSource,
    ICharacterInteractionVolumeReceiver
{
    private const string DefaultInputActionAssetPath = "Assets/Settings/Player Input/PlayerInputActions.inputactions";
    private const string PlayerActionMapName = "Player";

    private const string MoveActionName = "Move";
    private const string LookActionName = "Look";
    private const string FireActionName = "Fire";
    private const string AimActionName = "Aim";
    private const string SprintActionName = "Sprint";
    private const string JumpActionName = "Jump";
    private const string CrouchActionName = "Crouch";
    private const string TalentActionName = "Talent";
    private const string BurstActionName = "Burst";
    private const string ReloadActionName = "Reload";
    private const string InteractActionName = "Interact";
    private const string Switch1ActionName = "Switch1";
    private const string Switch2ActionName = "Switch2";
    private const string Switch3ActionName = "Switch3";
    private const string Switch4ActionName = "Switch4";

    [Header("Input")]
    [SerializeField, Tooltip("玩家输入 Action Asset。留空时编辑器下会自动回填默认项目输入资源。")]
    private InputActionAsset inputActionsAsset;

    [SerializeField, Tooltip("开启后在 Console 中每帧打印处理后的输入数据")]
    private bool debugMode;

    [SerializeField, Tooltip("启用输入后先屏蔽多少帧的单次输入，避免开局误触发技能")]
    [Min(0)]
    private int startupOneShotSuppressionFrames = 3;

    [SerializeField, Tooltip("按住 Alt 时临时禁用玩家输入，并释放鼠标用于 UI 操作。")]
    private bool suspendInputWhileAltHeld = true;

    private PlayerController _owner;
    private readonly List<ICharacterInteractionVolume> _activeInteractionVolumes = new List<ICharacterInteractionVolume>(4);
    private int _suppressOneShotInputsRemaining;
    private bool _suppressOneShotInputsForCurrentCharacterThisFrame;
    private readonly Blackboard _switchSafeBoard = new Blackboard();
    private bool _externalInputBlocked;
    private bool _altInputBlocked;
    private bool _actionMapEnabled;
    private InputActionAsset _runtimeInputActionsAsset;
    private InputActionMap _playerActionMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _fireAction;
    private InputAction _aimAction;
    private InputAction _sprintAction;
    private InputAction _jumpAction;
    private InputAction _crouchAction;
    private InputAction _talentAction;
    private InputAction _burstAction;
    private InputAction _reloadAction;
    private InputAction _interactAction;
    private InputAction _switch1Action;
    private InputAction _switch2Action;
    private InputAction _switch3Action;
    private InputAction _switch4Action;

    public bool IsGameplayInputEnabled => !_externalInputBlocked && !_altInputBlocked;

    private void Reset()
    {
        TryAssignDefaultInputAsset();
    }

    private void OnValidate()
    {
        TryAssignDefaultInputAsset();
    }

    public void Initialize(PlayerController owner, PlayerContext context)
    {
        _owner = owner;
        EnsureRuntimeActions();
    }

    public void Enable()
    {
        EnsureRuntimeActions();
        _suppressOneShotInputsRemaining = Mathf.Max(0, startupOneShotSuppressionFrames);
        RefreshInputGateState(forceApply: true);
    }

    public void Disable()
    {
        SetActionMapEnabled(false);
        _suppressOneShotInputsRemaining = Mathf.Max(0, startupOneShotSuppressionFrames);
        _suppressOneShotInputsForCurrentCharacterThisFrame = false;
        _activeInteractionVolumes.Clear();
        ApplyCameraInputState(false);
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        Tick(board);
    }

    public void Tick(Blackboard board)
    {
        UpdateAltInputBlockState();

        if (board == null)
            return;

        if (!IsGameplayInputEnabled)
        {
            board.ClearAllData();
            if (debugMode)
                DebugPrint(board);

            return;
        }

        if (_playerActionMap == null)
            return;

        board.MoveInput = ReadVector2(_moveAction);
        board.LookInput = ReadVector2(_lookAction);
        board.AttackHeld = IsPressed(_fireAction);
        board.TalentHeld = IsPressed(_talentAction);
        board.BurstHeld = IsPressed(_burstAction);
        board.IsShooting = board.AttackHeld;
        board.IsAiming = IsPressed(_aimAction);
        board.IsSprinting = IsPressed(_sprintAction);

        if (_suppressOneShotInputsRemaining > 0)
        {
            SuppressOneShotInputs(board);
            _suppressOneShotInputsRemaining--;
        }
        else
        {
            board.AttackPressed = WasPressedThisFrame(_fireAction);
            board.AttackReleased = WasReleasedThisFrame(_fireAction);
            board.JumpPressed = WasPressedThisFrame(_jumpAction);
            board.CrouchPressed = WasPressedThisFrame(_crouchAction);
            board.TalentPressed = WasPressedThisFrame(_talentAction);
            board.TalentReleased = WasReleasedThisFrame(_talentAction);
            board.BurstPressed = WasPressedThisFrame(_burstAction);
            board.BurstReleased = WasReleasedThisFrame(_burstAction);
            board.ReloadPressed = WasPressedThisFrame(_reloadAction);
            board.InteractPressed = WasPressedThisFrame(_interactAction);
            board.SwitchIndex = ReadSwitchIndex();
            board.ScrollDelta = 0;
        }

        if (debugMode)
            DebugPrint(board);
    }

    public void DispatchCurrentCharacterControl(Blackboard board)
    {
        if (_owner == null)
            return;

        CharacterDriver currentCharacter = _owner.CurrentCharacter;
        if (currentCharacter != null && currentCharacter.DataPanel != null && currentCharacter.DataPanel.IsDead)
        {
            currentCharacter.ReleasePlayerControl();
            _suppressOneShotInputsForCurrentCharacterThisFrame = false;
            return;
        }

        if (currentCharacter != null)
            currentCharacter.ReceivePlayerControl(_owner, ResolveBoardForCurrentCharacter(board));

        _suppressOneShotInputsForCurrentCharacterThisFrame = false;
    }

    public void PrimeCurrentCharacterForCurrentInput()
    {
        if (_owner == null)
            return;

        CharacterDriver currentCharacter = _owner.CurrentCharacter;
        if (currentCharacter == null || !currentCharacter.IsInitialized)
            return;

        if (currentCharacter.DataPanel != null && currentCharacter.DataPanel.IsDead)
            return;

        CopyContinuousInputs(_owner.Board, _switchSafeBoard);
        _suppressOneShotInputsForCurrentCharacterThisFrame = true;
        currentCharacter.ReceivePlayerControl(_owner, _switchSafeBoard);
        currentCharacter.Tick(_switchSafeBoard, 0f);
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        bool blocked = !enabled;
        if (_externalInputBlocked == blocked)
            return;

        _externalInputBlocked = blocked;
        RefreshInputGateState(forceApply: true);
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

    private void SuppressOneShotInputs(Blackboard board)
    {
        board.AttackHeld = false;
        board.TalentHeld = false;
        board.BurstHeld = false;
        board.IsShooting = false;
        board.AttackPressed = false;
        board.AttackReleased = false;
        board.JumpPressed = false;
        board.CrouchPressed = false;
        board.TalentPressed = false;
        board.TalentReleased = false;
        board.BurstPressed = false;
        board.BurstReleased = false;
        board.ReloadPressed = false;
        board.InteractPressed = false;
        board.SwitchIndex = -1;
        board.ScrollDelta = 0;
    }

    private Blackboard ResolveBoardForCurrentCharacter(Blackboard board)
    {
        if (!_suppressOneShotInputsForCurrentCharacterThisFrame)
            return board;

        CopyContinuousInputs(board, _switchSafeBoard);
        return _switchSafeBoard;
    }

    private void CleanupInvalidInteractionVolumes()
    {
        for (int i = _activeInteractionVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeInteractionVolumes[i] == null)
                _activeInteractionVolumes.RemoveAt(i);
        }
    }

    private static void CopyContinuousInputs(Blackboard source, Blackboard destination)
    {
        if (destination == null)
            return;

        destination.ClearAllData();
        if (source == null)
            return;

        destination.MoveInput = source.MoveInput;
        destination.LookInput = source.LookInput;
        destination.AttackHeld = source.AttackHeld;
        destination.TalentHeld = source.TalentHeld;
        destination.BurstHeld = source.BurstHeld;
        destination.IsShooting = source.IsShooting;
        destination.IsAiming = source.IsAiming;
        destination.IsSprinting = source.IsSprinting;
    }

    private int ReadSwitchIndex()
    {
        if (WasPressedThisFrame(_switch1Action)) return 0;
        if (WasPressedThisFrame(_switch2Action)) return 1;
        if (WasPressedThisFrame(_switch3Action)) return 2;
        if (WasPressedThisFrame(_switch4Action)) return 3;
        return -1;
    }

    private void EnsureRuntimeActions()
    {
        if (_runtimeInputActionsAsset != null && _playerActionMap != null)
            return;

        if (inputActionsAsset == null)
            TryAssignDefaultInputAsset();

        if (inputActionsAsset == null)
        {
            Debug.LogWarning("PlayerInputModule 缺少 InputActionAsset，输入模块将不会生效。", this);
            return;
        }

        _runtimeInputActionsAsset = Instantiate(inputActionsAsset);
        _runtimeInputActionsAsset.name = $"{inputActionsAsset.name} (Runtime)";
        _playerActionMap = _runtimeInputActionsAsset.FindActionMap(PlayerActionMapName, true);
        _moveAction = FindRequiredAction(MoveActionName);
        _lookAction = FindRequiredAction(LookActionName);
        _fireAction = FindRequiredAction(FireActionName);
        _aimAction = FindRequiredAction(AimActionName);
        _sprintAction = FindRequiredAction(SprintActionName);
        _jumpAction = FindRequiredAction(JumpActionName);
        _crouchAction = FindRequiredAction(CrouchActionName);
        _talentAction = FindRequiredAction(TalentActionName);
        _burstAction = FindRequiredAction(BurstActionName);
        _reloadAction = FindRequiredAction(ReloadActionName);
        _interactAction = FindRequiredAction(InteractActionName);
        _switch1Action = FindRequiredAction(Switch1ActionName);
        _switch2Action = FindRequiredAction(Switch2ActionName);
        _switch3Action = FindRequiredAction(Switch3ActionName);
        _switch4Action = FindRequiredAction(Switch4ActionName);
        RefreshInputGateState(forceApply: true);
    }

    private InputAction FindRequiredAction(string actionName)
    {
        return _playerActionMap != null ? _playerActionMap.FindAction(actionName, true) : null;
    }

    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private static bool WasPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private static bool WasReleasedThisFrame(InputAction action)
    {
        return action != null && action.WasReleasedThisFrame();
    }

    private void UpdateAltInputBlockState()
    {
        bool altBlocked = false;
        if (suspendInputWhileAltHeld && Keyboard.current != null)
            altBlocked = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;

        if (_altInputBlocked == altBlocked)
            return;

        _altInputBlocked = altBlocked;
        RefreshInputGateState(forceApply: true);
    }

    private void RefreshInputGateState(bool forceApply = false)
    {
        bool shouldEnableActionMap = IsGameplayInputEnabled;
        if (forceApply || _actionMapEnabled != shouldEnableActionMap)
            SetActionMapEnabled(shouldEnableActionMap);

        ApplyCameraInputState(shouldEnableActionMap);
    }

    private void SetActionMapEnabled(bool enabled)
    {
        if (_playerActionMap == null)
        {
            _actionMapEnabled = false;
            return;
        }

        if (enabled)
            _playerActionMap.Enable();
        else
            _playerActionMap.Disable();

        _actionMapEnabled = enabled;
    }

    private void ApplyCameraInputState(bool enabled)
    {
        if (PlayerCameraController.Instance != null)
            PlayerCameraController.Instance.SetGameplayInputEnabled(enabled);
    }

    private void OnDestroy()
    {
        if (_runtimeInputActionsAsset != null)
            Destroy(_runtimeInputActionsAsset);
    }

    private void TryAssignDefaultInputAsset()
    {
#if UNITY_EDITOR
        if (inputActionsAsset == null)
        {
            inputActionsAsset =
                UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultInputActionAssetPath);
        }
#endif
    }

    private void DebugPrint(Blackboard board)
    {
        Debug.Log($"Move:      {board.MoveInput}");
        Debug.Log($"AttackPressed:  {board.AttackPressed}");
        Debug.Log($"AttackHeld:     {board.AttackHeld}");
        Debug.Log($"AttackReleased: {board.AttackReleased}");
        Debug.Log($"Aim:       {board.IsAiming}");
        Debug.Log($"Sprint:    {board.IsSprinting}");
        Debug.Log($"Jump:      {board.JumpPressed}");
        Debug.Log($"Crouch:    {board.CrouchPressed}");
        Debug.Log($"Talent:    {board.TalentPressed}");
        Debug.Log($"TalentHeld:{board.TalentHeld}");
        Debug.Log($"TalentUp:  {board.TalentReleased}");
        Debug.Log($"Burst:     {board.BurstPressed}");
        Debug.Log($"BurstHeld: {board.BurstHeld}");
        Debug.Log($"BurstUp:   {board.BurstReleased}");
        Debug.Log($"Reload:    {board.ReloadPressed}");
        Debug.Log($"Interact:  {board.InteractPressed}");
        Debug.Log($"Switch:    {board.SwitchIndex}");
        Debug.Log($"Scroll:    {board.ScrollDelta}");
    }
}
