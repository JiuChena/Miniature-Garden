using System;
using System.Collections.Generic;
using System.Text;
using BehaviorCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class EnemySceneMigrationMenu
{
    private const string GeneratedConfigFolder = "Assets/ScriptableObjects/Enemy/Generated";
    private const string DefaultSourceConfigPath =
        "Assets/ScriptableObjects/Unit Asset Information/瀹囨辰鐜茬罕/CH0167.asset";
    private const string DefaultGeneratedConfigPath = GeneratedConfigFolder + "/LegacyEnemy_Default.asset";
    private const string EnemyPrefabFolderPrefix = "Assets/Prefabs/Enemy/";

    [MenuItem("MiniatureGarden/Enemy/Upgrade Legacy Scene Enemies")]
    private static void UpgradeLegacySceneEnemies()
    {
        List<GameObject> legacyEnemies = CollectLegacySceneEnemies();
        if (legacyEnemies.Count == 0)
        {
            Debug.LogWarning("当前场景没有找到可迁移的旧版敌人对象。");
            return;
        }

        EnemyAuthoringUtility.EnsureFolder(GeneratedConfigFolder);
        Dictionary<UnitAssetInformation, UnitAssetInformation> enemyConfigBySource =
            new Dictionary<UnitAssetInformation, UnitAssetInformation>();

        UnitAssetInformation fallbackEnemyConfig = null;
        int upgradedCount = 0;
        int skippedCount = 0;
        for (int i = 0; i < legacyEnemies.Count; i++)
        {
            GameObject target = legacyEnemies[i];
            if (!ShouldUpgrade(target))
            {
                skippedCount++;
                continue;
            }

            CharacterDriver legacyDriver = target.GetComponent<CharacterDriver>();
            UnitAssetInformation sourceConfig = legacyDriver != null ? legacyDriver.Config : null;

            UnitAssetInformation enemyConfig;
            if (sourceConfig != null)
            {
                if (!enemyConfigBySource.TryGetValue(sourceConfig, out enemyConfig))
                {
                    string assetBaseName = EnemyAuthoringUtility.SanitizeAssetName(sourceConfig.name);
                    string configPath = $"{GeneratedConfigFolder}/{assetBaseName}_Enemy.asset";
                    enemyConfig = EnemyAuthoringUtility.EnsureEnemyConfigCopy(configPath, sourceConfig,
                        ResolveEnemyCharacterId(sourceConfig.characterId));
                    enemyConfigBySource[sourceConfig] = enemyConfig;
                }

            }
            else
            {
                fallbackEnemyConfig ??= ResolveFallbackEnemyConfig();
                if (fallbackEnemyConfig == null)
                {
                    Debug.LogWarning($"[{target.name}] 找不到默认敌人配置或 AI 配置，已跳过。", target);
                    skippedCount++;
                    continue;
                }

                enemyConfig = fallbackEnemyConfig;
            }

            target = EnemyAuthoringUtility.EnsureAnimatorReadyEnemyObject(target);
            EnemyAuthoringUtility.ConfigureEnemyObject(target, enemyConfig, true);
            upgradedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Scene activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        bool sceneSaved = TrySaveActiveScene(activeScene);

        Debug.Log($"旧版场景敌人迁移完成。升级 {upgradedCount} 个，跳过 {skippedCount} 个。场景保存结果：{sceneSaved}。");
    }

    [MenuItem("MiniatureGarden/Enemy/Upgrade Legacy Scene Enemies", true)]
    private static bool ValidateUpgradeLegacySceneEnemies()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("MiniatureGarden/Enemy/Validate Runtime Chain In Scene")]
    private static void ValidateRuntimeChainInScene()
    {
        List<GameObject> enemyObjects = CollectSceneEnemyCandidates(includeUpgraded: true);
        if (enemyObjects.Count == 0)
        {
            Debug.LogWarning("当前场景没有找到任何敌人候选对象，无法执行运行主链校验。");
            return;
        }

        int readyCount = 0;
        int issueCount = 0;
        List<string> detailedIssues = new List<string>(enemyObjects.Count * 2);
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"敌人运行主链静态校验开始。Scene={SceneManager.GetActiveScene().name} | Count={enemyObjects.Count}");

        for (int i = 0; i < enemyObjects.Count; i++)
        {
            GameObject target = enemyObjects[i];
            int issueBefore = issueCount;
            AppendValidationEntry(builder, target, ref issueCount, detailedIssues);
            if (issueCount == issueBefore)
                readyCount++;
        }

        builder.AppendLine($"校验完成。Ready={readyCount} | WithIssues={enemyObjects.Count - readyCount} | TotalIssues={issueCount}");
        if (issueCount > 0)
        {
            Debug.LogWarning($"敌人运行主链静态校验发现问题。Scene={SceneManager.GetActiveScene().name} | Ready={readyCount} | WithIssues={enemyObjects.Count - readyCount} | TotalIssues={issueCount}");
            for (int i = 0; i < detailedIssues.Count; i++)
                Debug.LogWarning(detailedIssues[i]);
            Debug.LogWarning(builder.ToString());
        }
        else
        {
            Debug.Log($"敌人运行主链静态校验通过。Scene={SceneManager.GetActiveScene().name} | Ready={readyCount} | Total={enemyObjects.Count}");
            Debug.Log(builder.ToString());
        }
    }

    [MenuItem("MiniatureGarden/Enemy/Validate Runtime Chain In Scene", true)]
    private static bool ValidateRuntimeChainInSceneMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("MiniatureGarden/Enemy/Normalize Existing Scene Enemies")]
    private static void NormalizeExistingSceneEnemies()
    {
        List<GameObject> enemyObjects = CollectSceneEnemyCandidates(includeUpgraded: true);
        if (enemyObjects.Count == 0)
        {
            Debug.LogWarning("当前场景没有找到任何敌人候选对象，无法执行归一化。");
            return;
        }

        int normalizedCount = 0;
        int skippedCount = 0;
        for (int i = 0; i < enemyObjects.Count; i++)
        {
            GameObject target = enemyObjects[i];
            EnemyDriver enemyDriver = target.GetComponent<EnemyDriver>();
            if (enemyDriver == null || enemyDriver.Config == null)
            {
                skippedCount++;
                continue;
            }

            UnitAssetInformation enemyConfig = enemyDriver.Config;

            target = EnemyAuthoringUtility.EnsureAnimatorReadyEnemyObject(target);
            EnemyAuthoringUtility.NormalizeEnemyConfigInPlace(enemyConfig);
            EnemyAuthoringUtility.ConfigureEnemyObject(target, enemyConfig, true);
            normalizedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Scene activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        bool sceneSaved = TrySaveActiveScene(activeScene);

        Debug.Log($"当前场景敌人归一化完成。处理 {normalizedCount} 个，跳过 {skippedCount} 个。场景保存结果：{sceneSaved}。");
    }

    [MenuItem("MiniatureGarden/Enemy/Normalize Existing Scene Enemies", true)]
    private static bool ValidateNormalizeExistingSceneEnemies()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static List<GameObject> CollectLegacySceneEnemies()
    {
        return CollectSceneEnemyCandidates(includeUpgraded: false);
    }

    private static List<GameObject> CollectSceneEnemyCandidates(bool includeUpgraded)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        CharacterController[] controllers = UnityEngine.Object.FindObjectsOfType<CharacterController>(true);
        EnemyDriver[] enemyDrivers = UnityEngine.Object.FindObjectsOfType<EnemyDriver>(true);
        List<GameObject> results = new List<GameObject>(controllers.Length + enemyDrivers.Length);
        HashSet<int> seenInstanceIds = new HashSet<int>();

        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController controller = controllers[i];
            if (controller == null)
                continue;

            GameObject target = controller.gameObject;
            if (target == null || target.scene != activeScene)
                continue;

            if (!seenInstanceIds.Add(target.GetInstanceID()))
                continue;

            if (!IsSceneEnemyCandidate(target))
                continue;

            if (!includeUpgraded && target.GetComponent<EnemyDriver>() != null)
                continue;

            results.Add(target);
        }

        for (int i = 0; i < enemyDrivers.Length; i++)
        {
            EnemyDriver enemyDriver = enemyDrivers[i];
            if (enemyDriver == null)
                continue;

            GameObject target = enemyDriver.gameObject;
            if (target == null || target.scene != activeScene)
                continue;

            if (!seenInstanceIds.Add(target.GetInstanceID()))
                continue;

            if (!IsSceneEnemyCandidate(target))
                continue;

            if (!includeUpgraded && target.GetComponent<EnemyDriver>() != null)
                continue;

            results.Add(target);
        }

        return results;
    }

    private static bool ShouldUpgrade(GameObject target)
    {
        return IsSceneEnemyCandidate(target) && target.GetComponent<EnemyDriver>() == null;
    }

    private static bool IsSceneEnemyCandidate(GameObject target)
    {
        if (target == null || !target.scene.IsValid())
            return false;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
        if (!string.IsNullOrEmpty(prefabPath) &&
            prefabPath.StartsWith("Assets/Prefabs/Character/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        bool enemyByLayer = enemyLayer >= 0 && target.layer == enemyLayer;
        bool enemyByName = target.name.StartsWith("Enemy_", StringComparison.OrdinalIgnoreCase);
        bool enemyByPrefab = !string.IsNullOrEmpty(prefabPath) &&
                             prefabPath.StartsWith(EnemyPrefabFolderPrefix, StringComparison.OrdinalIgnoreCase);

        CharacterDriver legacyDriver = target.GetComponent<CharacterDriver>();
        bool enemyByConfig = legacyDriver != null &&
                             legacyDriver.Config != null &&
                             legacyDriver.Config.UnitAlignment == UnitAlignment.Enemy;

        if (!enemyByLayer && !enemyByName && !enemyByPrefab && !enemyByConfig)
            return false;

        return target.GetComponent<CharacterController>() != null;
    }

    private static void AppendValidationEntry(StringBuilder builder, GameObject target, ref int issueCount,
        List<string> detailedIssues)
    {
        builder.Append($"- {target.name}");

        List<string> issues = new List<string>(8);
        EnemyDriver enemyDriver = target.GetComponent<EnemyDriver>();
        CharacterController characterController = target.GetComponent<CharacterController>();
        UnityEngine.AI.NavMeshAgent navMeshAgent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        BehaviorInterpreter interpreter = target.GetComponent<BehaviorInterpreter>();
        StatusData statusData = target.GetComponent<StatusData>();
        NonPlayerTargetingModule targeting = target.GetComponent<NonPlayerTargetingModule>();
        EnemyBrainModule brain = target.GetComponent<EnemyBrainModule>();
        EnemyNavigationModule navigation = target.GetComponent<EnemyNavigationModule>();
        Animator animator = target.GetComponentInChildren<Animator>(true);

        if (enemyDriver == null)
            issues.Add("缺少 EnemyDriver");
        if (characterController == null)
            issues.Add("缺少 CharacterController");
        if (navMeshAgent == null)
            issues.Add("缺少 NavMeshAgent");
        if (interpreter == null)
            issues.Add("缺少 BehaviorInterpreter");
        if (statusData == null)
            issues.Add("缺少 StatusData");
        if (targeting == null)
            issues.Add("缺少 UnitTargetingModule");
        if (brain == null)
            issues.Add("缺少 EnemyBrainModule");
        if (navigation == null)
            issues.Add("缺少 EnemyNavigationModule");
        if (animator == null)
            issues.Add("子节点缺少 Animator");
        else if (animator.runtimeAnimatorController == null)
            issues.Add("Animator 缺少 RuntimeAnimatorController");

        UnitAssetInformation config = enemyDriver != null ? enemyDriver.Config : null;
        if (config == null)
        {
            issues.Add("EnemyDriver 未绑定 UnitAssetInformation");
        }
        else
        {
            if (!config.HasBehavior(BehaviorKeys.Idle))
                issues.Add("配置缺少 Idle 行为");
            if (!config.HasBehavior(BehaviorKeys.Move))
                issues.Add("配置缺少 Move 行为");
            if (config.SupportsAttack && !config.HasAnyAttackStartBehavior())
                issues.Add("配置声明支持 Attack，但缺少 AttackStart/AttackLoop/Attack");
            if (config.SupportsJump && !config.HasBehavior(BehaviorKeys.MoveJump))
                issues.Add("配置声明支持 Jump，但缺少 MoveJump 行为");
            if (config.HasBehavior(BehaviorKeys.Load))
            {
                BehaviorClip loadClip = config.GetBehavior(BehaviorKeys.Load);
                if (loadClip == null || loadClip.wrapMode != WrapMode.Once)
                    issues.Add("Load 行为存在但不是 WrapMode.Once");
            }
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0 && target.layer != enemyLayer)
            issues.Add("根对象不在 Enemy 层");

        if (issues.Count == 0)
        {
            builder.AppendLine(" | Ready");
            return;
        }

        issueCount += issues.Count;
        builder.Append(" | Issues: ");
        for (int i = 0; i < issues.Count; i++)
        {
            if (i > 0)
                builder.Append("；");
            builder.Append(issues[i]);
            detailedIssues?.Add($"[{target.name}] {issues[i]}");
        }

        builder.AppendLine();
    }

    private static UnitAssetInformation ResolveFallbackEnemyConfig()
    {
        UnitAssetInformation existing =
            AssetDatabase.LoadAssetAtPath<UnitAssetInformation>(DefaultGeneratedConfigPath);
        if (existing != null)
            return existing;

        UnitAssetInformation sourceConfig =
            AssetDatabase.LoadAssetAtPath<UnitAssetInformation>(DefaultSourceConfigPath);
        if (sourceConfig == null)
        {
            Debug.LogError($"Upgrade Legacy Scene Enemies 找不到默认源配置：{DefaultSourceConfigPath}");
            return null;
        }

        return EnemyAuthoringUtility.EnsureEnemyConfigCopy(DefaultGeneratedConfigPath, sourceConfig,
            ResolveEnemyCharacterId(sourceConfig.characterId));
    }

    private static int ResolveEnemyCharacterId(int sourceCharacterId)
    {
        int safeSourceId = Mathf.Abs(sourceCharacterId);
        if (safeSourceId <= 0)
            safeSourceId = 1;

        return 900000 + (safeSourceId % 99999);
    }

    private static bool TrySaveActiveScene(Scene activeScene)
    {
        if (!activeScene.IsValid())
            return false;

        if (string.IsNullOrEmpty(activeScene.path))
        {
            Debug.LogWarning("当前激活场景没有保存路径，已标记 Dirty 但无法自动保存。");
            return false;
        }

        if (!activeScene.isDirty)
            return true;

        bool saved = EditorSceneManager.SaveScene(activeScene);
        if (!saved)
            Debug.LogWarning($"自动保存场景失败：{activeScene.path}");

        return saved;
    }
}
