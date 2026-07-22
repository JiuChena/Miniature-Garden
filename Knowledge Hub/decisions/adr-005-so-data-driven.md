---
tags: [decision, skill, scriptableobject]
created: 2026-06-19
updated: 2026-06-24
---

# ADR-005: SO 数据驱动技能

## 状态
已采纳

## 背景
旧项目技能逻辑分散在 Lua 脚本和 C# 代码中，配置和逻辑混合。

## 决策
ScriptableObject 数据驱动 + BehaviorInterpreter 时间轴调度。

## 后果
- BehaviorClip (SO) 定义行为：动画段/事件/Hitbox/Transition
- BehaviorInterpreter 统一时间轴推进
- 角色专属逻辑通过 UnitAssetInformation 配置差异化（conditionSourceAsset/transitionPolicyAsset/numericProfile）
- Editor 中可用 Timeline 可视化编辑行为
