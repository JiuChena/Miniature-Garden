---
tags: [decision, architecture, framework, business]
created: 2026-06-20
updated: 2026-06-24
---

# ADR-006: Framework/Business 架构分离

## 状态
已采纳

## 背景
旧结构将所有 C# 脚本按功能分散在 `Scripts/C#/` 平级目录中（Character/、Player/、BehaviorCore/、Core Framework/、Data/、Movement/……），没有区分哪些是框架级（可在其他 RPG 项目中复用）、哪些是项目专属业务。

## 决策
将 `Scripts/C#/` 拆为两层：

```
Scripts/C#/
├── Framework/          # 核心代码层 —— 跨项目复用
│   ├── CoreFramework/  #    基础框架（EventCenter, HSM, AudioManager, BinaryDataManager...）
│   ├── BehaviorCore/      #    技能系统核心（BehaviorInterpreter, AnimatorSegmentPlayer...）
│   └── GameplayFramework/    #    RPG 通用玩法框架（StatusData, Effects, Combat, Targeting...）
│
└── Business/           # 业务层 —— 微型花园专属
    └── MiniatureGarden/
        ├── Player/     #    PlayerController + 6 个 Module
        ├── Character/  #    CharacterDriver, States, Modules
        ├── Data/       #    CharacterDataManager
        ├── Movement/   #    GroundMovement
        ├── Camera/     #    PlayerCameraController
        └── BehaviorCore/  #    项目专属技能扩展
```

## 原则
- Framework 不引用 Business，只依赖 Unity + 自身接口
- Business 引用 Framework 的接口和基类
- 接口桥接器（`RpgFrameworkRuntimeBridge`, `CoreFrameworkRpgEventBridge`）解耦框架内部子系统
- `IUnitDefinition` 等接口替代直接引用 `UnitAssetInformation`，实现框架层和业务层的解耦

## 后果
- 清晰的依赖方向：Business → Framework，不可逆
- Framework 可被其他 RPG 项目直接复用
- 旧路径（`Scripts/C#/Character/` 等）已完全移除
