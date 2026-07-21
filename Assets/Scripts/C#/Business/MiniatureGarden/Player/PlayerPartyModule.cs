using CoreFramework;
using System.Collections.Generic;
using UnityEngine;
using AudioType = CoreFramework.AudioType;

public readonly struct PlayerCharacterSwitchedEvent
{
    public readonly PlayerController Player;
    public readonly CharacterDriver PreviousCharacter;
    public readonly CharacterDriver CurrentCharacter;
    public readonly int CurrentCharacterIndex;
    public readonly bool CurrentCharacterWasOffField;
    public readonly bool PreviousCharacterNeedsStay;
    public readonly bool UsedDirectPoseInheritance;

    public PlayerCharacterSwitchedEvent(PlayerController player, CharacterDriver previousCharacter,
        CharacterDriver currentCharacter, int currentCharacterIndex, bool currentCharacterWasOffField,
        bool previousCharacterNeedsStay, bool usedDirectPoseInheritance)
    {
        Player = player;
        PreviousCharacter = previousCharacter;
        CurrentCharacter = currentCharacter;
        CurrentCharacterIndex = currentCharacterIndex;
        CurrentCharacterWasOffField = currentCharacterWasOffField;
        PreviousCharacterNeedsStay = previousCharacterNeedsStay;
        UsedDirectPoseInheritance = usedDirectPoseInheritance;
    }
}

[DisallowMultipleComponent]
public sealed class PlayerPartyModule : MonoBehaviour, IPlayerModule
{
    private sealed class PendingHideRequest
    {
        public CharacterDriver Character;
        public bool WaitForDeathPlaybackCompletion;
    }

    private sealed class SwitchScaleAnimation
    {
        public CharacterDriver Character;
        public Transform ScaleTarget;
        public Vector3 TargetScale;
        public float Duration;
        public float Elapsed;
    }

    [Header("Party")]
    [SerializeField]
    private CharacterDriver[] controllableCharacters = System.Array.Empty<CharacterDriver>();

    [SerializeField, Min(0f)]
    private float switchBackCooldown = 0.5f;

    [Header("Switch Presentation")]
    [SerializeField]
    private GameObject switchCharacterVfxPrefab;

    [SerializeField, Min(0.01f)]
    private float switchCharacterVfxRecycleTime = 1f;

    [SerializeField, Min(0f)]
    private float switchSpawnScaleDuration = 0.12f;

    [SerializeField]
    private AnimationCurve switchSpawnScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField, Tooltip("角色根节点下用于入场缩放的骨骼路径，例如 BoneRoot。留空则不执行入场缩放。")]
    private string switchSpawnScaleTargetPath = string.Empty;

    private readonly List<PendingHideRequest> _pendingHideRequests = new List<PendingHideRequest>(4);
    private readonly List<SwitchScaleAnimation> _activeSwitchScaleAnimations = new List<SwitchScaleAnimation>(2);
    private readonly Dictionary<CharacterDriver, Vector3> _characterDefaultScales = new Dictionary<CharacterDriver, Vector3>(4);
    private readonly Dictionary<CharacterDriver, Transform> _characterVisualScaleTargets = new Dictionary<CharacterDriver, Transform>(4);
    private float[] _switchCooldowns = System.Array.Empty<float>();
    private PlayerController _owner;
    private CharacterDriver _currentCharacter;
    private int _currentCharacterIndex = -1;
    private bool _unitDeathSubscribed;

    public CharacterDriver CurrentCharacter => _currentCharacter;
    public int CurrentCharacterIndex => _currentCharacterIndex;
    public bool HasConfiguredCharacters => controllableCharacters != null && controllableCharacters.Length > 0;
    public IReadOnlyList<CharacterDriver> ConfiguredCharacters => controllableCharacters;
    public int ConfiguredCharacterCount => controllableCharacters != null ? controllableCharacters.Length : 0;

    public AudioClip SwitchCharacterAudioClip;

    private void OnValidate()
    {
        controllableCharacters = SanitizeCharacters(controllableCharacters);
        switchSpawnScaleTargetPath = switchSpawnScaleTargetPath != null ? switchSpawnScaleTargetPath.Trim() : string.Empty;
        if (switchSpawnScaleCurve == null)
            switchSpawnScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public void Initialize(PlayerController owner, PlayerContext context)
    {
        _owner = owner;
    }

    public void Enable()
    {
        SubscribeUnitDeathEvent();
        controllableCharacters = SanitizeCharacters(controllableCharacters);
        RebuildPartyState();
        EnsureInitialCharacterSelected();
    }

    public void Disable()
    {
        UnsubscribeUnitDeathEvent();
        _pendingHideRequests.Clear();
        _activeSwitchScaleAnimations.Clear();
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        if (_owner == null)
            return;

        EnsureInitialCharacterSelected();
        UpdateSwitchCooldowns(deltaTime);

        if (board != null && board.SwitchIndex >= 0)
            SetCurrentCharacter(board.SwitchIndex, false);

        UpdateSwitchScaleAnimations(deltaTime);
        ProcessPendingHideRequests();
    }

    public void RebuildPartyState()
    {
        _pendingHideRequests.Clear();
        _activeSwitchScaleAnimations.Clear();
        _characterDefaultScales.Clear();
        _characterVisualScaleTargets.Clear();
        if (controllableCharacters == null || controllableCharacters.Length == 0)
        {
            _switchCooldowns = System.Array.Empty<float>();
            ClearCurrentCharacterRuntime();
            return;
        }

        NormalizeCharacterHierarchy();
        EnsureCooldownCapacity();
        ClearSwitchCooldowns();

        for (int i = 0; i < controllableCharacters.Length; i++)
        {
            CharacterDriver character = controllableCharacters[i];
            if (character == null)
                continue;

            Vector3 defaultScale = GetOrCacheCharacterDefaultScale(character);
            SetCharacterVisualScale(character, defaultScale);

            BindPlayerTargetingProvider(character);
            EnsureCharacterDataExists(character);
            SyncPlayerRootLayer(character);
            DisableCharacterLocalRuntimeComponents(character);
            character.ReleasePlayerControl();

            bool shouldBeActive = i == 0;
            if (character.gameObject.activeSelf != shouldBeActive)
                character.gameObject.SetActive(shouldBeActive);

            if (character.IsInitialized)
                character.RefreshRuntimeCharacterData(false);
        }

        ClearCurrentCharacterRuntime();
    }

    public void SetConfiguredCharacters(CharacterDriver[] characters, bool rebuildPartyState)
    {
        ClearRemovedCharacterVfx(characters);
        controllableCharacters = SanitizeCharacters(characters);
        EnsureCooldownCapacity();
        if (rebuildPartyState)
        {
            RebuildPartyState();
            EnsureInitialCharacterSelected();
        }
    }

    public bool SetCurrentCharacter(int index, bool force)
    {
        return SetCurrentCharacter(index, force,
            hidePreviousImmediately: false,
            waitForPreviousDeathPlaybackCompletion: false,
            allowPreviousStayPlacement: true,
            allowMoveInheritance: true);
    }

    private bool SetCurrentCharacter(int index, bool force, bool hidePreviousImmediately,
        bool waitForPreviousDeathPlaybackCompletion, bool allowPreviousStayPlacement, bool allowMoveInheritance)
    {
        if (controllableCharacters == null || controllableCharacters.Length == 0)
        {
            ClearCurrentCharacterRuntime();
            return false;
        }

        int safeIndex = Mathf.Clamp(index, 0, controllableCharacters.Length - 1);
        CharacterDriver nextCharacter = controllableCharacters[safeIndex];
        if (nextCharacter == null || IsCharacterDead(nextCharacter))
            return false;

        if (!force && nextCharacter == _currentCharacter)
            return false;

        if (!force && IsSwitchCoolingDown(safeIndex))
            return false;

        CharacterDriver previousCharacter = _currentCharacter;
        if (!force && previousCharacter != null && previousCharacter != nextCharacter && !previousCharacter.CanSwitchOut)
            return false;

        bool shouldEnterMoveAfterSwitch = allowMoveInheritance &&
                                          previousCharacter != null &&
                                          previousCharacter != nextCharacter &&
                                          previousCharacter.IsInMoveState;

        bool wasOffField = !nextCharacter.gameObject.activeSelf;
        bool previousCharacterNeedsStay = false;
        bool shouldInheritPreviousPose = false;
        if (previousCharacter != null && previousCharacter != nextCharacter)
        {
            previousCharacter.ReleasePlayerControl();
            shouldInheritPreviousPose = allowPreviousStayPlacement && previousCharacter.UsesDirectPoseInheritanceOnSwitch;
            bool queuedForDelayedHide = QueueCharacterForHide(previousCharacter, hidePreviousImmediately,
                waitForPreviousDeathPlaybackCompletion);
            previousCharacterNeedsStay = allowPreviousStayPlacement && queuedForDelayedHide;
        }

        if (TryResolveIncomingRootPose(previousCharacter, nextCharacter, wasOffField, previousCharacterNeedsStay,
                out Vector3 desiredRootPosition, out Quaternion desiredRootRotation))
        {
            _owner?.SetRootPose(desiredRootPosition, desiredRootRotation);
        }

        SetCurrentCharacterRuntime(nextCharacter, safeIndex);
        ApplyPlayerRootRuntimeTemplate(nextCharacter);
        EnsureCharacterActive(nextCharacter, wasOffField);
        _owner.SyncControlledCharacterToPlayerRoot();
        _owner.FollowPlayerRoot();
        _owner.PrimeCurrentCharacterForCurrentInput();
        if (shouldEnterMoveAfterSwitch)
            _owner.PrimeCurrentCharacterForCurrentInput();

        TypedEventBus.Publish(new PlayerCharacterSwitchedEvent(_owner, previousCharacter, nextCharacter, safeIndex,
            wasOffField, previousCharacterNeedsStay, shouldInheritPreviousPose));
        StartSwitchCooldown(previousCharacter);

        PlaySwitchPresentation(nextCharacter);
        AudioManager.Instance.Play(SwitchCharacterAudioClip, AudioType.Sound, transform.position);
        return true;
    }

    private bool QueueCharacterForHide(CharacterDriver character, bool hideImmediately,
        bool waitForDeathPlaybackCompletion)
    {
        if (character == null)
            return false;

        if (hideImmediately)
        {
            HideCharacterImmediately(character);
            return false;
        }

        bool requiresDelayedHide = waitForDeathPlaybackCompletion || !character.CanBeHiddenAfterSwitch;
        if (!requiresDelayedHide)
        {
            HideCharacterImmediately(character);
            return false;
        }

        int requestIndex = FindPendingHideRequestIndex(character);
        if (requestIndex >= 0)
        {
            if (waitForDeathPlaybackCompletion)
                _pendingHideRequests[requestIndex].WaitForDeathPlaybackCompletion = true;

            return true;
        }

        _pendingHideRequests.Add(new PendingHideRequest
        {
            Character = character,
            WaitForDeathPlaybackCompletion = waitForDeathPlaybackCompletion,
        });
        return true;
    }

    private void EnsureCharacterActive(CharacterDriver character, bool wasOffField)
    {
        if (character == null)
            return;

        BindPlayerTargetingProvider(character);
        SyncPlayerRootLayer(character);
        DisableCharacterLocalRuntimeComponents(character);
        RemovePendingHideRequest(character);
        RemoveSwitchScaleAnimation(character);

        Vector3 targetScale = GetOrCacheCharacterDefaultScale(character);
        if (wasOffField && _owner != null)
            ApplyCharacterPose(character, _owner.transform.position, _owner.transform.rotation);

        if (wasOffField)
            SetCharacterVisualScale(character, Vector3.zero);
        else
            SetCharacterVisualScale(character, targetScale);

        if (wasOffField)
            character.gameObject.SetActive(true);

        EnsureCharacterDataExists(character);
        character.RefreshRuntimeCharacterData(true);
        character.RefreshMovementStrategyBinding();
        if (wasOffField)
        {
            character.PrepareForReactivationFromOffField();
            StartSwitchScaleAnimation(character, targetScale);
        }
    }

    private void UpdateSwitchScaleAnimations(float deltaTime)
    {
        if (_activeSwitchScaleAnimations.Count == 0)
            return;

        for (int i = _activeSwitchScaleAnimations.Count - 1; i >= 0; i--)
        {
            SwitchScaleAnimation animation = _activeSwitchScaleAnimations[i];
            CharacterDriver character = animation.Character;
            Transform scaleTarget = animation.ScaleTarget;
            if (character == null || !character.gameObject.activeSelf || scaleTarget == null)
            {
                _activeSwitchScaleAnimations.RemoveAt(i);
                continue;
            }

            animation.Elapsed += Mathf.Max(0f, deltaTime);
            float normalizedTime = animation.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(animation.Elapsed / animation.Duration);
            float evaluated = switchSpawnScaleCurve != null
                ? Mathf.Clamp01(switchSpawnScaleCurve.Evaluate(normalizedTime))
                : normalizedTime;

            scaleTarget.localScale = Vector3.LerpUnclamped(Vector3.zero, animation.TargetScale, evaluated);
            if (normalizedTime >= 1f)
            {
                scaleTarget.localScale = animation.TargetScale;
                _activeSwitchScaleAnimations.RemoveAt(i);
            }
        }
    }

    private void ProcessPendingHideRequests()
    {
        if (_pendingHideRequests.Count == 0)
            return;

        for (int i = _pendingHideRequests.Count - 1; i >= 0; i--)
        {
            PendingHideRequest request = _pendingHideRequests[i];
            CharacterDriver character = request.Character;
            if (character == null || character == _currentCharacter)
            {
                _pendingHideRequests.RemoveAt(i);
                continue;
            }

            if (!character.gameObject.activeSelf)
            {
                _pendingHideRequests.RemoveAt(i);
                continue;
            }

            if (request.WaitForDeathPlaybackCompletion)
            {
                if (!character.IsDeathBehaviorPlaybackFinished())
                    continue;
            }
            else if (!character.CanBeHiddenAfterSwitch)
            {
                continue;
            }

            HideCharacterImmediately(character);
        }
    }

    private void EnsureInitialCharacterSelected()
    {
        if (_currentCharacter != null || controllableCharacters == null || controllableCharacters.Length == 0)
            return;

        int firstAliveIndex = FindNextAliveCharacterIndex(-1);
        if (firstAliveIndex >= 0)
            SetCurrentCharacter(firstAliveIndex, true);
    }

    private static void ApplyCharacterPose(CharacterDriver character, Vector3 position, Quaternion rotation)
    {
        if (character == null)
            return;

        CharacterController controller = character.LocalCharacterController;
        bool restoreController = controller != null && controller.enabled;
        if (restoreController)
            controller.enabled = false;

        character.transform.SetPositionAndRotation(position, rotation);

        if (restoreController)
            controller.enabled = true;
    }

    private void NormalizeCharacterHierarchy()
    {
        if (!Application.isPlaying || _owner == null || controllableCharacters == null)
            return;

        Transform playerRoot = _owner.transform;
        for (int i = 0; i < controllableCharacters.Length; i++)
        {
            CharacterDriver character = controllableCharacters[i];
            if (character == null)
                continue;

            Transform characterTransform = character.transform;
            if (characterTransform.parent == null || !characterTransform.IsChildOf(playerRoot))
                continue;

            characterTransform.SetParent(null, true);
        }
    }

    private void ApplyPlayerRootRuntimeTemplate(CharacterDriver character)
    {
        if (character == null || _owner == null || _owner.MovementModule == null)
            return;

        if (character.RuntimeConfig != null)
            _owner.MovementModule.ApplyUnitConfig(character.RuntimeConfig);

        if (character.TryGetComponent(out CharacterController controllerTemplate))
            _owner.MovementModule.ApplyCharacterControllerTemplate(controllerTemplate);
    }

    private void SyncPlayerRootLayer(CharacterDriver character)
    {
        if (_owner == null || character == null)
            return;

        _owner.gameObject.layer = character.gameObject.layer;
    }

    private static void DisableCharacterLocalRuntimeComponents(CharacterDriver character)
    {
        if (!Application.isPlaying || character == null)
            return;

        CharacterController controller = character.LocalCharacterController;
        if (controller != null && controller.enabled)
            controller.enabled = false;
    }

    private bool TryResolveIncomingRootPose(CharacterDriver previousCharacter, CharacterDriver nextCharacter,
        bool nextCharacterWasOffField, bool previousCharacterNeedsStay, out Vector3 position, out Quaternion rotation)
    {
        position = _owner != null ? _owner.transform.position : Vector3.zero;
        rotation = _owner != null ? _owner.transform.rotation : Quaternion.identity;

        if (_owner == null || nextCharacter == null)
            return false;

        if (previousCharacter == null)
        {
            position = nextCharacter.transform.position;
            rotation = nextCharacter.transform.rotation;
            return true;
        }

        if (!nextCharacterWasOffField)
        {
            position = nextCharacter.transform.position;
            rotation = nextCharacter.transform.rotation;
            return true;
        }

        if (previousCharacterNeedsStay &&
            _owner.SwitchPlacementModule != null &&
            _owner.SwitchPlacementModule.TryResolveSwitchPlacement(previousCharacter, nextCharacter,
                out position, out rotation))
        {
            return true;
        }

        position = previousCharacter.transform.position;
        rotation = previousCharacter.transform.rotation;
        return true;
    }

    private static void EnsureCharacterDataExists(CharacterDriver character)
    {
        if (character == null || character.RuntimeConfig == null || character.RuntimeConfig.UnitId <= 0)
            return;

        CharacterDataManager.Instance.GetOrCreateCharacterData(character.RuntimeConfig.UnitId);
    }

    private void SubscribeUnitDeathEvent()
    {
        if (_unitDeathSubscribed)
            return;

        TypedEventBus.Subscribe<UnitDiedEvent>(HandleUnitDied);
        _unitDeathSubscribed = true;
    }

    private void UnsubscribeUnitDeathEvent()
    {
        if (!_unitDeathSubscribed)
            return;

        TypedEventBus.Unsubscribe<UnitDiedEvent>(HandleUnitDied);
        _unitDeathSubscribed = false;
    }

    private void HandleUnitDied(UnitDiedEvent eventData)
    {
        if (eventData.Unit == null || controllableCharacters == null || controllableCharacters.Length == 0)
            return;

        int deadCharacterIndex = FindCharacterIndex(eventData.Unit);
        if (deadCharacterIndex < 0)
            return;

        CharacterDriver deadCharacter = controllableCharacters[deadCharacterIndex];
        if (deadCharacter == null)
            return;

        deadCharacter.ReleasePlayerControl();
        deadCharacter.ForceEnterDeathState();
        QueueCharacterForHide(deadCharacter, hideImmediately: false, waitForDeathPlaybackCompletion: true);

        if (deadCharacter != _currentCharacter)
            return;

        int nextAliveIndex = FindNextAliveCharacterIndex(deadCharacterIndex);
        if (nextAliveIndex < 0)
        {
            ClearCurrentCharacterRuntime(clearPendingHideRequests: false);
            Debug.LogError("当前编队已全部死亡，后续流程暂未实现。", this);
            return;
        }

        SetCurrentCharacterAfterDeath(nextAliveIndex);
    }

    private void SetCurrentCharacterAfterDeath(int nextIndex)
    {
        if (controllableCharacters == null || nextIndex < 0 || nextIndex >= controllableCharacters.Length)
            return;

        SetCurrentCharacter(nextIndex, true,
            hidePreviousImmediately: false,
            waitForPreviousDeathPlaybackCompletion: true,
            allowPreviousStayPlacement: false,
            allowMoveInheritance: false);
    }

    private int FindCharacterIndex(StatusData data)
    {
        if (data == null || controllableCharacters == null)
            return -1;

        for (int i = 0; i < controllableCharacters.Length; i++)
        {
            CharacterDriver character = controllableCharacters[i];
            if (character != null && character.StatusData == data)
                return i;
        }

        return -1;
    }

    private int FindNextAliveCharacterIndex(int startIndex)
    {
        if (controllableCharacters == null || controllableCharacters.Length == 0)
            return -1;

        int count = controllableCharacters.Length;
        int normalizedStartIndex = Mathf.Clamp(startIndex, -1, count - 1);
        for (int step = 1; step <= count; step++)
        {
            int candidateIndex = (normalizedStartIndex + step) % count;
            CharacterDriver candidate = controllableCharacters[candidateIndex];
            if (candidate == null || IsCharacterDead(candidate))
                continue;

            return candidateIndex;
        }

        return -1;
    }

    private static bool IsCharacterDead(CharacterDriver character)
    {
        return character != null && character.StatusData != null && character.StatusData.IsDead;
    }

    private void PlaySwitchPresentation(CharacterDriver nextCharacter)
    {
        if (nextCharacter == null || switchCharacterVfxPrefab == null)
            return;

        int ownerId = _owner != null ? _owner.GetInstanceID() : GetInstanceID();
        Vector3 spawnPosition = ResolveSwitchVfxSpawnPosition(nextCharacter);
        VFXPool.Instance.Spawn(ownerId, switchCharacterVfxPrefab, spawnPosition,
            nextCharacter.transform.rotation, switchCharacterVfxPrefab.transform.localScale,
            switchCharacterVfxRecycleTime, (go) =>
            {
                go.transform.SetParent(nextCharacter.transform);
            });
    }

    private static Vector3 ResolveSwitchVfxSpawnPosition(CharacterDriver character)
    {
        if (character == null)
            return Vector3.zero;

        CharacterController controller = character.LocalCharacterController;
        if (controller == null)
            return character.transform.position;

        return character.transform.position + controller.center + Vector3.down * (controller.height * 0.5f);
    }

    private void StartSwitchScaleAnimation(CharacterDriver character, Vector3 targetScale)
    {
        if (character == null)
            return;

        Transform scaleTarget = ResolveCharacterVisualScaleTarget(character);
        if (scaleTarget == null)
            return;

        RemoveSwitchScaleAnimation(character);
        if (switchSpawnScaleDuration <= 0f)
        {
            scaleTarget.localScale = targetScale;
            return;
        }

        _activeSwitchScaleAnimations.Add(new SwitchScaleAnimation
        {
            Character = character,
            ScaleTarget = scaleTarget,
            TargetScale = targetScale,
            Duration = switchSpawnScaleDuration,
            Elapsed = 0f,
        });
    }

    private Vector3 GetOrCacheCharacterDefaultScale(CharacterDriver character)
    {
        if (character == null)
            return Vector3.one;

        if (_characterDefaultScales.TryGetValue(character, out Vector3 cachedScale))
            return cachedScale;

        Transform scaleTarget = ResolveCharacterVisualScaleTarget(character);
        cachedScale = scaleTarget != null ? scaleTarget.localScale : Vector3.one;
        _characterDefaultScales.Add(character, cachedScale);
        return cachedScale;
    }

    private Transform ResolveCharacterVisualScaleTarget(CharacterDriver character)
    {
        if (character == null)
            return null;

        if (_characterVisualScaleTargets.TryGetValue(character, out Transform cachedTarget) && cachedTarget != null)
            return cachedTarget;

        if (string.IsNullOrWhiteSpace(switchSpawnScaleTargetPath))
            return null;

        Transform root = character.transform;
        Transform resolvedTarget = root.Find(switchSpawnScaleTargetPath);

        if (resolvedTarget != null)
            _characterVisualScaleTargets[character] = resolvedTarget;

        return resolvedTarget;
    }

    private void SetCharacterVisualScale(CharacterDriver character, Vector3 scale)
    {
        Transform scaleTarget = ResolveCharacterVisualScaleTarget(character);
        if (scaleTarget == null)
            return;

        scaleTarget.localScale = scale;
    }

    private int FindPendingHideRequestIndex(CharacterDriver character)
    {
        if (character == null)
            return -1;

        for (int i = 0; i < _pendingHideRequests.Count; i++)
        {
            if (_pendingHideRequests[i].Character == character)
                return i;
        }

        return -1;
    }

    private void RemovePendingHideRequest(CharacterDriver character)
    {
        int requestIndex = FindPendingHideRequestIndex(character);
        if (requestIndex >= 0)
            _pendingHideRequests.RemoveAt(requestIndex);
    }

    private void RemoveSwitchScaleAnimation(CharacterDriver character)
    {
        if (character == null)
            return;

        for (int i = _activeSwitchScaleAnimations.Count - 1; i >= 0; i--)
        {
            if (_activeSwitchScaleAnimations[i].Character == character)
                _activeSwitchScaleAnimations.RemoveAt(i);
        }
    }

    private void HideCharacterImmediately(CharacterDriver character)
    {
        if (character == null)
            return;

        RemovePendingHideRequest(character);
        RemoveSwitchScaleAnimation(character);
        SetCharacterVisualScale(character, GetOrCacheCharacterDefaultScale(character));
        character.gameObject.SetActive(false);
    }

    private void BindPlayerTargetingProvider(CharacterDriver character)
    {
        if (character == null)
            return;

        IUnitTargetingProvider targetingProvider = _owner != null ? _owner.UnitTargetingProvider : null;
        character.SetUnitTargetingProviderOverride(targetingProvider);
    }

    private void UpdateSwitchCooldowns(float deltaTime)
    {
        if (_switchCooldowns == null || _switchCooldowns.Length == 0 || deltaTime <= 0f)
            return;

        for (int i = 0; i < _switchCooldowns.Length; i++)
        {
            if (_switchCooldowns[i] <= 0f)
                continue;

            _switchCooldowns[i] = Mathf.Max(0f, _switchCooldowns[i] - deltaTime);
        }
    }

    private void EnsureCooldownCapacity()
    {
        int desiredLength = controllableCharacters != null ? controllableCharacters.Length : 0;
        if (_switchCooldowns != null && _switchCooldowns.Length == desiredLength)
            return;

        _switchCooldowns = desiredLength > 0 ? new float[desiredLength] : System.Array.Empty<float>();
    }

    private void ClearSwitchCooldowns()
    {
        if (_switchCooldowns == null || _switchCooldowns.Length == 0)
            return;

        for (int i = 0; i < _switchCooldowns.Length; i++)
            _switchCooldowns[i] = 0f;
    }

    private bool IsSwitchCoolingDown(int index)
    {
        if (_switchCooldowns == null || index < 0 || index >= _switchCooldowns.Length)
            return false;

        return _switchCooldowns[index] > 0f;
    }

    private void StartSwitchCooldown(CharacterDriver character)
    {
        if (character == null || switchBackCooldown <= 0f || controllableCharacters == null)
            return;

        for (int i = 0; i < controllableCharacters.Length; i++)
        {
            if (controllableCharacters[i] != character)
                continue;

            if (_switchCooldowns != null && i < _switchCooldowns.Length)
                _switchCooldowns[i] = switchBackCooldown;
            return;
        }
    }

    public static CharacterDriver[] SanitizeCharacters(CharacterDriver[] characters)
    {
        if (characters == null || characters.Length == 0)
            return System.Array.Empty<CharacterDriver>();

        int validCount = 0;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
                validCount++;
        }

        if (validCount == characters.Length)
            return characters;

        CharacterDriver[] filtered = new CharacterDriver[validCount];
        int writeIndex = 0;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null)
                continue;

            filtered[writeIndex] = characters[i];
            writeIndex++;
        }

        return filtered;
    }

    private void SetCurrentCharacterRuntime(CharacterDriver character, int index)
    {
        _currentCharacter = character;
        _currentCharacterIndex = index;
    }

    private void ClearCurrentCharacterRuntime(bool clearPendingHideRequests = true)
    {
        _currentCharacter?.ReleasePlayerControl();
        _currentCharacter = null;
        _currentCharacterIndex = -1;
        _activeSwitchScaleAnimations.Clear();
        if (clearPendingHideRequests)
            _pendingHideRequests.Clear();
    }

    public static CharacterDriver[] CollectSceneCharacters()
    {
        CharacterDriver[] sceneCharacters = Object.FindObjectsByType<CharacterDriver>(FindObjectsSortMode.None);
        return SanitizeCharacters(sceneCharacters);
    }

    [ContextMenu("Auto Find Scene Characters")]
    private void AutoFindSceneCharacters()
    {
        controllableCharacters = CollectSceneCharacters();
        if (Application.isPlaying && _owner != null)
            SetConfiguredCharacters(controllableCharacters, true);

        Debug.Log($"PlayerPartyModule 已收集到 {controllableCharacters.Length} 个 CharacterDriver。", this);
    }

    private void ClearRemovedCharacterVfx(CharacterDriver[] nextCharacters)
    {
        if (controllableCharacters == null || controllableCharacters.Length == 0)
            return;

        CharacterDriver[] sanitizedNextCharacters = SanitizeCharacters(nextCharacters);
        for (int i = 0; i < controllableCharacters.Length; i++)
        {
            CharacterDriver existingCharacter = controllableCharacters[i];
            if (existingCharacter == null || ContainsCharacter(sanitizedNextCharacters, existingCharacter))
                continue;

            int ownerId = existingCharacter.RuntimeConfig != null ? existingCharacter.RuntimeConfig.UnitId : 0;
            if (ownerId > 0)
                VFXPool.Instance.ClearOwner(ownerId);
        }
    }

    private static bool ContainsCharacter(CharacterDriver[] characters, CharacterDriver target)
    {
        if (characters == null || target == null)
            return false;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == target)
                return true;
        }

        return false;
    }
}
