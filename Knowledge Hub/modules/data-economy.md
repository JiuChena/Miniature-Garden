---
tags: [module, data, persistence, economy]
created: 2026-06-19
updated: 2026-06-24
---

# 数据与经济系统

## TL;DR
当前持久化已经拆成“按领域各自管理 + BinaryDataManager 统一序列化”，代码中已不再存在 `DataCenter` 这一层。角色养成走 `CharacterDataManager`，全局音频设置走 `AudioDataManager`。

## 当前管理器
- CharacterDataManager — 管理玩家拥有角色的 `CharacterData`
- AudioDataManager — 管理全局音频开关与音量
- BinaryDataManager — MessagePack 读写底座
- 其他子系统（背包/任务/UI 等）各自维护自己的运行时结构，不再通过单一 DataCenter 汇总

## BinaryDataManager
- `Save<T>(folder, fileName, data)` → MessagePack 序列化 → `persistentDataPath/Data/`
- `Load<T>(folder, fileName)` → 反序列化
- 类型安全，拒绝 BinaryFormatter

## CharacterDataManager
- 管理每个角色的 CharacterData（characterLevel / attackLevel / talentLevel / burstLevel）
- 本地持久化：`PlayerData/Character/CharacterData`
- 若访问的角色不存在，返回默认等级数据 `1 / 1 / 1 / 1`
- `GetOrCreateCharacterData` 负责在首次持有角色时创建存档项

## AudioDataManager
- 本地持久化：`PlayerData/Setting/GlobalAudio`
- 设置项：`musicEnabled / musicVolume / soundEnabled / soundVolume`
- 修改后立即应用到运行时 AudioManager，并写回本地存档

## 技能数值表
- UnitAbilityNumericProfile (SO) — 按技能等级查倍率
- UnitAbilityLevelGroup — 枚举映射 NormalAttack/Talent/Burst

## 设计约束
- 新的玩家本地数据优先新增对应的 `xxxDataManager`
- 静态规则和固定配置放 ScriptableObject；玩家可变进度放本地数据结构
- 不再恢复“大一统 DataCenter” 叙事，避免知识库和当前代码脱节

## 相关
- [[modules/character-numeric]] · [[modules/audio-system]] · [[modules/core-framework]]
