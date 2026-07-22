---
tags: [module, quest, dialogue]
created: 2026-06-19
updated: 2026-06-20
---

# 任务系统

## TL;DR
任务系统当前已经有可确认实现：`QuestDataSO` 定义任务，`QuestManager` 管理运行时进度与持久化，`QuestConditionTracker` 通过事件推进条件，`DialogueSystem` 管理对话树状态。

## 核心组件
- QuestDataSO — 任务配置（ID/条件/奖励/对话）
- QuestManager — 运行时任务管理
- QuestConditionTracker — 监听 EventCenter 推进条件
- DialogueSystem — 对话流程控制
- DialoguePanel — 对话 UI
- QuestTrackerUI — 任务追踪 UI
- TaskSystem — 任务系统入口 MonoBehaviour，用于启动追踪与预注册任务配置

## 事件驱动
- QuestAccepted / QuestProgressUpdated / QuestStageAdvanced / QuestCompleted
- DialogueStarted / DialogueEnded
- InteractionPerformed（与交互系统联动）

## 当前推进链路
```
TaskSystem.Start
  → QuestConditionTracker.StartTracking()
  → 预注册 QuestDataSO

运行时事件
  → UnitDeath / BagUpdated / AreaEntered / InteractionPerformed
      → QuestConditionTracker.UpdateConditions(...)
      → QuestManager.CheckStageCompletion(...)
      → 必要时 CompleteQuest(...)
```

## 持久化
- `QuestManager` 使用 `BinaryDataManager`
- 存档路径：`Quest/QuestSaveData`
