using CoreFramework;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBrainModule : MonoBehaviour, IEnemyModule
{
    private const float DefaultTargetRefreshInterval = 0.25f;
    private const float DefaultTargetLostDelay = 1f;
    private const float DefaultAttackRange = 6f;
    private const float DefaultAttackHysteresis = 0.35f;
    private const float DefaultVaultCooldown = 0.75f;
    private const float DefaultReturnHomeStoppingDistance = 0.05f;
    private const float DefaultHomeFacingSpeed = 720f;

    [SerializeField, Min(0.05f), Tooltip("敌人刷新索敌结果的时间间隔，单位为秒。")] private float targetRefreshInterval = DefaultTargetRefreshInterval;
    [SerializeField, Min(0f), Tooltip("目标超出索敌模块保持范围后，需要持续丢失多久才会彻底放弃目标并回到原位。")] private float targetLostDelay = DefaultTargetLostDelay;
    [SerializeField, Min(0.1f), Tooltip("敌人允许发起攻击的距离，单位为世界距离。")] private float attackRange = DefaultAttackRange;
    [SerializeField, Min(0f), Tooltip("攻击距离滞后范围，用于避免敌人在攻击边界附近反复切换状态。")] private float attackRangeHysteresis = DefaultAttackHysteresis;
    [SerializeField, Tooltip("开启后敌人 AI 可以在满足交互条件时决策进入蹲下行为。")] private bool enableCrouchDecision;
    [SerializeField, Min(0f), Tooltip("敌人允许考虑蹲下行为的最小目标距离。")] private float crouchDistanceMin = 2f;
    [SerializeField, Min(0f), Tooltip("敌人允许考虑蹲下行为的最大目标距离。")] private float crouchDistanceMax = 8f;
    [SerializeField, Tooltip("开启后敌人 AI 可以在可翻越交互范围内决策翻越障碍。")] private bool enableVaultDecision = true;
    [SerializeField, Min(0f), Tooltip("敌人两次翻越决策之间的冷却时间，单位为秒。")] private float vaultDecisionCooldown = DefaultVaultCooldown;

    private EnemyDriver _owner;
    private CharacterContext _context;
    private float _nextTargetRefreshTime;
    private float _targetLostTimer;
    private bool _wasAttackingLastFrame;
    private bool _hasHomePose;
    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private float _nextAttackRetargetTime;

    public StatusData CurrentTargetData { get; private set; }
    public Transform CurrentTargetTransform { get; private set; }
    public Vector3 CurrentAimPoint { get; private set; }
    public Vector3 DesiredMoveDestination { get; private set; }
    public float DesiredStoppingDistance { get; private set; }
    public bool WantsAttack { get; private set; }
    public bool WantsMove { get; private set; }
    public bool HasTarget => CurrentTargetData != null && CurrentTargetTransform != null;
    public bool VaultDecisionEnabled => IsVaultDecisionEnabled();
    public float VaultDecisionCooldown => GetVaultCooldown();

    public void Initialize(EnemyDriver owner, CharacterContext context)
    {
        _owner = owner;
        _context = context;
        CaptureHomePose();
        ClearDecisionState();
    }

    public void OnOwnerEnabled()
    {
        CaptureHomePose();
    }

    public void OnOwnerDisabled()
    {
        ClearDecisionState();
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        if (_owner == null || board == null)
            return;

        if (_owner.StatusData == null || _owner.StatusData.IsDead)
        {
            ClearDecisionState();
            return;
        }

        RefreshTargetIfNeeded(deltaTime);
        EvaluatePrimaryIntent(board, deltaTime);
        EvaluateCrouch(board);
    }

    public void LateTick(Blackboard board, float deltaTime)
    {
    }

    public void Dispose()
    {
        _owner = null;
        _context = null;
        ClearDecisionState();
    }

    private void RefreshTargetIfNeeded(float deltaTime)
    {
        if (_owner == null)
            return;

        NonPlayerTargetingModule targetingModule = _owner.NonPlayerTargetingProvider;
        if (targetingModule == null || _owner.StatusData == null)
        {
            ClearTargetState();
            return;
        }

        bool hasCurrentTarget = HasTarget;
        bool targetAliveAndTargetable = IsCurrentTargetAliveAndTargetable();
        if (hasCurrentTarget && !targetAliveAndTargetable)
        {
            ClearTargetState();
            hasCurrentTarget = false;
        }

        bool targetWithinTrackingRange = hasCurrentTarget &&
                                         targetingModule.IsTargetWithinLoseDistance(CurrentTargetTransform);
        if (hasCurrentTarget && !targetWithinTrackingRange)
        {
            _targetLostTimer += Mathf.Max(0f, deltaTime);
            if (_targetLostTimer >= GetTargetLostDelay())
            {
                ClearTargetState();
                hasCurrentTarget = false;
            }
        }
        else if (hasCurrentTarget)
        {
            _targetLostTimer = 0f;
        }

        if (HasTarget)
            CurrentAimPoint = CurrentTargetTransform.position;

        if (HasTarget && Time.time < _nextTargetRefreshTime)
            return;

        _nextTargetRefreshTime = Time.time + GetTargetRefreshInterval();
        Vector3 origin = _owner.transform.position;
        Vector3 facingDirection = ResolveSearchFacingDirection();
        if (targetingModule.TryResolveCombatTarget(_owner.StatusData, origin, facingDirection, out NonPlayerTargetingResult result))
        {
            SetCurrentTarget(result);
            targetingModule.SetTrackedTarget(result);
            _targetLostTimer = 0f;
            return;
        }

        if (!HasTarget)
            targetingModule.ClearTrackedTarget();
    }

    private void EvaluatePrimaryIntent(Blackboard board, float deltaTime)
    {
        WantsAttack = false;
        WantsMove = false;
        DesiredMoveDestination = _owner != null ? _owner.transform.position : Vector3.zero;
        DesiredStoppingDistance = Mathf.Max(0.1f, GetAttackRange() - GetAttackHysteresis());

        if (_owner == null || board == null)
            return;

        if (!HasTarget)
        {
            ClearLockedAttackTargetingSnapshot();
            _wasAttackingLastFrame = false;
            EvaluateReturnHome(deltaTime);
            return;
        }

        Vector3 ownerPosition = _owner.transform.position;
        Vector3 targetPosition = CurrentTargetTransform.position;
        Vector3 flatOffset = targetPosition - ownerPosition;
        flatOffset.y = 0f;
        float distance = flatOffset.magnitude;
        float enterAttackRange = GetAttackRange();
        float exitAttackRange = enterAttackRange + GetAttackHysteresis();
        bool shouldAttack = _wasAttackingLastFrame ? distance <= exitAttackRange : distance <= enterAttackRange;

        DesiredMoveDestination = targetPosition;
        if (!shouldAttack)
        {
            ClearLockedAttackTargetingSnapshot();
            _wasAttackingLastFrame = false;
            WantsMove = true;
            return;
        }

        WantsAttack = true;
        WantsMove = false;
        RefreshAttackFacingDuringAttack(!_wasAttackingLastFrame);
        ApplyAttackInput(board, !_wasAttackingLastFrame);
    }

    private void EvaluateCrouch(Blackboard board)
    {
        if (_owner == null || _context == null || board == null || WantsAttack)
            return;

        bool supportsCrouch = _context.Config != null && _context.Config.SupportsCrouch;
        if (!supportsCrouch || !IsCrouchDecisionEnabled())
            return;

        bool isCurrentlyCrouching = _context.CurrentStance == CharacterStance.Crouching;
        bool shouldCrouch = false;
        if (HasTarget && _owner.IsInCoverInteractionRange(_context))
        {
            float distance = Vector3.Distance(_owner.transform.position, CurrentTargetTransform.position);
            shouldCrouch = distance >= GetCrouchDistanceMin() && distance <= GetCrouchDistanceMax() && !WantsMove;
        }

        if (shouldCrouch != isCurrentlyCrouching)
        {
            board.CrouchPressed = true;
            if (shouldCrouch)
            {
                board.AttackHeld = false;
                board.AttackPressed = false;
                WantsAttack = false;
                _wasAttackingLastFrame = false;
            }
        }
    }

    private void EvaluateReturnHome(float deltaTime)
    {
        if (_owner == null || !_hasHomePose)
            return;

        DesiredMoveDestination = _homePosition;
        DesiredStoppingDistance = DefaultReturnHomeStoppingDistance;

        Vector3 homeOffset = _homePosition - _owner.transform.position;
        homeOffset.y = 0f;
        if (homeOffset.sqrMagnitude > DefaultReturnHomeStoppingDistance * DefaultReturnHomeStoppingDistance)
        {
            WantsMove = true;
            return;
        }

        _owner.transform.rotation = Quaternion.RotateTowards(
            _owner.transform.rotation, _homeRotation, DefaultHomeFacingSpeed * Mathf.Max(0f, deltaTime));
    }

    private void ApplyAttackInput(Blackboard board, bool pressedThisFrame)
    {
        if (board == null)
            return;

        board.AttackHeld = true;
        board.AttackPressed = pressedThisFrame;
        WantsAttack = true;
        WantsMove = false;
        _wasAttackingLastFrame = true;
    }

    private bool IsCurrentTargetAliveAndTargetable()
    {
        return CurrentTargetData != null &&
               CurrentTargetTransform != null &&
               !CurrentTargetData.IsDead &&
               CurrentTargetData.IsTargetable;
    }

    private bool IsTargetStillInAttackRange()
    {
        if (_owner == null || CurrentTargetTransform == null)
            return false;

        Vector3 flatOffset = CurrentTargetTransform.position - _owner.transform.position;
        flatOffset.y = 0f;
        return flatOffset.magnitude <= GetAttackRange();
    }

    private Vector3 ResolveSearchFacingDirection()
    {
        if (_owner == null)
            return Vector3.forward;

        Vector3 forward = _owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            return forward.normalized;

        if (_hasHomePose)
        {
            Vector3 homeForward = _homeRotation * Vector3.forward;
            homeForward.y = 0f;
            if (homeForward.sqrMagnitude > 0.0001f)
                return homeForward.normalized;
        }

        return Vector3.forward;
    }

    private Vector3 ResolveAttackDirection()
    {
        if (_owner == null)
            return Vector3.zero;

        Vector3 targetDirection = CurrentAimPoint - _owner.transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude > 0.0001f)
            return targetDirection.normalized;

        if (CurrentTargetTransform == null)
            return Vector3.zero;

        targetDirection = CurrentTargetTransform.position - _owner.transform.position;
        targetDirection.y = 0f;
        return targetDirection.sqrMagnitude > 0.0001f ? targetDirection.normalized : Vector3.zero;
    }

    private void SetCurrentTarget(NonPlayerTargetingResult result)
    {
        CurrentTargetData = result.targetData;
        CurrentTargetTransform = result.targetTransform;
        CurrentAimPoint = result.aimPoint;
    }

    private void CaptureHomePose()
    {
        if (_owner == null)
            return;

        _homePosition = _owner.transform.position;
        _homeRotation = _owner.transform.rotation;
        _hasHomePose = true;
    }

    private void ClearDecisionState()
    {
        ClearTargetState();
        ClearLockedAttackTargetingSnapshot();
        DesiredMoveDestination = Vector3.zero;
        DesiredStoppingDistance = 0f;
        WantsAttack = false;
        WantsMove = false;
        _wasAttackingLastFrame = false;
        _nextTargetRefreshTime = 0f;
        _nextAttackRetargetTime = 0f;
        _targetLostTimer = 0f;
    }

    private void ClearTargetState()
    {
        ClearLockedAttackTargetingSnapshot();
        CurrentTargetData = null;
        CurrentTargetTransform = null;
        CurrentAimPoint = Vector3.zero;
        _targetLostTimer = 0f;
        _nextAttackRetargetTime = 0f;
        if (_owner != null && _owner.NonPlayerTargetingProvider != null)
            _owner.NonPlayerTargetingProvider.ClearTrackedTarget();
    }

    private float GetTargetRefreshInterval()
    {
        return Mathf.Max(0.05f, targetRefreshInterval);
    }

    private float GetTargetLostDelay()
    {
        return Mathf.Max(0f, targetLostDelay);
    }

    private float GetAttackRange()
    {
        return Mathf.Max(0.1f, attackRange);
    }

    private float GetAttackHysteresis()
    {
        return Mathf.Max(0f, attackRangeHysteresis);
    }

    private float GetAttackRetargetInterval()
    {
        NonPlayerTargetingModule targetingModule = _owner != null ? _owner.NonPlayerTargetingProvider : null;
        if (targetingModule != null)
            return targetingModule.AttackRetargetInterval;

        return 0f;
    }

    private bool IsCrouchDecisionEnabled()
    {
        return enableCrouchDecision;
    }

    private float GetCrouchDistanceMin()
    {
        return Mathf.Max(0f, crouchDistanceMin);
    }

    private float GetCrouchDistanceMax()
    {
        return Mathf.Max(0f, crouchDistanceMax);
    }

    private bool IsVaultDecisionEnabled()
    {
        return enableVaultDecision;
    }

    private float GetVaultCooldown()
    {
        return Mathf.Max(0f, vaultDecisionCooldown);
    }

    private void RefreshAttackFacingDuringAttack(bool forceRefresh)
    {
        if (_owner == null)
            return;

        float retargetInterval = GetAttackRetargetInterval();
        if (!forceRefresh && retargetInterval > 0f && Time.time < _nextAttackRetargetTime)
            return;

        Vector3 attackDirection = ResolveAttackDirection();
        TryRefreshAttackTargetingSnapshot(ref attackDirection);
        if (attackDirection.sqrMagnitude <= 0.0001f)
            return;

        _owner.transform.rotation = Quaternion.LookRotation(attackDirection.normalized, Vector3.up);
        _nextAttackRetargetTime = retargetInterval > 0f ? Time.time + retargetInterval : Time.time;
    }

    private void TryRefreshAttackTargetingSnapshot(ref Vector3 attackDirection)
    {
        if (_owner == null || _owner.StatusData == null)
            return;

        NonPlayerTargetingModule targetingModule = _owner.NonPlayerTargetingProvider;
        if (targetingModule == null)
            return;

        targetingModule.ClearLockedProjectileTargetingSnapshot();
        Vector3 fallbackDirection = attackDirection.sqrMagnitude > 0.0001f ? attackDirection : ResolveSearchFacingDirection();
        if (!targetingModule.TryResolveProjectileTargeting(_owner.StatusData, _owner.transform.position, fallbackDirection,
                out ProjectileTargetingResult targetingResult))
        {
            return;
        }

        targetingModule.LockProjectileTargetingSnapshot(targetingResult);
        CurrentTargetData = targetingResult.targetData;
        CurrentTargetTransform = targetingResult.targetTransform;
        CurrentAimPoint = targetingResult.aimPoint;

        Vector3 lockedDirection = targetingResult.launchDirection;
        lockedDirection.y = 0f;
        if (lockedDirection.sqrMagnitude > 0.0001f)
            attackDirection = lockedDirection.normalized;
    }

    private void ClearLockedAttackTargetingSnapshot()
    {
        NonPlayerTargetingModule targetingModule = _owner != null ? _owner.NonPlayerTargetingProvider : null;
        if (targetingModule != null)
            targetingModule.ClearLockedProjectileTargetingSnapshot();

        _nextAttackRetargetTime = 0f;
    }
}
