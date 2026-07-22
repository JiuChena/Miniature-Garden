---
tags: [index, directory]
created: 2026-06-19
updated: 2026-06-24
---

# 全局目录

> 如果不知道某个概念/模块在哪个文件，在这里搜。

## 按概念查找

| 概念 | 所在文件 |
|------|---------|
| 玩家控制、PlayerController、输入、切人 | [[modules/player-control]] |
| 技能、BehaviorClip、BehaviorInterpreter、Hitbox | [[modules/skill-system]] |
| 战斗、伤害计算、StatusData、死亡流 | [[modules/combat-system]] |
| Buff、加成、修正器 | [[modules/buff-system]] |
| 角色数值、等级、倍率表、养成 | [[modules/character-numeric]] |
| 角色动画、动作演出、时间轴 | [[modules/character-animation]] |
| 敌人 AI、NavMesh、巡逻 | [[modules/enemy-ai]] |
| 交互、掩体、翻越 | [[modules/interaction-system]] |
| UI、面板、HUD、弹窗 | [[modules/ui-system]] |
| 数据持久化、背包、商店、芯片 | [[modules/data-economy]] |
| 任务、对话 | [[modules/quest-system]] |
| 成就 | [[modules/achievement-system]] |
| 音频、AudioSource、音量设置 | [[modules/audio-system]] |
| EventCenter、HSM、Blackboard、BinaryDataManager | [[modules/core-framework]] |

## 按文件夹

| 文件夹 | 内容 | 说明 |
|--------|------|------|
| `/HOME.md` | 仪表盘 | 项目当前状态、核心架构索引 |
| `/_INDEX.md` | 本文件 | 概念 → 文件映射 |
| `/_conventions/` | 约定 | 命名、教训、偏好 |
| `/modules/` | 领域笔记 | 每个模块一篇，渐进摘要格式 |
| `/decisions/` | 决策记录 | ADR 格式，重大技术决策 |
| `/references/` | 外部参考 | 旧项目架构、外部资料 |
| `/journal/` | 日记 | 每天一篇，讨论 + 决策 + 修改 |
| `/templates/` | 模板 | 新笔记时用的结构模板 |

## 按代码路径查找

### Framework（核心代码层，跨项目复用）

| 代码路径 | 对应笔记 |
|---------|---------|
| `Framework/CoreFramework/` | [[modules/core-framework]], [[modules/audio-system]] |
| `Framework/CoreFramework/事件中心模块/` | [[modules/core-framework]]（EventCenter, TypedEventBus） |
| `Framework/CoreFramework/HSM/` | [[modules/core-framework]]（HSM） |
| `Framework/BehaviorCore/Runtime/` | [[modules/skill-system]], [[modules/character-animation]] |
| `Framework/BehaviorCore/Data/` | [[modules/skill-system]]（BehaviorClip, BehaviorEvent 等 SO） |
| `Framework/BehaviorCore/Interfaces/` | [[modules/skill-system]]（IBehaviorEventReceiver 等） |
| `Framework/GameplayFramework/Core/` | [[modules/combat-system]], [[modules/character-numeric]]（StatusData, UnitDriverBase, IUnitDefinition） |
| `Framework/GameplayFramework/Effects/` | [[modules/buff-system]] |
| `Framework/GameplayFramework/Combat/` | [[modules/combat-system]]（战斗全局配置） |
| `Framework/GameplayFramework/Common/` | [[modules/character-numeric]]（UnitAbilityLevelGroup, CharacterAlignment） |

### Business（业务层，微型花园专属）

| 代码路径 | 对应笔记 |
|---------|---------|
| `Business/MiniatureGarden/Player/` | [[modules/player-control]] |
| `Business/MiniatureGarden/Character/Core/` | [[modules/character-numeric]]（CharacterDriver） |
| `Business/MiniatureGarden/Character/States/` | [[modules/player-control]], [[modules/combat-system]] |
| `Business/MiniatureGarden/Character/Combat/` | [[modules/combat-system]]（CharacterConditions, CharacterCooldowns） |
| `Business/MiniatureGarden/BehaviorCore/Runtime/` | [[modules/skill-system]], [[modules/character-animation]] |
| `Business/MiniatureGarden/Data/` | [[modules/data-economy]] |
| `Business/MiniatureGarden/Movement/` | [[modules/player-control]]（旧 GroundMovement 说明已退场，当前以 PlayerMovementModule 为主） |
| `Business/MiniatureGarden/Camera/` | [[modules/player-control]]（PlayerCameraController） |

## 按"遇到了什么问题"查找

| 问题 | 看这里 |
|------|--------|
| 新加一个角色怎么配 | [[modules/character-numeric]] |
| 技能怎么加新行为 | [[modules/skill-system]] |
| 伤害公式在哪改 | [[modules/combat-system]] |
| GC 太高怎么排查 | [[debugging/behavior-interpreter-gc]] |
| 事件怎么注册/触发 | [[modules/core-framework]] |
| 数据怎么存/读 | [[modules/data-economy]] |
