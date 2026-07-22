---
tags: [module, ui, panel]
created: 2026-06-19
updated: 2026-06-20
---

# UI 系统

## TL;DR
当前 UI 笔记只保留已经能从项目中确认的共识：面板层和世界空间 UI 以事件驱动刷新为主。旧文档里的 `BonusPanel` 已不应再作为当前实现描述。

## Canvas 层级
- WorldSpaceCanvas — 伤害跳字、敌人血条
- Root Canvas — 由 `PanelManager` 通过 Addressables 懒加载
- 分层节点：`Bot / Mid / Top / System`

## 当前已确认的 UI 运行时
- `PanelManager`
  - Addressables 异步加载面板
  - 面板实例缓存
  - 面板栈支持 `ESC` 关闭栈顶
- `PanelBase`
  - 面板基类
- `DialoguePanel`
  - 对话 UI
  - 监听 `DialogueSystem` 的节点变化与结束事件
- `QuestTrackerUI`
  - 任务追踪显示

## PanelManager
- Addressables 异步加载 + 实例缓存
- 面板栈支持 ESC 返回
- PanelBase 基类（LoadInit / ComponentInit / OnUpdate）

## 事件驱动
- `DialogueStarted / DialogueEnded`
- `QuestAccepted / QuestStageAdvanced / QuestCompleted`
- `UnitEffectChangedEvent / StatusData` 相关事件
- 其他 HUD 或角色面板事件链路待 UI 真正落地后再补

## ESC 自定义返回
- `PanelBase.OnEscapePressed()` — 虚方法，返回 `true` 走默认栈式关闭，返回 `false` 面板自行处理
- 例：GamePanel 覆写为切换到 MainMenuPanel 而不弹栈；MainMenuPanel 覆写为返回游戏
- `PanelManager.CheckCloseTopPanelInput` 先问栈顶面板，面板接管则跳过 `CloseTopPanel`

## 相关
- [[data-economy]] · [[buff-system]]
