---
tags: [module, numeric, character, formula]
created: 2026-06-19
updated: 2026-06-24
---

# 角色数值与行为执行

## TL;DR
角色属性分三层：`UnitAssetInformation`（静态配置公式/行为/倍率表）→ `CharacterData`（玩家拥有数据）→ `StatusData`（运行时最终值与修正器）。`UnitDriverBase / CharacterDriver` 负责把前两层组装成运行时快照。

## 数据三层结构
```
UnitAssetInformation (SO) — 公式 + 倍率表 + 行为配置
  ├─ primary stat formula: y = A * (level - 1) + C
  ├─ baseCritRate / baseCritDamage / baseDamageBonus / basePenetration
  ├─ numericProfile (UnitAbilityNumericProfile)
  ├─ behaviors[] (BehaviorEntry → BehaviorClip[])
  └─ condition / transition / attack resolver strategy assets

CharacterData — 玩家养成进度
  ├─ characterLevel
  ├─ attackLevel
  ├─ talentLevel
  └─ burstLevel

StatusData — 运行时状态
  ├─ BaseHealth / MaxHealth / CurrentHealth
  ├─ BaseAttackPower / AttackPower
  ├─ BaseDefense / Defense
  ├─ Crit / DamageBonus / Penetration
  └─ percent / flat modifiers + damage reduction + targetable / dead flags
```

## 运行时装配
- `CharacterDataManager` 根据 `characterId` 提供等级数据；不存在时返回默认 `1 / 1 / 1 / 1`
- `UnitDriverBase.TryBuildStatusSnapshot()` 提供完整状态快照
- `StatusData` 优先读取驱动快照；无驱动时回退到最小兜底状态（默认生命 / 防御 / 阵营）
- `UnitAssetInformation.TryResolveNumericValue()` 结合 `UnitAbilityNumericProfile` 按技能等级查数值

## 策略挂接
- `conditionSourceAsset`
- `transitionPolicyAsset`
- `attackResolverAsset`

## 相关
- [[modules/combat-system]] · [[modules/data-economy]] · [[modules/skill-system]]
