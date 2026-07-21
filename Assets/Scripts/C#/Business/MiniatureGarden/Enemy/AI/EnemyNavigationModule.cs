using CoreFramework;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人导航模块。
/// NavMesh 只负责寻路与给出前进方向，实际位移统一交给 CharacterController 驱动。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyNavigationModule : MonoBehaviour, IEnemyModule
{
    [Header("Navigation")]
    [SerializeField, Tooltip("重新下发寻路终点的最短时间间隔。")]
    [Min(0.02f)]
    private float repathInterval = 0.15f;

    [SerializeField, Tooltip("终点变化至少超过这个距离后才会强制刷新路径。")]
    [Min(0f)]
    private float repathDistanceThreshold = 0.25f;

    [SerializeField, Tooltip("行为运行时明确接管位移时，是否临时禁用 NavMeshAgent。")]
    private bool disableAgentDuringManualMotion = true;

    [SerializeField, Range(1f, 89f), Tooltip("敌人想要前进的方向与障碍物方向夹角小于等于该值时，才允许触发翻越。")]
    private float vaultApproachAngleDegrees = 45f;

    [SerializeField, Min(0.05f), Tooltip("当前方短距离内检测到可翻越交互体时，允许在真正顶墙前主动触发翻越。")]
    private float vaultForwardProbeDistance = 0.45f;

    [SerializeField, Min(0.05f), Tooltip("前向翻越探测的额外半径补偿，会叠加在 CharacterController 半径之上。")]
    private float vaultForwardProbeRadiusPadding = 0.2f;

    [SerializeField, Range(0.05f, 1f), Tooltip("若本帧实际前进距离显著小于期望前进距离，则视为被障碍阻挡，可触发翻越兜底。")]
    private float blockedMoveRatioThreshold = 0.35f;

    private EnemyDriver _owner;
    private CharacterContext _context;
    private EnemyBrainModule _brain;
    private NavMeshAgent _agent;
    private CharacterController _characterController;
    private float _nextRepathTime;
    private float _nextVaultDecisionTime;
    private Vector3 _lastRequestedDestination;
    private bool _hasRequestedDestination;
    private bool _missingNavMeshWarningLogged;

    public NavMeshAgent Agent => _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(EnemyDriver owner, CharacterContext context)
    {
        _owner = owner;
        _context = context;
        _brain = owner != null ? owner.BrainModule : null;
        _characterController = owner != null ? owner.GetComponent<CharacterController>() : null;
        _agent ??= GetComponent<NavMeshAgent>();

        ConfigureAgent();
        ResetNavigationState();
    }

    public void OnOwnerEnabled()
    {
    }

    public void OnOwnerDisabled()
    {
        StopAgent();
    }

    public void StopMovementImmediately()
    {
        StopAgent();
        _hasRequestedDestination = false;
        _nextRepathTime = 0f;
    }

    public void Tick(Blackboard board, float deltaTime)
    {
        if (_owner == null || _brain == null || board == null)
            return;

        if (ShouldSuppressNavigation())
        {
            StopAgent();
            board.MoveInput = Vector2.zero;
            return;
        }

        if (!TryPreparePlanningAgent())
        {
            MoveWithoutNavMesh(board, deltaTime);
            return;
        }

        SyncAgentToOwnerPose();
        UpdateDestinationRequest();

        Vector3 desiredVelocity = ResolvePlannedVelocity();
        Vector3 desiredDirection = FlattenDirection(desiredVelocity);
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            board.MoveInput = Vector2.zero;
            return;
        }

        if (TryTriggerVault(board, desiredDirection))
            return;

        float moveSpeed = ResolveMoveSpeed(desiredVelocity.magnitude);
        Vector3 positionBeforeMove = _owner.transform.position;
        Vector3 expectedVelocity = desiredDirection * moveSpeed;
        RotateTowards(desiredDirection, deltaTime);
        ApplyMovement(expectedVelocity, deltaTime);
        SyncAgentToOwnerPose();

        if (TryTriggerVaultAfterMove(board, desiredDirection, expectedVelocity, positionBeforeMove, deltaTime))
            return;

        board.MoveInput = ResolveLocalMoveInput(expectedVelocity);
    }

    public void LateTick(Blackboard board, float deltaTime)
    {
    }

    public void Dispose()
    {
        _owner = null;
        _context = null;
        _brain = null;
        ResetNavigationState();
    }

    private void ConfigureAgent()
    {
        if (_agent == null)
            return;

        if (_context != null && _context.Config != null)
            _agent.speed = Mathf.Max(0.1f, _context.Config.MoveSpeed);

        _agent.autoTraverseOffMeshLink = false;
        _agent.updatePosition = false;
        _agent.updateRotation = false;
    }

    private void ResetNavigationState()
    {
        _nextRepathTime = 0f;
        _nextVaultDecisionTime = 0f;
        _lastRequestedDestination = Vector3.zero;
        _hasRequestedDestination = false;
        _missingNavMeshWarningLogged = false;
    }

    private bool ShouldSuppressNavigation()
    {
        if (_owner == null || _brain == null)
            return true;

        if (disableAgentDuringManualMotion && _owner.IsInVaultState)
        {
            DisableAgentIfNeeded();
            return true;
        }

        if (_owner.StatusData == null || _owner.StatusData.IsDead)
            return true;

        if (_owner.RequiresNavigationSuppression)
            return true;

        if (!_brain.WantsMove)
            return true;

        return false;
    }

    private bool TryPreparePlanningAgent()
    {
        if (_agent == null)
            return false;

        ConfigureAgent();

        if (_agent.enabled && _agent.isOnNavMesh)
            return true;

        if (_agent.enabled && !_agent.isOnNavMesh)
        {
            if (TryWarpAgentToNearestNavMesh())
                return true;

            _agent.enabled = false;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            if (!_missingNavMeshWarningLogged)
            {
                Debug.LogWarning("EnemyNavigationModule 未能在当前敌人脚下采样到有效 NavMesh，将回退到直线位移兜底。", this);
                _missingNavMeshWarningLogged = true;
            }

            return false;
        }

        _agent.enabled = true;
        ConfigureAgent();
        _agent.Warp(hit.position);
        _missingNavMeshWarningLogged = false;
        return _agent.isOnNavMesh;
    }

    private bool TryWarpAgentToNearestNavMesh()
    {
        if (_agent == null)
            return false;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return false;

        _agent.Warp(hit.position);
        return _agent.isOnNavMesh;
    }

    private void UpdateDestinationRequest()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _brain == null)
            return;

        _agent.stoppingDistance = Mathf.Max(0f, _brain.DesiredStoppingDistance);
        _agent.isStopped = false;

        Vector3 destination = _brain.DesiredMoveDestination;
        if (!ShouldRefreshDestination(destination))
            return;

        _agent.SetDestination(destination);
        _lastRequestedDestination = destination;
        _hasRequestedDestination = true;
        _nextRepathTime = Time.time + repathInterval;
    }

    private bool ShouldRefreshDestination(Vector3 destination)
    {
        if (!_hasRequestedDestination || Time.time >= _nextRepathTime)
            return true;

        return (destination - _lastRequestedDestination).sqrMagnitude >=
               repathDistanceThreshold * repathDistanceThreshold;
    }

    private Vector3 ResolvePlannedVelocity()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return Vector3.zero;

        Vector3 desiredVelocity = _agent.desiredVelocity;
        desiredVelocity.y = 0f;
        if (desiredVelocity.sqrMagnitude > 0.0001f)
            return desiredVelocity;

        Vector3 steeringOffset = _agent.steeringTarget - transform.position;
        steeringOffset.y = 0f;
        if (steeringOffset.sqrMagnitude > 0.0001f)
            return steeringOffset.normalized * ResolveMoveSpeed(0f);

        return Vector3.zero;
    }

    private bool TryTriggerVault(Blackboard board, Vector3 desiredDirection)
    {
        if (_owner == null || _context == null || _brain == null || board == null)
            return false;

        if (_context.Config == null || !_context.Config.SupportsJump || !_brain.VaultDecisionEnabled)
            return false;

        if (Time.time < _nextVaultDecisionTime)
            return false;

        if (!_owner.TryGetVaultRequestForDirection(
                _context, desiredDirection, vaultApproachAngleDegrees, out CharacterVaultRequest request))
        {
            return false;
        }

        _context.CurrentVaultRequest = request;
        _context.HasPendingVaultRequest = true;
        board.JumpPressed = true;
        board.AttackHeld = false;
        board.AttackPressed = false;
        board.MoveInput = Vector2.zero;

        StopAgent();
        _nextVaultDecisionTime = Time.time + Mathf.Max(0f, _brain.VaultDecisionCooldown);
        return true;
    }

    private void ApplyMovement(Vector3 worldVelocity, float deltaTime)
    {
        if (_owner == null)
            return;

        if (_characterController != null && _characterController.enabled)
        {
            _characterController.SimpleMove(worldVelocity);
            return;
        }

        _owner.transform.position += worldVelocity * deltaTime;
    }

    private void RotateTowards(Vector3 desiredDirection, float deltaTime)
    {
        if (_owner == null || desiredDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
        float angularSpeed = _agent != null ? Mathf.Max(1f, _agent.angularSpeed) : 720f;
        _owner.transform.rotation = Quaternion.RotateTowards(
            _owner.transform.rotation, targetRotation, angularSpeed * deltaTime);
    }

    private void SyncAgentToOwnerPose()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        _agent.nextPosition = transform.position;
    }

    private void StopAgent()
    {
        _hasRequestedDestination = false;
        _lastRequestedDestination = Vector3.zero;
        _nextRepathTime = 0f;

        if (_agent == null || !_agent.enabled)
            return;

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.nextPosition = transform.position;
        }
    }

    private void DisableAgentIfNeeded()
    {
        if (_agent == null || !_agent.enabled)
            return;

        StopAgent();
        _agent.enabled = false;
    }

    private void MoveWithoutNavMesh(Blackboard board, float deltaTime)
    {
        if (_owner == null || _brain == null || board == null)
        {
            if (board != null)
                board.MoveInput = Vector2.zero;

            return;
        }

        Vector3 toTarget = _brain.DesiredMoveDestination - _owner.transform.position;
        Vector3 desiredDirection = FlattenDirection(toTarget);
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            board.MoveInput = Vector2.zero;
            return;
        }

        if (TryTriggerVault(board, desiredDirection))
            return;

        float moveSpeed = ResolveMoveSpeed(0f);
        Vector3 positionBeforeMove = _owner.transform.position;
        Vector3 expectedVelocity = desiredDirection * moveSpeed;
        RotateTowards(desiredDirection, deltaTime);
        ApplyMovement(expectedVelocity, deltaTime);

        if (TryTriggerVaultAfterMove(board, desiredDirection, expectedVelocity, positionBeforeMove, deltaTime))
            return;

        board.MoveInput = ResolveLocalMoveInput(expectedVelocity);
    }

    private float ResolveMoveSpeed(float requestedSpeed)
    {
        float configuredSpeed = _context != null && _context.Config != null
            ? Mathf.Max(0f, _context.Config.MoveSpeed)
            : 0f;
        if (configuredSpeed > 0f)
            return requestedSpeed > 0f ? Mathf.Min(configuredSpeed, requestedSpeed) : configuredSpeed;

        if (_agent != null)
            return Mathf.Max(0.1f, requestedSpeed > 0f ? Mathf.Min(_agent.speed, requestedSpeed) : _agent.speed);

        return Mathf.Max(0.1f, requestedSpeed);
    }

    private bool TryTriggerVaultAfterMove(Blackboard board, Vector3 desiredDirection, Vector3 expectedVelocity,
        Vector3 positionBeforeMove, float deltaTime)
    {
        if (_owner == null || board == null || deltaTime <= 0f)
            return false;

        Vector3 actualDisplacement = _owner.transform.position - positionBeforeMove;
        actualDisplacement.y = 0f;

        Vector3 expectedDisplacement = expectedVelocity * deltaTime;
        expectedDisplacement.y = 0f;
        float expectedDistance = expectedDisplacement.magnitude;
        if (expectedDistance <= 0.0001f)
            return false;

        bool hitSides = _characterController != null &&
                        (_characterController.collisionFlags & CollisionFlags.Sides) != 0;
        bool movedTooLittle = actualDisplacement.magnitude <= expectedDistance * blockedMoveRatioThreshold;
        if (!hitSides && !movedTooLittle)
            return false;

        return TryTriggerVaultByForwardProbe(board, desiredDirection);
    }

    private bool TryTriggerVaultByForwardProbe(Blackboard board, Vector3 desiredDirection)
    {
        if (_owner == null || _context == null || _brain == null || board == null)
            return false;

        if (_context.Config == null || !_context.Config.SupportsJump || !_brain.VaultDecisionEnabled)
            return false;

        if (Time.time < _nextVaultDecisionTime)
            return false;

        if (!TryFindVaultRequestAhead(desiredDirection, out CharacterVaultRequest request))
            return false;

        _context.CurrentVaultRequest = request;
        _context.HasPendingVaultRequest = true;
        board.JumpPressed = true;
        board.AttackHeld = false;
        board.AttackPressed = false;
        board.MoveInput = Vector2.zero;

        StopAgent();
        _nextVaultDecisionTime = Time.time + Mathf.Max(0f, _brain.VaultDecisionCooldown);
        return true;
    }

    private bool TryFindVaultRequestAhead(Vector3 desiredDirection, out CharacterVaultRequest request)
    {
        request = default;
        if (_context == null || _context.Transform == null)
            return false;

        float baseRadius = _characterController != null ? _characterController.radius : 0.4f;
        float probeRadius = Mathf.Max(0.05f, baseRadius + vaultForwardProbeRadiusPadding);
        float probeDistance = Mathf.Max(0.05f, vaultForwardProbeDistance);
        Vector3 origin = _context.Transform.position + desiredDirection.normalized * probeDistance;

        Collider[] colliders = Physics.OverlapSphere(origin, probeRadius, ~0, QueryTriggerInteraction.Collide);
        if (colliders == null || colliders.Length == 0)
            return false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitCollider = colliders[i];
            if (hitCollider == null)
                continue;

            CharacterInteractionVolume volume = hitCollider.GetComponentInParent<CharacterInteractionVolume>();
            if (volume == null || !volume.AllowsVault)
                continue;

            if (volume.TryBuildVaultRequestForApproach(
                    _context, desiredDirection, vaultApproachAngleDegrees, out request))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 ResolveLocalMoveInput(Vector3 worldVelocity)
    {
        if (_owner == null)
            return Vector2.zero;

        Vector3 localVelocity = _owner.transform.InverseTransformDirection(worldVelocity);
        Vector2 moveInput = new Vector2(localVelocity.x, localVelocity.z);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        return moveInput;
    }

    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return direction.normalized;
    }
}
