using System;
using System.Collections.Generic;
using BehaviorCore;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

/// <summary>
/// 瑙掕壊琛屼负涓庡熀纭€鏁板€奸厤缃€?
/// </summary>
[MovedFrom(false, null, null, "CharacterAssetInformation")]
[CreateAssetMenu(fileName = "UnitAssetInformation", menuName = "MiniatureGarden/Config/Units/Unit Asset Information")]
public class UnitAssetInformation : ScriptableObject, IUnitRuntimeDefinition
{
    [Header("Identity")]
    [Tooltip("单位运行时使用的唯一 ID，用于特效分组、调试定位和数据查找。")]
    public int characterId = 1;

    [Tooltip("单位所属阵营，用于技能效果目标筛选。")]
    [FormerlySerializedAs("alignment")]
    public UnitAlignment unitAlignment = UnitAlignment.Friendly;

    [Space(8)]
    [Header("Stats")]
    [Tooltip("基础生命成长公式：BaseHealth = A * (Level - 1) + C。")]
    public CharacterPrimaryStatFormula healthFormula = new CharacterPrimaryStatFormula
    {
        baseValueAtLevel1 = 100f,
        growthPerLevel = 0f,
    };

    [Tooltip("基础攻击成长公式：BaseAttack = A * (Level - 1) + C。")]
    public CharacterPrimaryStatFormula attackFormula = new CharacterPrimaryStatFormula
    {
        baseValueAtLevel1 = 10f,
        growthPerLevel = 0f,
    };

    [Tooltip("基础防御成长公式：BaseDefense = A * (Level - 1) + C。")]
    public CharacterPrimaryStatFormula defenseFormula = new CharacterPrimaryStatFormula
    {
        baseValueAtLevel1 = 2f,
        growthPerLevel = 0f,
    };

    [Tooltip("基础暴击率，0 表示 0%，1 表示 100%。")]
    [Range(0f, 1f)]
    public float baseCritRate = 0.5f;

    [Tooltip("基础暴击伤害倍率，2 表示造成 200% 伤害。")]
    [Min(1f)]
    public float baseCritDamage = 2f;

    [Tooltip("基础伤害加成，0.2 表示增加 20%。")]
    [Min(0f)]
    public float baseDamageBonus;

    [Tooltip("基础穿透加成，0.15 表示穿透 15%。")]
    [Min(0f)]
    public float basePenetration;

    [Tooltip("单位最大能量值。")]
    [Min(0f)]
    public float maxEnergy = 100f;

    [Space(8)]
    [Header("Movement")]
    [Tooltip("单位地面移动基础速度，单位为米/秒。")]
    [Min(0f)]
    public float moveSpeed = 6f;

    [Tooltip("单位跳跃或翻越使用的基础力度配置。")]
    [Min(0f)]
    public float jumpPower = 6f;

    [Tooltip("旧版默认行为过渡比例。当前优先使用具体行为或切换定义中的过渡值。")]
    [Range(0f, 1f)]
    public float defaultBehaviorTransitionDuration = 0.25f;

    [Space(8)]
    [Header("Combat")]
    [Tooltip("释放爆发技能时消耗的能量值。")]
    [Min(0f)]
    public float burstCost = 40f;

    [Tooltip("天赋技能进入冷却后的持续时间，单位为秒。")]
    [Min(0f)]
    public float talentCooldown = 8f;

    [Tooltip("爆发技能进入冷却后的持续时间，单位为秒。")]
    [Min(0f)]
    public float burstCooldown = 5f;

    [Tooltip("Talent skill indicator display config.")] public SkillIndicatorDisplayConfig talentIndicator = new SkillIndicatorDisplayConfig();
    [Tooltip("Burst skill indicator display config.")] public SkillIndicatorDisplayConfig burstIndicator = new SkillIndicatorDisplayConfig();

    [Header("Capabilities")]
    [Tooltip("单位是否支持普通攻击输入或 AI 普攻请求。")]
    public bool supportsAttack = true;

    [Tooltip("单位是否支持天赋技能输入或 AI 天赋请求。")]
    public bool supportsTalent = true;

    [Tooltip("单位是否支持爆发技能输入或 AI 爆发请求。")]
    public bool supportsBurst = true;

    [Tooltip("单位是否支持装填行为请求。")]
    public bool supportsReload = true;

    [Tooltip("单位是否支持蹲下行为。")]
    public bool supportsCrouch;

    [Tooltip("单位是否支持跳跃或翻越行为。")]
    public bool supportsJump;

    [Tooltip("行为命中检测默认作用到的目标层。")]
    public LayerMask hitboxTargetLayers = ~0;

    [Space(8)]
    [Header("UI")]
    [Tooltip("角色在 UI 中显示的名称。留空时回退为资源名。")] public string displayName = string.Empty;
    [Tooltip("角色主头像。用于当前角色主头像显示。")] public Sprite portraitIcon;
    [Tooltip("角色编队头像。留空时回退到主头像。")] public Sprite teamIcon;
    [Tooltip("角色天赋技能图标。")] public Sprite talentIcon;
    [Tooltip("角色爆发技能图标。")] public Sprite burstIcon;
    [Tooltip("角色武器图标。")] public Sprite weaponIcon;

    [Space(8)]
    [Header("Numeric")]
    [Tooltip("单位技能、普攻、子弹、治疗等数值定义表。")]
    public UnitAbilityNumericProfile numericProfile;

    [Space(8)]
    [Header("Strategies")]
    [Tooltip("单位专属条件源。留空时使用默认条件判断。")]
    public UnitConditionSourceAsset conditionSourceAsset;

    [Tooltip("单位专属行为切换策略。留空时使用默认切换策略。")]
    public UnitTransitionPolicyAsset transitionPolicyAsset;

    [Tooltip("单位专属攻击解析器。留空时使用默认攻击解析器。")]
    public UnitAttackResolverAsset attackResolverAsset;

    [Space(8)]
    [Header("Behaviors")]
    [Tooltip("单位行为表。每个 key 对应一组 BehaviorClip。")]
    public BehaviorEntry[] behaviors = Array.Empty<BehaviorEntry>();

    private readonly Dictionary<string, BehaviorClip[]> _behaviorCache =
        new Dictionary<string, BehaviorClip[]>(StringComparer.Ordinal);
    private bool _behaviorCacheBuilt;

    public int CharacterId => characterId;
    public CharacterAlignment Alignment => (CharacterAlignment)unitAlignment;
    public int UnitId => characterId;
    public UnitAlignment UnitAlignment => unitAlignment;
    public float MoveSpeed => moveSpeed;
    public float MaxEnergy => maxEnergy;
    public float BurstCost => burstCost;
    public float TalentCooldown => talentCooldown;
    public float BurstCooldown => burstCooldown;
    public SkillIndicatorDisplayConfig TalentIndicator => talentIndicator;
    public SkillIndicatorDisplayConfig BurstIndicator => burstIndicator;
    public float BaseCritRate => baseCritRate;
    public float BaseCritDamage => baseCritDamage;
    public float BaseDamageBonus => baseDamageBonus;
    public float BasePenetration => basePenetration;
    public bool SupportsAttack => supportsAttack;
    public bool SupportsTalent => supportsTalent;
    public bool SupportsBurst => supportsBurst;
    public bool SupportsReload => supportsReload;
    public bool SupportsCrouch => supportsCrouch;
    public bool SupportsJump => supportsJump;
    public LayerMask HitboxTargetLayers => hitboxTargetLayers;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite PortraitIcon => portraitIcon;
    public Sprite TeamIcon => teamIcon != null ? teamIcon : portraitIcon;
    public Sprite TalentIcon => talentIcon;
    public Sprite BurstIcon => burstIcon;
    public Sprite WeaponIcon => weaponIcon;
    public IUnitNumericResolver NumericResolver => numericProfile;
    public UnitConditionSourceAsset ConditionSourceAsset => conditionSourceAsset;
    public UnitTransitionPolicyAsset TransitionPolicyAsset => transitionPolicyAsset;
    public UnitAttackResolverAsset AttackResolverAsset => attackResolverAsset;

    public BehaviorClip GetBehavior(string key, int clipIndex = 0)
    {
        BehaviorClip[] group = GetBehaviorGroup(key);
        if (group.Length == 0)
            return null;

        int safeIndex = Mathf.Clamp(clipIndex, 0, group.Length - 1);
        return group[safeIndex];
    }

    public BehaviorClip[] GetBehaviorGroup(string key)
    {
        EnsureBehaviorTableReady();
        if (string.IsNullOrWhiteSpace(key))
            return Array.Empty<BehaviorClip>();

        if (_behaviorCache.TryGetValue(key, out BehaviorClip[] clips) && clips != null)
            return clips;

        if (string.Equals(key, BehaviorKeys.MoveJump, StringComparison.Ordinal) &&
            _behaviorCache.TryGetValue(BehaviorKeys.LegacyVault, out clips) &&
            clips != null)
        {
            return clips;
        }

        if (string.Equals(key, BehaviorKeys.LegacyVault, StringComparison.Ordinal) &&
            _behaviorCache.TryGetValue(BehaviorKeys.MoveJump, out clips) &&
            clips != null)
        {
            return clips;
        }

        return Array.Empty<BehaviorClip>();
    }

    public bool HasBehavior(string key)
    {
        return GetBehaviorGroup(key).Length > 0;
    }

    public bool HasAnyAttackStartBehavior()
    {
        return HasBehavior(BehaviorKeys.AttackStart) ||
               HasBehavior(BehaviorKeys.AttackLoop) ||
               HasBehavior(BehaviorKeys.Attack);
    }

    public bool HasAttackLoopBehavior()
    {
        return HasBehavior(BehaviorKeys.AttackLoop) || HasBehavior(BehaviorKeys.Attack);
    }

    public bool HasAnyCrouchAttackStartBehavior()
    {
        return HasBehavior(BehaviorKeys.CrouchAttackStart) || HasBehavior(BehaviorKeys.CrouchAttackLoop);
    }

    public bool HasCrouchAttackLoopBehavior()
    {
        return HasBehavior(BehaviorKeys.CrouchAttackLoop);
    }

    public bool ContainsBehaviorEntry(string key)
    {
        EnsureBehaviorTableReady();
        if (string.IsNullOrWhiteSpace(key) || behaviors == null)
            return false;

        for (int i = 0; i < behaviors.Length; i++)
        {
            BehaviorEntry entry = behaviors[i];
            if (entry != null && string.Equals(entry.key, key, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool EnsureBehaviorEntry(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        EnsureBehaviorTableReady();
        if (ContainsBehaviorEntry(key))
            return false;

        List<BehaviorEntry> entries = behaviors != null
            ? new List<BehaviorEntry>(behaviors)
            : new List<BehaviorEntry>();

        entries.Add(new BehaviorEntry
        {
            key = key,
            clips = Array.Empty<BehaviorClip>(),
        });

        behaviors = entries.ToArray();
        _behaviorCacheBuilt = false;
        RebuildBehaviorCache();
        return true;
    }

    public bool TryResolveNumericValue(string numericKey, IUnitAbilityLevelProvider levelProvider, out float value)
    {
        value = 0f;
        return NumericResolver != null && NumericResolver.TryResolveValue(numericKey, levelProvider, out value);
    }

    public float ResolveBaseHealth(int level)
    {
        return healthFormula.Evaluate(level);
    }

    public float ResolveBaseAttack(int level)
    {
        return attackFormula.Evaluate(level);
    }

    public float ResolveBaseDefense(int level)
    {
        return defenseFormula.Evaluate(level);
    }

    public void SetBehaviorGroup(string key, params BehaviorClip[] clips)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        List<BehaviorEntry> entries = behaviors != null
            ? new List<BehaviorEntry>(behaviors)
            : new List<BehaviorEntry>();

        BehaviorClip[] sanitizedClips = SanitizeClips(clips);
        for (int i = 0; i < entries.Count; i++)
        {
            BehaviorEntry entry = entries[i];
            if (entry == null || !string.Equals(entry.key, key, StringComparison.Ordinal))
                continue;

            entry.clips = sanitizedClips;
            entries[i] = entry;
            behaviors = entries.ToArray();
            _behaviorCacheBuilt = false;
            RebuildBehaviorCache();
            return;
        }

        entries.Add(new BehaviorEntry
        {
            key = key,
            clips = sanitizedClips,
        });

        behaviors = entries.ToArray();
        _behaviorCacheBuilt = false;
        RebuildBehaviorCache();
    }

    [ContextMenu("Validate Behavior Table")]
    private void ValidateBehaviorTableFromContextMenu()
    {
        ValidateData();
    }

    public bool ValidateData(bool logWarnings = true)
    {
        List<string> issues = new List<string>();
        CollectValidationIssues(issues);

        if (logWarnings)
        {
            for (int i = 0; i < issues.Count; i++)
                Debug.LogWarning($"[{name}] {issues[i]}", this);
        }

        return issues.Count == 0;
    }

    public int CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return 0;

        int initialCount = issues.Count;
        EnsureBehaviorTableReady();

        if (behaviors == null || behaviors.Length == 0)
        {
            issues.Add("单位行为表为空，单位将无法播放任何行为。");
        }

        if (numericProfile != null)
        {
            List<string> numericIssues = new List<string>();
            numericProfile.CollectValidationIssues(numericIssues);
            for (int i = 0; i < numericIssues.Count; i++)
                issues.Add($"单位能力数值表存在问题：{numericIssues[i]}");
        }

        CollectRequiredBehaviorKeyIssues(issues);

        if (behaviors == null || behaviors.Length == 0)
            return issues.Count - initialCount;

        HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < behaviors.Length; i++)
        {
            BehaviorEntry entry = behaviors[i];
            if (entry == null)
            {
                issues.Add($"BehaviorEntry[{i}] 为空引用。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.key))
            {
                issues.Add($"BehaviorEntry[{i}] 的 key 为空。");
                continue;
            }

            if (!seenKeys.Add(entry.key))
                issues.Add($"行为表中存在重复 key：{entry.key}。后出现的配置会覆盖前面的缓存结果。");

            BehaviorClip[] clips = SanitizeClips(entry.clips);
            if (clips.Length == 0)
            {
                issues.Add($"行为 key '{entry.key}' 没有任何有效的 BehaviorClip。");
                continue;
            }

            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                BehaviorClip clip = clips[clipIndex];
                if (clip == null)
                {
                    issues.Add($"行为 key '{entry.key}' 的 clips[{clipIndex}] 为空引用。");
                    continue;
                }

                List<string> clipIssues = new List<string>();
                clip.CollectValidationIssues(clipIssues);
                for (int issueIndex = 0; issueIndex < clipIssues.Count; issueIndex++)
                    issues.Add($"行为 key '{entry.key}' 引用的 BehaviorClip '{clip.name}' 存在问题：{clipIssues[issueIndex]}");

                CollectTransitionTargetIssues(entry.key, clip, issues);
                CollectNumericBindingIssues(entry.key, clip, issues);
            }
        }

        return issues.Count - initialCount;
    }

    private void OnValidate()
    {
        if (healthFormula == null)
            healthFormula = new CharacterPrimaryStatFormula();

        if (attackFormula == null)
            attackFormula = new CharacterPrimaryStatFormula();

        if (defenseFormula == null)
            defenseFormula = new CharacterPrimaryStatFormula();

        if (talentIndicator == null)
            talentIndicator = new SkillIndicatorDisplayConfig();

        if (burstIndicator == null)
            burstIndicator = new SkillIndicatorDisplayConfig();

        talentIndicator.Sanitize();
        burstIndicator.Sanitize();

        _behaviorCacheBuilt = false;
        EnsureBehaviorTableReady();
        ValidateData();
    }

    private void EnsureBehaviorTableReady()
    {
        if (_behaviorCacheBuilt)
            return;

        RebuildBehaviorCache();
    }

    private void RebuildBehaviorCache()
    {
        _behaviorCache.Clear();
        if (behaviors == null || behaviors.Length == 0)
        {
            _behaviorCacheBuilt = true;
            return;
        }

        for (int i = 0; i < behaviors.Length; i++)
        {
            BehaviorEntry entry = behaviors[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            _behaviorCache[entry.key] = SanitizeClips(entry.clips);
        }

        _behaviorCacheBuilt = true;
    }

    private static BehaviorClip[] SanitizeClips(BehaviorClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return Array.Empty<BehaviorClip>();

        List<BehaviorClip> validClips = new List<BehaviorClip>(clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validClips.Add(clips[i]);
        }

        return validClips.Count == 0 ? Array.Empty<BehaviorClip>() : validClips.ToArray();
    }

    private void CollectRequiredBehaviorKeyIssues(List<string> issues)
    {
        if (issues == null)
            return;

        ValidateRequiredBehaviorKey(BehaviorKeys.Idle, true, issues, "基础待机行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.CrouchIdle, supportsCrouch, issues, "蹲下待机行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.Move, moveSpeed > 0f, issues, "基础移动行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.MoveJump, supportsJump, issues, "跳跃或翻越行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.Death, true, issues, "死亡行为");
        ValidateRequiredAttackBehavior(issues);
        ValidateRequiredBehaviorKey(BehaviorKeys.Talent, supportsTalent, issues, "天赋技能行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.Burst, supportsBurst, issues, "爆发技能行为");
        ValidateRequiredBehaviorKey(BehaviorKeys.Reload, supportsReload, issues, "装填行为");
    }
    private void ValidateRequiredAttackBehavior(List<string> issues)
    {
        if (!supportsAttack || issues == null)
            return;

        if (!HasAnyAttackStartBehavior())
        {
            issues.Add($"普通攻击行为缺失：至少需要配置行为 key '{BehaviorKeys.AttackStart}'、'{BehaviorKeys.AttackLoop}' 或 '{BehaviorKeys.Attack}'。");
        }
    }

    private void ValidateRequiredBehaviorKey(string behaviorKey, bool required, List<string> issues, string label)
    {
        if (!required || issues == null)
            return;

        if (!HasBehavior(behaviorKey))
            issues.Add($"{label}缺失：行为表中未找到核心行为 key '{behaviorKey}'。");
    }
    private void CollectTransitionTargetIssues(string behaviorKey, BehaviorClip clip, List<string> issues)
    {
        if (clip == null || issues == null || clip.transitions == null || clip.transitions.Length == 0)
            return;

        for (int i = 0; i < clip.transitions.Length; i++)
        {
            BehaviorTransitionDefinition transition = clip.transitions[i];
            if (transition == null || string.IsNullOrWhiteSpace(transition.targetBehaviorKey))
                continue;

            if (!CanResolveTransitionTargetBehavior(transition.targetBehaviorKey))
            {
                issues.Add($"行为 key '{behaviorKey}' 的 Transition[{i}] 指向了不存在的目标行为 key '{transition.targetBehaviorKey}'。");
            }
        }
    }

    private bool CanResolveTransitionTargetBehavior(string behaviorKey)
    {
        if (string.IsNullOrWhiteSpace(behaviorKey))
            return false;

        if (string.Equals(behaviorKey, BehaviorKeys.Attack, StringComparison.Ordinal))
            return supportsAttack && HasAnyAttackStartBehavior();

        return HasBehavior(behaviorKey);
    }

    private void CollectNumericBindingIssues(string behaviorKey, BehaviorClip clip, List<string> issues)
    {
        if (clip == null || issues == null)
            return;

        if (clip.hitboxes != null)
        {
            for (int i = 0; i < clip.hitboxes.Length; i++)
            {
                HitboxDef hitbox = clip.hitboxes[i];
                if (hitbox == null || string.IsNullOrWhiteSpace(hitbox.numericKey))
                    continue;

                ValidateNumericKeyBinding(
                    $"行为 key '{behaviorKey}' 的 Hitbox[{i}] numericKey='{hitbox.numericKey}'",
                    hitbox.numericKey,
                    issues);
            }
        }

        if (clip.events == null)
            return;

        for (int i = 0; i < clip.events.Length; i++)
        {
            BehaviorEvent behaviorEvent = clip.events[i];
            if (behaviorEvent == null)
                continue;

            if (!string.IsNullOrWhiteSpace(behaviorEvent.numericKey))
            {
                ValidateNumericKeyBinding(
                    $"行为 key '{behaviorKey}' 的 BehaviorEvent[{i}] numericKey='{behaviorEvent.numericKey}'",
                    behaviorEvent.numericKey,
                    issues);
            }

            GameplayEffectSO gameplayEffect = behaviorEvent.gameplayEffectRef as GameplayEffectSO;
            if (gameplayEffect == null)
                continue;

            if (!string.IsNullOrWhiteSpace(gameplayEffect.baseValueNumericKey))
            {
                ValidateNumericKeyBinding(
                    $"行为 key '{behaviorKey}' 的 GameplayEffect '{gameplayEffect.name}' baseValueNumericKey='{gameplayEffect.baseValueNumericKey}'",
                    gameplayEffect.baseValueNumericKey,
                    issues);
            }

            if (!string.IsNullOrWhiteSpace(gameplayEffect.scalingMultiplierNumericKey))
            {
                ValidateNumericKeyBinding(
                    $"行为 key '{behaviorKey}' 的 GameplayEffect '{gameplayEffect.name}' scalingMultiplierNumericKey='{gameplayEffect.scalingMultiplierNumericKey}'",
                    gameplayEffect.scalingMultiplierNumericKey,
                    issues);
            }
        }
    }

    private void ValidateNumericKeyBinding(string bindingLabel, string numericKey, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(numericKey))
            return;

        if (NumericResolver == null)
        {
            issues.Add($"{bindingLabel} 已配置，但 UnitAbilityNumericProfile 为空。");
            return;
        }

        if (!NumericResolver.ContainsKey(numericKey))
            issues.Add($"{bindingLabel} 在 UnitAbilityNumericProfile 中不存在。");
    }
}
