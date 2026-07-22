---
tags: [module, interaction]
created: 2026-06-19
updated: 2026-06-20
---

# 交互系统

## TL;DR
当前交互系统不是旧的 `InteractionDetector / InteractionBasicModule` 架构，而是 `InteractionEmitter + InteractionReceiver + IInteractable`。玩家侧 `InteractionReceiver` 维护范围内候选项、世界空间选项 UI、滚轮切换和 F 键执行。

## 核心要点
- 可交互物体挂 `InteractionEmitter`
- 玩家侧挂 `InteractionReceiver`
- 交互对象通过 `IInteractable` 暴露提示、图标、优先级和行为
- 世界空间选项 UI 由 `InteractionReceiver` 动态生成
- 鼠标滚轮只用于交互选项切换，不再用于角色切换

## 核心组件
- `IInteractable`
  - `CanInteract`
  - `OnInteract`
  - `OnEnterRange / OnExitRange`
  - 以及提示文本、图标、优先级等元数据
- `InteractionEmitter`
  - 挂在场景交互物体上
  - 依赖 `Collider.isTrigger`
  - `OnTriggerEnter / Exit` 时向玩家侧接收器注册 / 移除
  - 支持 `disposable` 一次性交互
- `InteractionReceiver`
  - 挂在玩家侧
  - 管理 `_interactables` 列表与 `_optionViews`
  - 支持滚轮切换、选中箭头、F 键触发
  - 通过 `EventCenter` 广播当前交互对象和执行事件
- `InteractionOption`
  - 交互选项 UI 组件
  - 提供图标、文字、Animator 引用

## 执行链路
```
交互物体触发器范围
  → InteractionEmitter.OnTriggerEnter
      → 找玩家侧 InteractionReceiver
      → Register(IInteractable)
      → OnEnterRange

玩家滚轮切换选项
  → InteractionReceiver.HandleScrollInput

玩家按 F
  → InteractionReceiver.HandleInteractInput
      → current.OnInteract(player)
      → EventCenter.Trigger(InteractionPerformed)
      → 一次性交互物可被 NotifyInteracted 后禁用
```

## 当前输入约定
- 滚轮：切换交互选项
- `F`：触发当前交互
- 候选项按 `Priority` 排序

## 文档边界
- 不再把 `InteractionDetector`、`InteractionBasicModule` 当作当前实现
- 掩体 / 翻越 / 道具 / NPC 只是 `IInteractable` 的具体内容，不需要再强行套一层旧抽象基类
