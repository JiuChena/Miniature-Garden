using BehaviorCore;
using CoreFramework;
using UnityEngine;

/// <summary>
/// 项目侧对 BehaviorCore 运行时事件的桥接实现。
/// </summary>
public class CharacterBehaviorEventReceiver : IBehaviorEventReceiver
{
    private static readonly System.Collections.Generic.HashSet<int> MissingProjectileLaunchWarnings =
        new System.Collections.Generic.HashSet<int>();

    public void SpawnVFX(int unitId, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float autoRecycleTime)
    {
        if (prefab == null)
            return;

        VFXPool.Instance.Spawn(unitId, prefab, position, rotation, scale, autoRecycleTime);
    }

    public int PlayAudio(AudioClip clip, Vector3 position, bool loop, float volume = 1f)
    {
        return AudioManager.Instance.Play(clip, CoreFramework.AudioType.Sound, position, loop, volume);
    }

    public void StopAudio(int audioHandle)
    {
        AudioManager.Instance.Stop(audioHandle);
    }

    public void SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation, IBehaviorUnit ownerData,
        float damageMultiplier, string numericKey, int targetingScopeId)
    {
        if (prefab == null)
            return;

        StatusData ownerStatusData = ownerData as StatusData;
        if (ownerStatusData == null)
            return;

        Vector3 originalPosition = position;
        Quaternion originalRotation = rotation;
        GameObject projectile = ObjectsPool.Instance.Get(prefab);

        Vector3 fallbackDirection = rotation * Vector3.forward;
        ProjectileTargetingResult targetingResult = default;
        bool hasTargeting = CharacterTargetingUtility.TryResolveProjectileTargeting(ownerStatusData, position, fallbackDirection,
            out targetingResult, targetingScopeId);

        if (hasTargeting && !targetingResult.usesLockedSnapshot)
        {
            ReorientOwnerAndRemapLaunchPose(ownerStatusData, targetingResult.launchDirection, ref position, ref rotation,
                originalPosition, originalRotation);

            Vector3 remappedDirection = targetingResult.aimPoint - position;
            if (remappedDirection.sqrMagnitude > 0.0001f)
            {
                targetingResult.launchDirection =
                    CharacterTargetingUtility.ResolveConstrainedDirection(rotation * Vector3.forward, remappedDirection);
            }
        }

        projectile.transform.position = position;
        projectile.transform.rotation = rotation;

        ProjectileLaunchContext context = new ProjectileLaunchContext
        {
            ownerData = ownerStatusData,
            damageMultiplier = damageMultiplier,
            numericKey = numericKey,
            position = position,
            rotation = rotation,
            defaultSpeed = 10f,
            hasTarget = hasTargeting,
            targetTransform = targetingResult.targetTransform,
            aimPoint = hasTargeting ? targetingResult.aimPoint : position + fallbackDirection,
            launchDirection = hasTargeting ? targetingResult.launchDirection : fallbackDirection,
        };

        IProjectileLaunchHandler launchHandler = projectile.GetComponent<IProjectileLaunchHandler>();
        if (launchHandler == null)
        {
            if (MissingProjectileLaunchWarnings.Add(prefab.GetInstanceID()))
            {
                Debug.LogError(
                    $"Projectile prefab '{prefab.name}' 缺少 IProjectileLaunchHandler。当前项目的投射物事件要求预制体直接挂载可发射组件，" +
                    "通常应由 ProjectileBase 或其子类提供该能力。",
                    prefab);
            }

            ObjectsPool.Instance.Put(projectile);
            return;
        }

        launchHandler.Launch(context);
    }

    public void ApplyEffect(GameObject target, BehaviorEffectAsset effectDefinition, GameObject source)
    {
        EffectDefinitionSO typedEffectDefinition = effectDefinition as EffectDefinitionSO;
        if (target == null || typedEffectDefinition == null)
            return;

        UnitEffectController sourceController = ResolveEffectController(source);
        UnitEffectController targetController = ResolveEffectController(target);
        if (sourceController == null || targetController == null)
            return;

        sourceController.ApplyEffect(typedEffectDefinition, targetController, target.transform.position);
    }

    public void ExecuteEffect(BehaviorEffectAsset effectDefinition, IBehaviorUnit ownerData, Vector3 origin, GameObject source)
    {
        EffectDefinitionSO typedEffectDefinition = effectDefinition as EffectDefinitionSO;
        if (typedEffectDefinition == null || ownerData == null)
            return;

        UnitEffectController sourceController = ResolveEffectController(source);
        if (sourceController == null)
            sourceController = ResolveEffectController(ownerData.RuntimeGameObject);

        if (sourceController == null)
            return;

        sourceController.ApplyEffect(typedEffectDefinition, null, origin);
    }

    public void ShakeCamera(float amplitude, float frequency, float duration)
    {
        if (PlayerCameraController.Instance != null)
            PlayerCameraController.Instance.PlayShake(amplitude, frequency, duration);
    }

    public float CalculateDamage(IBehaviorUnit attacker, IBehaviorUnit defender, float multiplier, string numericKey)
    {
        return ProjectileDamageResolver.CalculateDamage(attacker as StatusData, defender as StatusData, multiplier, numericKey);
    }

    private static UnitEffectController ResolveEffectController(GameObject targetObject)
    {
        if (targetObject == null)
            return null;

        if (targetObject.TryGetComponent(out UnitEffectController controller))
            return controller;

        return UnitCombatResolver.ResolveEffectController(targetObject.transform);
    }

    private static void ReorientOwnerAndRemapLaunchPose(StatusData ownerData, Vector3 launchDirection,
        ref Vector3 worldPosition, ref Quaternion worldRotation, Vector3 originalWorldPosition, Quaternion originalWorldRotation)
    {
        if (ownerData == null || launchDirection.sqrMagnitude <= 0.0001f)
            return;

        Transform ownerTransform = ownerData.transform;
        Vector3 flatDirection = launchDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 localPosition = ownerTransform.InverseTransformPoint(originalWorldPosition);
        Quaternion localRotation = Quaternion.Inverse(ownerTransform.rotation) * originalWorldRotation;
        if (!CharacterTargetingUtility.TryFaceDirection(ownerTransform, flatDirection))
            return;

        worldPosition = ownerTransform.TransformPoint(localPosition);
        worldRotation = ownerTransform.rotation * localRotation;
    }
}
