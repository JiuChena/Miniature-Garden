---
tags: [index, moc, modules]
created: 2026-06-19
updated: 2026-06-24
---

# 模块 MOC（Map of Content）

## 核心依赖图
```
player-control ← 输入/切换
    ↓ 驱动
skill-system ← 行为播放
    ↓ 依赖
character-animation ← 动画演出
    ↓ 触发
combat-system ← 战斗结算
    ├─→ buff-system
    └─→ character-numeric
```

## 模块清单
| 模块 | 层级 | 核心类 |
|------|------|--------|
| [[core-framework]] | 基础框架 | EventCenter, TypedEventBus, Blackboard, BinaryDataManager, AudioManager |
| [[audio-system]] | 基础框架 | AudioManager, AudioDataManager |
| [[player-control]] | 玩家 | PlayerController, PlayerInputModule, PlayerPartyModule, PlayerCameraModule, PlayerMovementModule, PlayerSwitchPlacementModule, UnitTargetingModule, PlayerCameraController |
| [[skill-system]] | 技能 | BehaviorClip, BehaviorInterpreter, AnimatorSegmentPlayer |
| [[character-animation]] | 表现 | AnimatorSegmentPlayer, Timeline Authoring, BehaviorCoreBaseController |
| [[character-numeric]] | 数据 | UnitAssetInformation, CharacterDataManager, UnitAbilityNumericProfile, StatusData, UnitDriverBase |
| [[combat-system]] | 战斗 | StatusData, ProjectileDamageResolver, CharacterConditions, DefaultCharacterAttackResolver |
| [[buff-system]] | 战斗 | EffectDefinitionSO, GameplayEffectSO, HealEffectSO, BuffDataSO, GlobalEffectSystem, UnitEffectController |
| [[enemy-ai]] | 敌人 | 预留，待 AI 运行时落地后补充 |
| [[interaction-system]] | 交互 | IInteractable, InteractionReceiver |
| [[ui-system]] | UI | PanelManager |
| [[data-economy]] | 数据 | CharacterDataManager, AudioDataManager, BinaryDataManager |
| [[quest-system]] | 任务 | QuestDataSO, QuestManager |
| [[achievement-system]] | 成就 | 预留，待系统落地后补充 |

## 阅读顺序（推荐）
1. [[core-framework]] — 地基
2. [[data-economy]] + [[character-numeric]] — 数据怎么流
3. [[player-control]] — 输入怎么进
4. [[skill-system]] + [[character-animation]] — 行为怎么执行
5. [[combat-system]] + [[buff-system]] — 伤害怎么算
6. [[ui-system]] + [[enemy-ai]] + [[interaction-system]] — 其余子系统
