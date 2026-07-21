using System;
using System.Collections.Generic;
using System.IO;
using BehaviorCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

internal static class EnemyAuthoringUtility
{
    private const string LegacyVaultBehaviorKey = "Vault";
    private const string EnemyPrefabRoot = "Assets/Prefabs/Enemy";
    private const string DefaultAnimatorControllerPath = "Assets/Animations/General.controller";
    private const string FallbackBehaviorControllerPath =
        BehaviorAnimatorControllerConvention.DefaultSharedControllerFolder + "/" +
        BehaviorAnimatorControllerConvention.DefaultSharedControllerName + ".controller";

    public static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Enemy";

        string sanitized = value.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
            sanitized = sanitized.Replace(invalidChars[i], '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "Enemy" : sanitized;
    }

    public static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    public static UnitAssetInformation EnsureEnemyConfigCopy(string targetConfigPath,
        UnitAssetInformation sourceConfig, int characterId)
    {
        if (sourceConfig == null)
            throw new ArgumentNullException(nameof(sourceConfig));

        EnsureFolder(Path.GetDirectoryName(targetConfigPath)?.Replace('\\', '/') ?? "Assets");
        if (!File.Exists(targetConfigPath))
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceConfig), targetConfigPath))
                throw new IOException($"复制敌人配置失败：{targetConfigPath}");
        }

        AssetDatabase.ImportAsset(targetConfigPath, ImportAssetOptions.ForceSynchronousImport);
        UnitAssetInformation enemyConfig = AssetDatabase.LoadAssetAtPath<UnitAssetInformation>(targetConfigPath);
        if (enemyConfig == null)
            throw new IOException($"加载敌人配置失败：{targetConfigPath}");

        EditorUtility.CopySerialized(sourceConfig, enemyConfig);
        enemyConfig.name = Path.GetFileNameWithoutExtension(targetConfigPath);
        enemyConfig.characterId = characterId;
        NormalizeEnemyConfigInPlace(enemyConfig);
        return enemyConfig;
    }

    public static void NormalizeEnemyConfigInPlace(UnitAssetInformation enemyConfig)
    {
        if (enemyConfig == null)
            return;

        enemyConfig.unitAlignment = UnitAlignment.Enemy;
        enemyConfig.moveSpeed = Mathf.Max(0.1f, enemyConfig.moveSpeed);
        enemyConfig.conditionSourceAsset = null;
        enemyConfig.transitionPolicyAsset = null;
        enemyConfig.attackResolverAsset = null;

        int playerLayer = LayerMask.NameToLayer("Player");
        enemyConfig.hitboxTargetLayers = playerLayer >= 0 ? (1 << playerLayer) : ~0;

        List<BehaviorEntry> entries = enemyConfig.behaviors != null
            ? new List<BehaviorEntry>(enemyConfig.behaviors)
            : new List<BehaviorEntry>();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BehaviorEntry entry = entries[i];
            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            if (string.Equals(entry.key, BehaviorKeys.Load, StringComparison.Ordinal))
            {
                BehaviorClip loadClip = entry.clips != null && entry.clips.Length > 0 ? entry.clips[0] : null;
                bool keepLoad = loadClip != null && loadClip.wrapMode == WrapMode.Once;
                if (!keepLoad)
                    entries.RemoveAt(i);

                continue;
            }

            if (string.Equals(entry.key, LegacyVaultBehaviorKey, StringComparison.Ordinal))
                entries.RemoveAt(i);
        }

        enemyConfig.behaviors = entries.ToArray();
        EditorUtility.SetDirty(enemyConfig);
    }

    public static void ConfigureEnemyObject(GameObject target, UnitAssetInformation enemyConfig,
        bool stripCharacterRuntime)
    {
        if (target == null)
            return;

        if (stripCharacterRuntime)
        {
            RemoveComponent<CharacterDriver>(target);
            RemoveComponent<CharacterDebugModule>(target);
            RemoveComponent<GroundMovement>(target);
        }

        RemoveComponent<UnitTargetingModule>(target);

        EnsureCharacterController(target);
        EnsureAnimatorComponent(target);
        GetOrAddComponent<BehaviorInterpreter>(target);
        GetOrAddComponent<UnitEffectController>(target);
        StatusData statusData = GetOrAddComponent<StatusData>(target);
        ConfigureStatusData(statusData);

        NonPlayerTargetingModule targetingModule = GetOrAddComponent<NonPlayerTargetingModule>(target);
        ConfigureNonPlayerTargeting(targetingModule);

        EnemyBrainModule brainModule = GetOrAddComponent<EnemyBrainModule>(target);
        ConfigureEnemyBrain(brainModule);

        GetOrAddComponent<EnemyNavigationModule>(target);
        EnemyDriver enemyDriver = GetOrAddComponent<EnemyDriver>(target);
        ConfigureEnemyDriver(enemyDriver, enemyConfig);
        ConfigureNavMeshAgent(target, enemyConfig);
        ApplyEnemyLayer(target);
        AlignObjectToNavMesh(target.transform);
    }

    public static GameObject EnsureAnimatorReadyEnemyObject(GameObject target)
    {
        if (target == null)
            return null;

        if (target.GetComponentInChildren<Animator>(true) != null)
            return target;

        string prefabPath = ResolveMatchingEnemyPrefabPath(target.name);
        if (string.IsNullOrEmpty(prefabPath))
            return target;

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
            return target;

        GameObject replacement = ReplaceSceneObjectWithPrefab(target, prefabAsset);
        return replacement != null ? replacement : target;
    }

    public static void ConfigureStatusData(StatusData statusData)
    {
        if (statusData == null)
            return;

        SerializedObject serializedObject = new SerializedObject(statusData);
        SerializedProperty isTargetableProperty = serializedObject.FindProperty("isTargetable");
        if (isTargetableProperty != null)
            isTargetableProperty.boolValue = true;

        SerializedProperty fallbackAlignmentProperty = serializedObject.FindProperty("fallbackUnitAlignment");
        if (fallbackAlignmentProperty != null)
            fallbackAlignmentProperty.enumValueIndex = (int)UnitAlignment.Enemy;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(statusData);
    }

    public static void ConfigureNonPlayerTargeting(NonPlayerTargetingModule targetingModule)
    {
        if (targetingModule == null)
            return;

        int playerLayer = LayerMask.NameToLayer("Player");
        SetSerializedInt(targetingModule, "targetLayers", playerLayer >= 0 ? (1 << playerLayer) : ~0);
        SetSerializedBool(targetingModule, "includeTriggerColliders", true);
        SetSerializedBool(targetingModule, "requireEnemyAlignment", true);
        SetSerializedBool(targetingModule, "allowNeutralTargets", false);
    }

    public static void ConfigureEnemyDriver(EnemyDriver enemyDriver, UnitAssetInformation enemyConfig)
    {
        if (enemyDriver == null)
            return;

        SetSerializedObjectReference(enemyDriver, "config", enemyConfig);
        SetSerializedBool(enemyDriver, "startOnAwake", true);
        SetSerializedBool(enemyDriver, "autoTick", true);
        SetSerializedInt(enemyDriver, "unitLevel", 1);
        SetSerializedInt(enemyDriver, "normalAttackLevel", 1);
        SetSerializedInt(enemyDriver, "talentLevel", 1);
        SetSerializedInt(enemyDriver, "burstLevel", 1);
    }

    public static void ConfigureEnemyBrain(EnemyBrainModule brainModule)
    {
        if (brainModule == null)
            return;

        SetSerializedFloat(brainModule, "targetRefreshInterval", 0.2f);
        SetSerializedFloat(brainModule, "targetLostDelay", 1f);
        SetSerializedFloat(brainModule, "attackRange", 5.5f);
        SetSerializedFloat(brainModule, "attackRangeHysteresis", 0.4f);
        SetSerializedBool(brainModule, "enableCrouchDecision", false);
        SetSerializedFloat(brainModule, "crouchDistanceMin", 2f);
        SetSerializedFloat(brainModule, "crouchDistanceMax", 8f);
        SetSerializedBool(brainModule, "enableVaultDecision", true);
        SetSerializedFloat(brainModule, "vaultDecisionCooldown", 0.75f);
    }

    public static void ConfigureNavMeshAgent(GameObject target, UnitAssetInformation enemyConfig)
    {
        if (target == null)
            return;

        CharacterController controller = target.GetComponent<CharacterController>();
        NavMeshAgent agent = GetOrAddComponent<NavMeshAgent>(target);
        if (controller != null)
        {
            agent.radius = controller.radius;
            agent.height = controller.height;
        }

        agent.speed = Mathf.Max(0.1f, enemyConfig != null ? enemyConfig.moveSpeed : 4f);
        agent.acceleration = 30f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.baseOffset = 0f;
        agent.enabled = false;
        EditorUtility.SetDirty(agent);
    }

    public static void ApplyEnemyLayer(GameObject target)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0 || target == null)
            return;

        Transform[] transforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = enemyLayer;
    }

    public static void AlignObjectToNavMesh(Transform targetTransform)
    {
        if (targetTransform == null)
            return;

        if (!NavMesh.SamplePosition(targetTransform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            return;

        targetTransform.position = hit.position;
    }

    private static void EnsureCharacterController(GameObject target)
    {
        if (target == null)
            return;

        CharacterController controller = GetOrAddComponent<CharacterController>(target);
        if (controller.height <= 0.01f)
            controller.height = 2f;
        if (controller.radius <= 0.01f)
            controller.radius = 0.5f;

        Vector3 center = controller.center;
        float expectedCenterY = controller.height * 0.5f;
        if (Mathf.Abs(center.y) <= 0.001f)
            center.y = expectedCenterY;

        controller.center = center;
        controller.slopeLimit = Mathf.Max(controller.slopeLimit, 45f);
        controller.stepOffset = Mathf.Max(controller.stepOffset, 0.1f);
        controller.skinWidth = Mathf.Max(controller.skinWidth, 0.08f);
        controller.minMoveDistance = Mathf.Max(controller.minMoveDistance, 0.001f);
        controller.detectCollisions = true;
        controller.enableOverlapRecovery = true;
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureAnimatorComponent(GameObject target)
    {
        if (target == null)
            return;

        Animator animator = target.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = target.AddComponent<Animator>();

        if (animator.runtimeAnimatorController == null)
        {
            RuntimeAnimatorController controller = ResolveDefaultAnimatorController();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
        }

        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
    }

    private static string ResolveMatchingEnemyPrefabPath(string sceneObjectName)
    {
        string baseName = StripCloneSuffix(sceneObjectName);
        if (string.IsNullOrWhiteSpace(baseName))
            return null;

        string exactPath = $"{EnemyPrefabRoot}/{baseName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(exactPath) != null)
            return exactPath;

        string[] searchFolders = { EnemyPrefabRoot };
        string[] prefabGuids = AssetDatabase.FindAssets($"{baseName} t:Prefab", searchFolders);
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(candidatePath))
                continue;

            string candidateName = Path.GetFileNameWithoutExtension(candidatePath);
            if (string.Equals(candidateName, baseName, System.StringComparison.OrdinalIgnoreCase))
                return candidatePath;
        }

        return null;
    }

    private static string StripCloneSuffix(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        string trimmed = objectName.Trim();
        int suffixStart = trimmed.LastIndexOf(" (", System.StringComparison.Ordinal);
        if (suffixStart < 0 || !trimmed.EndsWith(")", System.StringComparison.Ordinal))
            return trimmed;

        int numberStart = suffixStart + 2;
        int numberLength = trimmed.Length - numberStart - 1;
        if (numberLength <= 0)
            return trimmed;

        for (int i = 0; i < numberLength; i++)
        {
            if (!char.IsDigit(trimmed[numberStart + i]))
                return trimmed;
        }

        return trimmed.Substring(0, suffixStart);
    }

    private static GameObject ReplaceSceneObjectWithPrefab(GameObject target, GameObject prefabAsset)
    {
        if (target == null || prefabAsset == null)
            return null;

        Transform sourceTransform = target.transform;
        Transform parent = sourceTransform.parent;
        Scene scene = target.scene;
        Vector3 position = sourceTransform.position;
        Quaternion rotation = sourceTransform.rotation;
        Vector3 localPosition = sourceTransform.localPosition;
        Quaternion localRotation = sourceTransform.localRotation;
        Vector3 localScale = sourceTransform.localScale;
        int siblingIndex = sourceTransform.GetSiblingIndex();
        string instanceName = target.name;
        bool activeSelf = target.activeSelf;

        GameObject replacement = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
        if (replacement == null)
            return null;

        replacement.name = instanceName;
        if (parent != null)
        {
            replacement.transform.SetParent(parent, false);
            replacement.transform.SetSiblingIndex(siblingIndex);
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = localScale;
        }
        else
        {
            replacement.transform.SetPositionAndRotation(position, rotation);
            replacement.transform.localScale = localScale;
        }

        replacement.SetActive(activeSelf);
        UnityEngine.Object.DestroyImmediate(target);
        return replacement;
    }

    private static RuntimeAnimatorController ResolveDefaultAnimatorController()
    {
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultAnimatorControllerPath);
        if (controller != null)
            return controller;

        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackBehaviorControllerPath);
    }

    public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        if (!gameObject.TryGetComponent(out T component))
            component = gameObject.AddComponent<T>();

        return component;
    }

    public static void RemoveComponent<T>(GameObject gameObject) where T : Component
    {
        if (gameObject == null)
            return;

        T component = gameObject.GetComponent<T>();
        if (component != null)
            UnityEngine.Object.DestroyImmediate(component, true);
    }

    public static void SetSerializedObjectReference(UnityEngine.Object target, string propertyName,
        UnityEngine.Object value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    public static void SetSerializedBool(UnityEngine.Object target, string propertyName, bool value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    public static void SetSerializedInt(UnityEngine.Object target, string propertyName, int value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    public static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    public static T GetSerializedObjectReference<T>(UnityEngine.Object target, string propertyName)
        where T : UnityEngine.Object
    {
        if (target == null)
            return null;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return null;

        return property.objectReferenceValue as T;
    }
}
