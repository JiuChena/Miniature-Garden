---
tags: [module, achievement]
created: 2026-06-19
updated: 2026-06-20
---

# 成就系统

## TL;DR
当前项目知识库里这篇是预留说明。代码侧暂未确认到已落地的 `AchievementManager` / `AchievementPopup` 实现，因此这里不能继续把旧设想当成当前事实。

## 当前状态
- 作为预留模块存在
- 可合理预期未来会依赖事件系统和数据持久化
- 但目前知识库不再把旧类名写成“已经存在”

## 如果后续落地，建议关注
- 成就条件订阅来源：`EventCenter` 或 `TypedEventBus`
- 进度/解锁数据持久化：`BinaryDataManager`
- UI 展示与排队弹窗

## 备注
- 当代码中真正出现成就运行时与数据结构后，再把这篇从“预留说明”升级为正式模块笔记
