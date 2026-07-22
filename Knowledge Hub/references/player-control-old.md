---
tags: [reference, legacy, player]
created: 2026-06-19
updated: 2026-06-19
---

# 旧项目玩家控制设计

## TL;DR
旧项目 PlayerControlModule 800 行上帝对象，新项目拆分为 5 个独立 Module + PlayerController 协调器。

## 旧结构
```
PlayerControlModule（800行上帝对象）
  ├── 输入处理（内联）
  ├── 队伍管理（内联）
  ├── 摄像机（内联）
  └── 能量系统（内联）
```

## 新结构
```
PlayerController (协调器，~160行)
  ├── PlayerInputModule
  ├── PlayerPartyModule
  ├── PlayerCameraModule
  ├── PlayerSwitchPlacementModule
  └── CharacterTargetingModule
```

## 关键改进
- 每个 Module ≤200 行，职责单一
- 通过 IPlayerModule 接口统一生命周期
- 黑板模式传递输入数据
- 旧序列化字段有迁移路径（MigrateLegacySerializedFields）

## 相关
- [[../modules/player-control]] · [[../decisions/adr-004-hsm-interface]]
