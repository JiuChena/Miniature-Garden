using System.Collections.Generic;
using BehaviorCore;
using UnityEngine;

#if UNITY_EDITOR
internal static class UnitBehaviorValidationMenu
{
    [UnityEditor.MenuItem("MiniatureGarden/Behavior/Validate Unit Behavior Assets")]
    private static void ValidateAllUnitBehaviorAssets()
    {
        int unitAssetCount = 0;
        int behaviorClipCount = 0;
        int issueCount = 0;

        string[] unitGuids = UnityEditor.AssetDatabase.FindAssets("t:UnitAssetInformation");
        for (int i = 0; i < unitGuids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(unitGuids[i]);
            UnitAssetInformation config =
                UnityEditor.AssetDatabase.LoadAssetAtPath<UnitAssetInformation>(assetPath);
            if (config == null)
                continue;

            unitAssetCount++;
            List<string> issues = new List<string>();
            config.CollectValidationIssues(issues);
            issueCount += issues.Count;

            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                Debug.LogWarning($"[UnitConfig Validation] {assetPath} -> {issues[issueIndex]}", config);
        }

        string[] behaviorGuids = UnityEditor.AssetDatabase.FindAssets("t:BehaviorClip");
        for (int i = 0; i < behaviorGuids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(behaviorGuids[i]);
            BehaviorClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<BehaviorClip>(assetPath);
            if (clip == null)
                continue;

            behaviorClipCount++;
            List<string> issues = new List<string>();
            clip.CollectValidationIssues(issues);
            issueCount += issues.Count;

            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                Debug.LogWarning($"[BehaviorClip Validation] {assetPath} -> {issues[issueIndex]}", clip);
        }

        Debug.Log(
            $"Unit/Behavior 资源校验完成。UnitConfig={unitAssetCount}, BehaviorClip={behaviorClipCount}, Issues={issueCount}");
    }
}
#endif
