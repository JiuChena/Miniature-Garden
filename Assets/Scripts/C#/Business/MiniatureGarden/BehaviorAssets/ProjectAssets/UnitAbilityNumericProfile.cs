using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 瑙掕壊鎶€鑳芥暟鍊煎畾涔夎〃銆?
/// </summary>
[MovedFrom(false, null, null, "CharacterAbilityNumericProfile")]
[CreateAssetMenu(fileName = "UnitAbilityNumericProfile", menuName = "MiniatureGarden/Config/Units/Unit Ability Numeric Profile")]
public class UnitAbilityNumericProfile : ScriptableObject, IUnitNumericResolver
{
    [Header("Entries")]
    [Tooltip("单位所有技能、子弹、治疗等数值条目定义。")]
    public UnitAbilityNumericEntry[] entries = Array.Empty<UnitAbilityNumericEntry>();

    private readonly Dictionary<string, UnitAbilityNumericEntry> _entryCache =
        new Dictionary<string, UnitAbilityNumericEntry>(StringComparer.Ordinal);
    private bool _entryCacheBuilt;

    public bool TryResolveValue(string key, IUnitAbilityLevelProvider levelProvider, out float value)
    {
        value = 0f;
        string normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        if (!TryGetEntry(normalizedKey, out UnitAbilityNumericEntry entry) || entry == null)
            return false;

        float[] levelValues = entry.levelValues;
        if (levelValues == null || levelValues.Length == 0)
            return false;

        int level = ResolveLevel(entry.levelGroup, levelProvider);
        int index = Mathf.Clamp(level - 1, 0, levelValues.Length - 1);
        value = levelValues[index];
        return true;
    }

    public bool ContainsKey(string key)
    {
        string normalizedKey = NormalizeKey(key);
        return !string.IsNullOrWhiteSpace(normalizedKey) && TryGetEntry(normalizedKey, out _);
    }

    public int CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return 0;

        int initialCount = issues.Count;
        HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
        if (entries == null || entries.Length == 0)
            return 0;

        for (int i = 0; i < entries.Length; i++)
        {
            UnitAbilityNumericEntry entry = entries[i];
            if (entry == null)
            {
                issues.Add($"UnitAbilityNumericEntry[{i}] 为空引用。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.key))
            {
                issues.Add($"UnitAbilityNumericEntry[{i}] 的 key 为空。");
                continue;
            }

            string normalizedKey = NormalizeKey(entry.key);
            if (!string.Equals(entry.key, normalizedKey, StringComparison.Ordinal))
                issues.Add($"UnitAbilityNumericEntry[{i}] 的 key '{entry.key}' 含有首尾空白字符，建议修正为 '{normalizedKey}'。");

            if (!seenKeys.Add(normalizedKey))
                issues.Add($"单位能力数值定义中存在重复 key：{normalizedKey}。");

            if (entry.levelValues == null || entry.levelValues.Length == 0)
                issues.Add($"单位能力数值定义 key '{normalizedKey}' 没有任何等级值。");
        }

        return issues.Count - initialCount;
    }

    private int ResolveLevel(UnitAbilityLevelGroup levelGroup, IUnitAbilityLevelProvider levelProvider)
    {
        if (levelProvider != null)
            return Mathf.Max(1, levelProvider.GetAbilityLevel(levelGroup));

        return 1;
    }

    private void EnsureCache()
    {
        if (_entryCacheBuilt && Application.isPlaying)
            return;

        _entryCache.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                UnitAbilityNumericEntry entry = entries[i];
                string normalizedKey = entry != null ? NormalizeKey(entry.key) : null;
                if (entry == null || string.IsNullOrWhiteSpace(normalizedKey))
                    continue;

                _entryCache[normalizedKey] = entry;
            }
        }

        _entryCacheBuilt = true;
    }

    private bool TryGetEntry(string normalizedKey, out UnitAbilityNumericEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        if (Application.isPlaying)
        {
            EnsureCache();
            return _entryCache.TryGetValue(normalizedKey, out entry) && entry != null;
        }

        if (entries == null || entries.Length == 0)
            return false;

        for (int i = 0; i < entries.Length; i++)
        {
            UnitAbilityNumericEntry candidate = entries[i];
            if (candidate == null)
                continue;

            string candidateKey = NormalizeKey(candidate.key);
            if (!string.Equals(candidateKey, normalizedKey, StringComparison.Ordinal))
                continue;

            entry = candidate;
            return true;
        }

        return false;
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private void OnValidate()
    {
        _entryCacheBuilt = false;
    }
}
