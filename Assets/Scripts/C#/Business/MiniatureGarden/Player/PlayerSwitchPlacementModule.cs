using UnityEngine;

/// <summary>
/// 玩家切人入场位姿模块。
/// 负责在“上一个角色需要驻场、下一个角色当前离场”时，为入场角色求解一个安全的错位落点。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSwitchPlacementModule : MonoBehaviour, IPlayerModule
{
    [Header("Switch Placement")]
    [SerializeField, Tooltip("启用后，切换到离场角色时会尝试为其寻找错位入场点，避免与驻场角色模型重叠。")]
    private bool enableSwitchPlacement = true;

    [SerializeField, Tooltip("求解失败前最多尝试多少个候选点。")]
    [Min(1)]
    private int maxPlacementAttempts = 10;

    [SerializeField, Tooltip("候选点允许相对被切走角色当前高度上下波动的最大值。超过该值直接判失败，避免瞬移到墙顶或高台。")]
    [Min(0f)]
    private float maxHeightDelta = 0.3f;

    [SerializeField, Tooltip("地面法线 Y 最低要求。过小会被视为墙面或过陡斜坡，不允许作为入场点。")]
    [Range(0f, 1f)]
    private float minimumGroundNormalY = 0.55f;

    [Header("Enemy Target Placement")]
    [SerializeField, Tooltip("存在敌人目标时，围绕敌人 forward 扇区随机入场的最小距离。")]
    [Min(0f)]
    private float enemyMinDistance = 2.5f;

    [SerializeField, Tooltip("存在敌人目标时，围绕敌人 forward 扇区随机入场的最大距离。")]
    [Min(0f)]
    private float enemyMaxDistance = 4.5f;

    [SerializeField, Tooltip("存在敌人目标时，相对敌人 forward 的最小偏航角。")]
    private float enemyMinYaw = -75f;

    [SerializeField, Tooltip("存在敌人目标时，相对敌人 forward 的最大偏航角。")]
    private float enemyMaxYaw = 75f;

    [Header("Fallback Placement")]
    [SerializeField, Tooltip("没有敌人目标时，围绕驻场角色 forward 扇区随机入场的最小距离。")]
    [Min(0f)]
    private float fallbackMinDistance = 1.8f;

    [SerializeField, Tooltip("没有敌人目标时，围绕驻场角色 forward 扇区随机入场的最大距离。")]
    [Min(0f)]
    private float fallbackMaxDistance = 3.25f;

    [SerializeField, Tooltip("没有敌人目标时，相对驻场角色 forward 的最小偏航角。")]
    private float fallbackMinYaw = -60f;

    [SerializeField, Tooltip("没有敌人目标时，相对驻场角色 forward 的最大偏航角。")]
    private float fallbackMaxYaw = 60f;

    [Header("Placement Validation")]
    [SerializeField, Tooltip("向下找地面时的起始抬高距离。")]
    [Min(0.05f)]
    private float groundProbeStartHeight = 1.5f;

    [SerializeField, Tooltip("向下找地面时的最大检测距离。")]
    [Min(0.1f)]
    private float groundProbeDistance = 4f;

    [SerializeField, Tooltip("脚下支撑检测使用的半径比例。越大越严格，越能避免踩在边缘。")]
    [Range(0.1f, 1f)]
    private float supportProbeRadiusFactor = 0.75f;

    [SerializeField, Tooltip("脚下支撑检测允许的高度波动。超过该值视为踏空或站在边缘。")]
    [Min(0f)]
    private float supportHeightTolerance = 0.12f;

    [SerializeField, Tooltip("胶囊体重叠检测时向上抬起的微小偏移，用来避免把地面接触误判成占位冲突。")]
    [Min(0f)]
    private float collisionProbeLift = 0.03f;

    [SerializeField, Tooltip("胶囊体重叠检测时收缩的半径，用来降低与地面接触的误判。")]
    [Min(0f)]
    private float collisionRadiusShrink = 0.03f;

    [SerializeField, Tooltip("可被当作落脚地面的层级。")]
    private LayerMask groundLayers = ~0;

    [SerializeField, Tooltip("用于检测入场点占位冲突的层级。通常建议包含角色、敌人、障碍和场景碰撞。")]
    private LayerMask obstacleLayers = ~0;

    [Header("Debug")]
    [SerializeField, Tooltip("启用后输出本模块的入场求解日志。")]
    private bool logPlacementRuntime;

    [SerializeField, Tooltip("最近一次求解是否成功，仅用于 Inspector 观察。")]
    private bool runtimeLastResolveSucceeded;

    [SerializeField, Tooltip("最近一次求解失败原因，仅用于 Inspector 观察。")]
    private string runtimeLastFailureReason = string.Empty;

    [SerializeField, Tooltip("最近一次成功落点，仅用于 Inspector 观察。")]
    private Vector3 runtimeLastResolvedPosition;

    [SerializeField, Tooltip("最近一次成功朝向，仅用于 Inspector 观察。")]
    private Vector3 runtimeLastResolvedForward;

    [SerializeField, Tooltip("最近一次是按敌人目标求解还是按驻场角色前方求解，仅用于 Inspector 观察。")]
    private bool runtimeLastUsedEnemyTarget;

    private readonly Collider[] _overlapResults = new Collider[16];
    private readonly RaycastHit[] _castResults = new RaycastHit[16];
    private PlayerController _owner;

    internal bool RuntimeLastResolveSucceeded => runtimeLastResolveSucceeded;

    public void Initialize(PlayerController owner, PlayerContext context)
    {
        _owner = owner;
    }

    public void Enable()
    {
    }

    public void Disable()
    {
    }

    public void Tick(CoreFramework.Blackboard board, float deltaTime)
    {
    }

    public bool TryResolveSwitchPlacement(CharacterDriver previousCharacter, CharacterDriver nextCharacter,
        out Vector3 position, out Quaternion rotation)
    {
        position = nextCharacter != null ? nextCharacter.transform.position : Vector3.zero;
        rotation = nextCharacter != null ? nextCharacter.transform.rotation : Quaternion.identity;
        ResetRuntimeDebug();

        if (!enableSwitchPlacement)
            return Fail("切人错位入场模块已关闭。");

        if (previousCharacter == null || nextCharacter == null)
            return Fail("前后角色引用为空，无法求解入场点。");

        CharacterController nextController = ResolvePlacementController(nextCharacter);
        if (nextController == null)
            return Fail("入场角色缺少 CharacterController，无法进行安全落点检测。");

        Vector3 referencePosition = previousCharacter.transform.position;
        float referenceY = referencePosition.y;
        bool useEnemyTarget = TryResolveEnemyTarget(previousCharacter, out Transform targetTransform,
            out Vector3 targetAimPoint);

        Vector3 anchorPosition = useEnemyTarget && targetTransform != null
            ? targetTransform.position
            : referencePosition;
        Vector3 baseForward = useEnemyTarget && targetTransform != null
            ? Flatten(targetTransform.forward)
            : Flatten(previousCharacter.transform.forward);

        if (baseForward.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallbackDirection = useEnemyTarget
                ? Flatten(targetAimPoint - referencePosition)
                : Flatten(nextCharacter.transform.forward);
            baseForward = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector3.forward;
        }

        baseForward.Normalize();
        runtimeLastUsedEnemyTarget = useEnemyTarget;

        float minDistance = useEnemyTarget ? enemyMinDistance : fallbackMinDistance;
        float maxDistance = useEnemyTarget ? enemyMaxDistance : fallbackMaxDistance;
        float minYaw = useEnemyTarget ? enemyMinYaw : fallbackMinYaw;
        float maxYaw = useEnemyTarget ? enemyMaxYaw : fallbackMaxYaw;

        if (maxDistance < minDistance)
            (minDistance, maxDistance) = (maxDistance, minDistance);
        if (maxYaw < minYaw)
            (minYaw, maxYaw) = (maxYaw, minYaw);

        int attemptCount = Mathf.Max(1, maxPlacementAttempts);
        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            float distance = Random.Range(minDistance, maxDistance);
            float yaw = Random.Range(minYaw, maxYaw);
            Vector3 radialDirection = Quaternion.AngleAxis(yaw, Vector3.up) * baseForward;
            Vector3 candidatePosition = anchorPosition + radialDirection * distance;

            if (!TryResolveGroundedPosition(candidatePosition, nextController, referenceY, out Vector3 groundedPosition))
                continue;

            if (!HasGroundSupport(groundedPosition, nextController, referenceY))
                continue;

            if (!HasStandingRoom(groundedPosition, nextController, nextCharacter))
                continue;

            if (!HasClearSwitchPath(referencePosition, groundedPosition, nextController, previousCharacter,
                    nextCharacter))
            {
                continue;
            }

            if (useEnemyTarget && !HasClearTargetSight(groundedPosition, nextController, targetTransform,
                    targetAimPoint, previousCharacter, nextCharacter))
            {
                continue;
            }

            position = groundedPosition;
            rotation = ResolveEntryRotation(useEnemyTarget, groundedPosition, targetTransform, targetAimPoint,
                previousCharacter, radialDirection);

            runtimeLastResolveSucceeded = true;
            runtimeLastFailureReason = string.Empty;
            runtimeLastResolvedPosition = position;
            runtimeLastResolvedForward = rotation * Vector3.forward;

            if (logPlacementRuntime)
            {
                Debug.Log(
                    $"[{name}] 切人入场点求解成功 | Prev={previousCharacter.name} | Next={nextCharacter.name} | EnemyTarget={useEnemyTarget} | Attempt={attempt + 1}/{attemptCount} | Position={position} | Forward={runtimeLastResolvedForward}",
                    this);
            }

            return true;
        }

        return Fail("尝试多个候选点后仍未找到满足高度、地面支撑与占位安全的入场点。");
    }

    private bool TryResolveEnemyTarget(CharacterDriver previousCharacter, out Transform targetTransform,
        out Vector3 targetAimPoint)
    {
        targetTransform = null;
        targetAimPoint = previousCharacter != null ? previousCharacter.transform.position : Vector3.zero;
        if (previousCharacter == null || previousCharacter.DataPanel == null)
            return false;

        IUnitTargetingProvider provider = previousCharacter.UnitTargetingProvider;
        if (provider == null)
            return false;

        Vector3 fallbackDirection = Flatten(previousCharacter.transform.forward);
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            fallbackDirection = Vector3.forward;

        if (!provider.TryResolveProjectileTargeting(previousCharacter.DataPanel, previousCharacter.transform.position,
                fallbackDirection, out ProjectileTargetingResult result))
        {
            return false;
        }

        targetTransform = result.targetTransform != null
            ? result.targetTransform
            : result.targetData != null ? result.targetData.transform : null;
        targetAimPoint = result.aimPoint;
        return targetTransform != null;
    }

    private bool TryResolveGroundedPosition(Vector3 candidatePosition, CharacterController controller, float referenceY,
        out Vector3 groundedPosition)
    {
        groundedPosition = candidatePosition;

        float probeStart = Mathf.Max(groundProbeStartHeight, maxHeightDelta + 0.1f);
        Vector3 rayOrigin = new Vector3(candidatePosition.x, referenceY + probeStart, candidatePosition.z);
        float rayDistance = probeStart + Mathf.Max(groundProbeDistance, maxHeightDelta + 0.1f);
        int groundMask = groundLayers.value != 0 ? groundLayers.value : Physics.DefaultRaycastLayers;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider != null && hit.collider.GetComponentInParent<StatusData>() != null)
            return false;

        if (hit.normal.y < minimumGroundNormalY)
            return false;

        if (Mathf.Abs(hit.point.y - referenceY) > maxHeightDelta)
            return false;

        float groundedRootY = hit.point.y + controller.height * 0.5f - controller.center.y;
        groundedPosition = new Vector3(candidatePosition.x, groundedRootY, candidatePosition.z);
        return true;
    }

    private bool HasGroundSupport(Vector3 rootPosition, CharacterController controller, float referenceY)
    {
        Vector3 footCenter = GetFootContactCenter(rootPosition, controller);
        float supportRadius = Mathf.Max(0.05f, controller.radius * supportProbeRadiusFactor);
        Vector3[] offsets =
        {
            Vector3.zero,
            Vector3.forward * supportRadius,
            Vector3.back * supportRadius,
            Vector3.left * supportRadius,
            Vector3.right * supportRadius,
        };

        int groundMask = groundLayers.value != 0 ? groundLayers.value : Physics.DefaultRaycastLayers;
        float sampleStartHeight = Mathf.Max(0.2f, supportHeightTolerance + 0.15f);
        float rayDistance = sampleStartHeight + Mathf.Max(supportHeightTolerance, maxHeightDelta) + 0.2f;

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 sampleCenter = footCenter + offsets[i];
            Vector3 rayOrigin = sampleCenter + Vector3.up * sampleStartHeight;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.collider != null && hit.collider.GetComponentInParent<StatusData>() != null)
                return false;

            if (hit.normal.y < minimumGroundNormalY)
                return false;

            if (Mathf.Abs(hit.point.y - footCenter.y) > supportHeightTolerance)
                return false;

            if (Mathf.Abs(hit.point.y - referenceY) > maxHeightDelta)
                return false;
        }

        return true;
    }

    private bool HasStandingRoom(Vector3 rootPosition, CharacterController controller, CharacterDriver nextCharacter)
    {
        BuildCapsule(rootPosition, controller, out Vector3 bottom, out Vector3 top, out float radius);

        int obstacleMask = obstacleLayers.value != 0 ? obstacleLayers.value : Physics.DefaultRaycastLayers;
        float liftedRadius = Mathf.Max(0.01f, radius - collisionRadiusShrink);
        Vector3 liftOffset = Vector3.up * collisionProbeLift;

        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom + liftOffset, top + liftOffset, liftedRadius,
            _overlapResults, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapResults[i];
            if (hit == null)
                continue;

            if (nextCharacter != null && hit.transform.IsChildOf(nextCharacter.transform))
                continue;

            return false;
        }

        return true;
    }

    private bool HasClearSwitchPath(Vector3 currentRootPosition, Vector3 nextRootPosition, CharacterController controller,
        CharacterDriver previousCharacter, CharacterDriver nextCharacter)
    {
        Vector3 path = nextRootPosition - currentRootPosition;
        float distance = path.magnitude;
        if (distance <= 0.01f)
            return true;

        Vector3 direction = path / distance;
        float probeRadius = Mathf.Max(0.05f, controller.radius - collisionRadiusShrink);
        if (!HasClearSpherePath(ResolveBodyProbePoint(currentRootPosition, controller, 0.35f), direction, distance,
                probeRadius, previousCharacter, nextCharacter, null))
        {
            return false;
        }

        return HasClearSpherePath(ResolveBodyProbePoint(currentRootPosition, controller, 0.7f), direction, distance,
            probeRadius, previousCharacter, nextCharacter, null);
    }

    private bool HasClearTargetSight(Vector3 rootPosition, CharacterController controller, Transform targetTransform,
        Vector3 targetAimPoint, CharacterDriver previousCharacter, CharacterDriver nextCharacter)
    {
        if (targetTransform == null)
            return true;

        Vector3 origin = ResolveBodyProbePoint(rootPosition, controller, 0.65f);
        Vector3 path = targetAimPoint - origin;
        float distance = path.magnitude;
        if (distance <= 0.01f)
            return true;

        Vector3 direction = path / distance;
        float probeRadius = Mathf.Max(0.05f, (controller.radius - collisionRadiusShrink) * 0.5f);
        return HasClearSpherePath(origin, direction, distance, probeRadius, previousCharacter, nextCharacter,
            targetTransform);
    }

    private bool HasClearSpherePath(Vector3 origin, Vector3 direction, float distance, float radius,
        CharacterDriver previousCharacter, CharacterDriver nextCharacter, Transform ignoredTarget)
    {
        int obstacleMask = obstacleLayers.value != 0 ? obstacleLayers.value : Physics.DefaultRaycastLayers;
        int hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, _castResults, distance, obstacleMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _castResults[i];
            Collider collider = hit.collider;
            if (collider == null)
                continue;

            if (previousCharacter != null && collider.transform.IsChildOf(previousCharacter.transform))
                continue;

            if (nextCharacter != null && collider.transform.IsChildOf(nextCharacter.transform))
                continue;

            if (ignoredTarget != null && collider.transform.IsChildOf(ignoredTarget))
                continue;

            return false;
        }

        return true;
    }

    private static void BuildCapsule(Vector3 rootPosition, CharacterController controller, out Vector3 bottom,
        out Vector3 top, out float radius)
    {
        radius = Mathf.Max(0.01f, controller.radius);
        Vector3 worldCenter = rootPosition + controller.center;
        float halfSegment = Mathf.Max(0f, controller.height * 0.5f - radius);
        bottom = worldCenter + Vector3.down * halfSegment;
        top = worldCenter + Vector3.up * halfSegment;
    }

    private CharacterController ResolvePlacementController(CharacterDriver nextCharacter)
    {
        if (nextCharacter != null && nextCharacter.TryGetComponent(out CharacterController characterController))
            return characterController;

        return _owner != null && _owner.MovementModule != null ? _owner.MovementModule.Controller : null;
    }

    private static Vector3 GetFootContactCenter(Vector3 rootPosition, CharacterController controller)
    {
        return rootPosition + controller.center + Vector3.down * (controller.height * 0.5f);
    }

    private static Vector3 ResolveBodyProbePoint(Vector3 rootPosition, CharacterController controller,
        float heightFactor)
    {
        float clampedFactor = Mathf.Clamp01(heightFactor);
        float localY = controller.center.y - controller.height * 0.5f + controller.height * clampedFactor;
        return rootPosition + new Vector3(controller.center.x, localY, controller.center.z);
    }

    private static Quaternion ResolveEntryRotation(bool useEnemyTarget, Vector3 entryPosition, Transform targetTransform,
        Vector3 targetAimPoint, CharacterDriver previousCharacter, Vector3 fallbackDirection)
    {
        Vector3 lookDirection;
        if (useEnemyTarget)
        {
            Vector3 targetPosition = targetTransform != null ? targetTransform.position : targetAimPoint;
            lookDirection = Flatten(targetPosition - entryPosition);
        }
        else if (previousCharacter != null)
        {
            Vector3 forwardPoint = previousCharacter.transform.position + Flatten(previousCharacter.transform.forward);
            lookDirection = Flatten(forwardPoint - entryPosition);
        }
        else
        {
            lookDirection = Flatten(fallbackDirection);
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Flatten(fallbackDirection);
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Vector3.forward;

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private bool Fail(string reason)
    {
        runtimeLastResolveSucceeded = false;
        runtimeLastFailureReason = reason;
        runtimeLastResolvedPosition = Vector3.zero;
        runtimeLastResolvedForward = Vector3.zero;

        if (logPlacementRuntime)
            Debug.LogWarning($"[{name}] 切人入场点求解失败 | {reason}", this);

        return false;
    }

    private void ResetRuntimeDebug()
    {
        runtimeLastResolveSucceeded = false;
        runtimeLastFailureReason = string.Empty;
        runtimeLastResolvedPosition = Vector3.zero;
        runtimeLastResolvedForward = Vector3.zero;
        runtimeLastUsedEnemyTarget = false;
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.zero;
    }
}
