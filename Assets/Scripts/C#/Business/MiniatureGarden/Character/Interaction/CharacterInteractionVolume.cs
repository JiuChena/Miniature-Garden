using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 场景交互体。挂在掩体或障碍物上，通过触发器向角色声明“这里能蹲下/能翻越”。
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("MiniatureGarden/Interaction/Traversal Interaction Volume")]
public class CharacterInteractionVolume : MonoBehaviour, ICharacterInteractionVolume
{
    [Header("Filter")]
    [FormerlySerializedAs("playerLayerMask")]
    [SerializeField, Tooltip("只有这些层级的对象进入触发器时才会被识别为角色交互接收者。玩家和敌人都可以共用这套判定。")]
    private LayerMask receiverLayerMask = ~0;

    [Space(8)]
    [Header("Capabilities")]
    [SerializeField, Tooltip("开启后表示角色进入该范围后可以执行掩体蹲下")]
    private bool allowsCover = true;

    [SerializeField, Tooltip("开启后表示角色进入该范围后可以执行翻越")]
    private bool allowsVault = true;

    [Space(8)]
    [Header("Vault")]
    [SerializeField, Tooltip("翻越终点标记。存在时优先使用该 Transform 的世界坐标作为落点")]
    private Transform vaultEndPoint;

    [SerializeField, Tooltip("未指定翻越终点标记时，使用该本地偏移推导翻越落点")]
    private Vector3 vaultEndOffset = new Vector3(0f, 0f, 1.6f);

    [SerializeField, Tooltip("翻越朝向。为零向量时回退为交互体当前朝向")]
    private Vector3 vaultFacingDirection = Vector3.forward;

    [SerializeField, Tooltip("翻越抛物线的额外抬升高度，单位为米")]
    [Min(0f)]
    private float vaultArcHeight = 0.6f;

    [Space(8)]
    [Header("Debug")]
    [SerializeField, Tooltip("开启后在 Scene 视图中绘制翻越辅助线")]
    private bool drawDebugGizmos = true;

    private readonly HashSet<ICharacterInteractionVolumeReceiver> _overlappingReceivers =
        new HashSet<ICharacterInteractionVolumeReceiver>();

    public bool AllowsCover => allowsCover;
    public bool AllowsVault => allowsVault;

    public bool TryBuildVaultRequest(CharacterContext context, out CharacterVaultRequest request)
    {
        request = default;
        if (!allowsVault || context == null || context.Transform == null)
            return false;

        Vector3 startPosition = context.Transform.position;
        bool isCharacterOnPositiveSide = ResolvePositiveSideForCharacter(context.Transform);
        Vector3 endPosition = ResolveVaultEndPosition(isCharacterOnPositiveSide);
        Vector3 facingDirection = ResolveVaultFacingDirection(startPosition, endPosition, isCharacterOnPositiveSide);

        request = new CharacterVaultRequest
        {
            StartPosition = startPosition,
            EndPosition = endPosition,
            FacingDirection = facingDirection,
            ArcHeight = vaultArcHeight,
        };
        return true;
    }

    public bool TryBuildVaultRequestForApproach(CharacterContext context, Vector3 desiredDirection,
        float maxApproachAngleDegrees, out CharacterVaultRequest request)
    {
        request = default;
        if (!IsVaultApproachCompatible(context != null ? context.Transform : null,
                desiredDirection, maxApproachAngleDegrees))
        {
            return false;
        }

        return TryBuildVaultRequest(context, out request);
    }

    public bool IsVaultApproachCompatible(Transform characterTransform, Vector3 desiredDirection,
        float maxApproachAngleDegrees)
    {
        if (!allowsVault || characterTransform == null)
            return false;

        Vector3 planarDesiredDirection = desiredDirection;
        planarDesiredDirection.y = 0f;
        if (planarDesiredDirection.sqrMagnitude <= 0.0001f)
            return false;

        planarDesiredDirection.Normalize();
        Vector3 referencePoint = ResolveApproachReferencePoint(characterTransform.position);
        Vector3 toVolume = referencePoint - characterTransform.position;
        toVolume.y = 0f;
        if (toVolume.sqrMagnitude <= 0.0001f)
            return true;

        float approachAngle = Vector3.Angle(planarDesiredDirection, toVolume.normalized);
        return approachAngle <= Mathf.Max(0f, maxApproachAngleDegrees);
    }

    private bool ResolvePositiveSideForCharacter(Transform characterTransform)
    {
        if (characterTransform == null)
            return true;

        Vector3 obstacleForward = transform.forward;
        obstacleForward.y = 0f;
        if (obstacleForward.sqrMagnitude <= 0.0001f)
            obstacleForward = Vector3.forward;
        else
            obstacleForward.Normalize();

        Vector3 toCharacter = characterTransform.position - transform.position;
        toCharacter.y = 0f;
        float sideDot = Vector3.Dot(toCharacter, obstacleForward);
        if (Mathf.Abs(sideDot) > 0.01f)
            return sideDot >= 0f;

        Vector3 characterForward = characterTransform.forward;
        characterForward.y = 0f;
        if (characterForward.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(characterForward.normalized, obstacleForward) >= 0f;
    }

    private Vector3 ResolveVaultEndPosition(bool isCharacterOnPositiveSide)
    {
        Vector3 localEndPosition = vaultEndPoint != null
            ? transform.InverseTransformPoint(vaultEndPoint.position)
            : vaultEndOffset;
        float absoluteForwardDistance = Mathf.Abs(localEndPosition.z);
        if (absoluteForwardDistance <= 0.001f)
            absoluteForwardDistance = Mathf.Max(0.001f, Mathf.Abs(vaultEndOffset.z));

        localEndPosition.z = isCharacterOnPositiveSide ? -absoluteForwardDistance : absoluteForwardDistance;
        return transform.TransformPoint(localEndPosition);
    }

    private Vector3 ResolveVaultFacingDirection(Vector3 startPosition, Vector3 endPosition, bool isCharacterOnPositiveSide)
    {
        Vector3 flatTravelDirection = endPosition - startPosition;
        flatTravelDirection.y = 0f;
        if (flatTravelDirection.sqrMagnitude > 0.0001f)
            return flatTravelDirection.normalized;

        Vector3 localFacingDirection = vaultFacingDirection.sqrMagnitude > 0.0001f
            ? vaultFacingDirection.normalized
            : Vector3.forward;
        if (isCharacterOnPositiveSide)
            localFacingDirection.z *= -1f;

        Vector3 worldFacingDirection = transform.TransformDirection(localFacingDirection);
        worldFacingDirection.y = 0f;
        return worldFacingDirection.sqrMagnitude > 0.0001f ? worldFacingDirection.normalized : transform.forward;
    }

    private Vector3 ResolveApproachReferencePoint(Vector3 characterPosition)
    {
        Collider colliderComponent = GetComponent<Collider>();
        if (colliderComponent != null)
        {
            Vector3 closestPoint = colliderComponent.ClosestPoint(characterPosition);
            if ((closestPoint - characterPosition).sqrMagnitude > 0.000001f)
                return closestPoint;
        }

        return transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveReceiver(other, out ICharacterInteractionVolumeReceiver receiver))
            return;

        if (_overlappingReceivers.Add(receiver))
            receiver.RegisterInteractionVolume(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryResolveReceiver(other, out ICharacterInteractionVolumeReceiver receiver))
            return;

        if (_overlappingReceivers.Remove(receiver))
            receiver.UnregisterInteractionVolume(this);
    }

    private void OnDisable()
    {
        foreach (ICharacterInteractionVolumeReceiver receiver in _overlappingReceivers)
        {
            if (receiver != null)
                receiver.UnregisterInteractionVolume(this);
        }

        _overlappingReceivers.Clear();
    }

    private bool TryResolveReceiver(Collider other, out ICharacterInteractionVolumeReceiver receiver)
    {
        receiver = null;
        if (other == null)
            return false;

        if ((receiverLayerMask.value & (1 << other.gameObject.layer)) == 0)
            return false;

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour is ICharacterInteractionVolumeReceiver typedReceiver)
            {
                receiver = typedReceiver;
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Vector3 startPosition = transform.position;
        Vector3 positiveSideEndPosition = ResolveVaultEndPosition(true);
        Vector3 negativeSideEndPosition = ResolveVaultEndPosition(false);

        Gizmos.color = allowsCover ? Color.green : new Color(0.2f, 0.5f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(startPosition, 0.2f);

        Gizmos.color = allowsVault ? Color.cyan : new Color(0.2f, 0.5f, 0.5f, 0.5f);
        Gizmos.DrawLine(startPosition, positiveSideEndPosition);
        Gizmos.DrawWireSphere(positiveSideEndPosition, 0.18f);

        Gizmos.color = allowsVault ? new Color(0.1f, 0.8f, 1f, 0.65f) : new Color(0.2f, 0.5f, 0.5f, 0.35f);
        Gizmos.DrawLine(startPosition, negativeSideEndPosition);
        Gizmos.DrawWireSphere(negativeSideEndPosition, 0.18f);
    }
}
