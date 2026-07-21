using System.IO;
using System.Collections.Generic;
using BehaviorCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class EnemyAISampleBuilderMenu
{
    private const string DefaultSourceConfigPath =
        "Assets/ScriptableObjects/Unit Asset Information/瀹囨辰鐜茬罕/CH0167.asset";
    private const string DefaultSourcePrefabPath =
        "Assets/Prefabs/Character/CH0167.prefab";
    private const string EnemyConfigFolder =
        "Assets/ScriptableObjects/Enemy/RuntimeSamples";
    private const string EnemyPrefabFolder =
        "Assets/Prefabs/Enemy/RuntimeSamples";
    private const string EnemyConfigPath =
        EnemyConfigFolder + "/Enemy_AI_Sample_CH0167.asset";
    private const string EnemyPrefabPath =
        EnemyPrefabFolder + "/Enemy_AI_Sample_CH0167.prefab";
    private const string EnemySampleName = "Enemy_AI_Sample_CH0167";
    private const int EnemyCharacterId = 900167;

    [MenuItem("MiniatureGarden/Enemy/Build AI Sample")]
    private static void BuildAiSample()
    {
        UnitAssetInformation sourceConfig =
            AssetDatabase.LoadAssetAtPath<UnitAssetInformation>(DefaultSourceConfigPath);
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourcePrefabPath);
        if (sourceConfig == null)
        {
            Debug.LogError($"Enemy AI Sample Builder 找不到源配置：{DefaultSourceConfigPath}");
            return;
        }

        if (sourcePrefab == null)
        {
            Debug.LogError($"Enemy AI Sample Builder 找不到源预制体：{DefaultSourcePrefabPath}");
            return;
        }

        EnemyAuthoringUtility.EnsureFolder(EnemyConfigFolder);
        EnemyAuthoringUtility.EnsureFolder(EnemyPrefabFolder);

        UnitAssetInformation enemyConfig = EnsureEnemyConfig(sourceConfig);
        GameObject sceneSample = EnsureSceneSample(sourcePrefab, enemyConfig);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            $"Enemy AI 样本构建完成。\nConfig={EnemyConfigPath}\nPrefab={EnemyPrefabPath}\nSceneObject={(sceneSample != null ? sceneSample.name : "<null>")}",
            sceneSample);
    }

    [MenuItem("MiniatureGarden/Enemy/Build AI Sample", true)]
    private static bool ValidateBuildAiSample()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static UnitAssetInformation EnsureEnemyConfig(UnitAssetInformation sourceConfig)
    {
        return EnemyAuthoringUtility.EnsureEnemyConfigCopy(EnemyConfigPath, sourceConfig, EnemyCharacterId);
    }

    private static GameObject EnsureSceneSample(GameObject sourcePrefab, UnitAssetInformation enemyConfig)
    {
        GameObject sample = FindSceneObjectByName(EnemySampleName);
        if (sample == null)
        {
            sample = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (sample == null)
                throw new IOException("实例化敌人样本失败。");
        }

        sample.name = EnemySampleName;
        sample.SetActive(true);
        SetTransformToSpawn(sample.transform);
        EnemyAuthoringUtility.ConfigureEnemyObject(sample, enemyConfig, true);

        PrefabUtility.SaveAsPrefabAssetAndConnect(sample, EnemyPrefabPath, InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(sample);
        return sample;
    }

    private static void SetTransformToSpawn(Transform sampleTransform)
    {
        if (sampleTransform == null)
            return;

        Vector3 spawnPosition = ResolveSpawnPosition();
        sampleTransform.SetPositionAndRotation(spawnPosition, ResolveSpawnRotation(spawnPosition));
    }

    private static Vector3 ResolveSpawnPosition()
    {
        GameObject player = GameObject.Find("Player");
        Vector3 fallback = player != null
            ? player.transform.position + new Vector3(5f, 0f, 3f)
            : new Vector3(6.5f, 0f, 5.5f);
        return fallback;
    }

    private static Quaternion ResolveSpawnRotation(Vector3 spawnPosition)
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return Quaternion.identity;

        Vector3 forward = player.transform.position - spawnPosition;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject directMatch = GameObject.Find(objectName);
        if (directMatch != null)
            return directMatch;

        Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }

}
