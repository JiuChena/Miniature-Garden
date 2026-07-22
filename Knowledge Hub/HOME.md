---
tags: [dashboard, home]
created: 2026-06-19
updated: 2026-06-24
---

# 微型花园 · 项目知识库

> **新手上路**: 先读本文 → [[_INDEX]] 全局目录 → [[modules/_README]] 模块地图 → 具体模块笔记

## 当前状态
- **阶段**：核心框架可用，玩家/角色主链路已完成 Framework/Business 架构分离
- **架构**：`Framework/`（跨项目复用）+ `Business/MiniatureGarden/`（项目专属）双层
- **引擎**：Unity + 纯 C#（暂时无 Lua）
- **持久化**：MessagePack（BinaryDataManager）+ 专管数据管理器
- **资源**：Addressables 唯一通道
- **相机/空间锚点**：相机固定跟随 Player 根节点，Player 位姿同步到当前受控角色
- **玩家输入收束**：`PlayerInputModule` 统一负责玩法输入总开关（含 Alt / UI 禁用），`PlayerCameraController` 只消费该开关来控制鼠标与镜头输入

## 核心架构
- [[modules/player-control]] — PlayerController + 5 个 PlayerModule + CharacterTargetingModule
- [[modules/skill-system]] — BehaviorClip SO + BehaviorInterpreter + HSM
- [[modules/character-numeric]] — UnitAssetInformation + CharacterData + StatusData
- [[modules/combat-system]] — StatusData + ProjectileDamageResolver + UnitEffectController
- [[modules/core-framework]] — EventCenter、TypedEventBus、AudioManager、BinaryDataManager

## 找不到？
→ [[_INDEX]] 全局目录（按概念/代码路径/问题查找）

## 子系统
- [[modules/buff-system]] · [[modules/interaction-system]] · [[modules/audio-system]]
- [[modules/data-economy]] · [[modules/ui-system]] · [[modules/enemy-ai]]
- [[modules/quest-system]] · [[modules/achievement-system]] · [[modules/character-animation]]

## 关键决策
- [[decisions/adr-001-pure-csharp]]
- [[decisions/adr-002-messagepack]]
- [[decisions/adr-003-addressables]]
- [[decisions/adr-004-hsm-interface]]
- [[decisions/adr-005-so-data-driven]]
- [[decisions/adr-006-framework-business-separation]]

## 经验教训
见 [[_conventions/lessons]]

## 命名规范
见 [[_conventions/naming]]

## 最近讨论
```dataview
TABLE tags, date
FROM "journal"
SORT date DESC
LIMIT 10
```
