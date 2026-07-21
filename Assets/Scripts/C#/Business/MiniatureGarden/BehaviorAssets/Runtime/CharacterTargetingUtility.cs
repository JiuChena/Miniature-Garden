using UnityEngine;

internal static class CharacterTargetingUtility
{
    public static bool TryResolveProjectileTargeting(StatusData ownerData, Vector3 spawnPosition,
        Vector3 fallbackDirection, out ProjectileTargetingResult result, int targetingScopeId = 0)
    {
        result = default;
        if (ownerData == null)
            return false;

        IUnitTargetingProvider provider = ownerData.UnitTargetingProvider;
        if (provider == null)
            return false;

        return provider.TryResolveProjectileTargeting(ownerData, spawnPosition, fallbackDirection, out result,
            targetingScopeId);
    }

    public static bool TryFaceProjectileTarget(StatusData ownerData, Transform ownerTransform, int targetingScopeId = 0)
    {
        if (ownerData == null || ownerTransform == null)
            return false;

        BattleGlobalSettingsSO settings = GlobalConfigManager.Instance.BattleSettings;
        if (!settings.rotateHostUnitTowardProjectileTarget)
            return false;

        Vector3 fallbackDirection = ownerTransform.forward;
        if (!TryResolveProjectileTargeting(ownerData, ownerTransform.position, fallbackDirection, out ProjectileTargetingResult result,
                targetingScopeId))
            return false;

        Vector3 targetDirection = result.aimPoint - ownerTransform.position;
        if (targetDirection.sqrMagnitude <= 0.0001f)
            targetDirection = result.launchDirection;

        return TryFaceDirection(ownerTransform, targetDirection);
    }

    public static bool TryFaceDirection(Transform ownerTransform, Vector3 worldDirection)
    {
        if (ownerTransform == null)
            return false;

        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
            return false;

        ownerTransform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        return true;
    }

    public static Vector3 ResolveConstrainedDirection(Vector3 fallbackDirection, Vector3 desiredDirection)
    {
        Vector3 normalizedFallback = fallbackDirection.sqrMagnitude > 0.0001f
            ? fallbackDirection.normalized
            : Vector3.forward;
        Vector3 normalizedDesired = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : normalizedFallback;

        BattleGlobalSettingsSO settings = GlobalConfigManager.Instance != null
            ? GlobalConfigManager.Instance.BattleSettings
            : null;
        float maxCorrectionAngle = settings != null
            ? Mathf.Clamp(settings.maxProjectileTargetCorrectionAngle, 0f, 180f)
            : 180f;
        if (maxCorrectionAngle >= 179.9f)
            return normalizedDesired;
        if (maxCorrectionAngle <= 0f)
            return normalizedFallback;

        float angle = Vector3.Angle(normalizedFallback, normalizedDesired);
        if (angle <= maxCorrectionAngle)
            return normalizedDesired;

        Quaternion clampedRotation = Quaternion.RotateTowards(
            Quaternion.LookRotation(normalizedFallback, Vector3.up),
            Quaternion.LookRotation(normalizedDesired, Vector3.up),
            maxCorrectionAngle);
        return clampedRotation * Vector3.forward;
    }
}
