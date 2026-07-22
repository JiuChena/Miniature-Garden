---
tags: [module, animation, behavior, section]
created: 2026-06-19
updated: 2026-06-20
---

# 角色动作演出系统

## TL;DR
当前动作演出层的重点不是“再包一层 Animator 状态机逻辑”，而是固定一套可共享的 `BehaviorCoreBaseController` 约定，让 `AnimatorSegmentPlayer`、Timeline 作者工具和运行时解释器都围绕同一命名规则工作。

## 核心要点
- 基础 Controller 采用固定槽位命名：`L{layer}_Segment_{slot}` / `L{layer}_Placeholder_{slot}`
- `AnimatorSegmentPlayer` 负责把行为动画段映射到占位片段，并用归一化 `CrossFade` 切换
- Timeline 作者期已经偏向“原生轨道预览 + 导出到 BehaviorClip”，不是再维护一套独立表现系统
- Scene 视图支持 Hitbox Gizmo，可在作者期观察当前命中范围
- 当前动画过渡值是归一化比例，不是秒数

## Controller 约定
- 共享命名约定定义在 `BehaviorCoreAnimatorControllerConvention`
- 默认共享目录：`Assets/BehaviorCore/Animator`
- 默认控制器名：`BehaviorCoreBaseController`
- 默认层数 / 槽位数：2 层 × 每层 8 槽
- 作者工具与运行时都依赖这套命名，而不是按角色各自猜状态名

## 播放链路
```
BehaviorInterpreter.PlaySegment
  → AnimatorSegmentPlayer.TryPlaySegment(segment, slotIndex, overrideCrossFade)
      → resolve SlotRuntimeInfo
      → overrideController[placeholderName] = segment.clip
      → animator.Play(...) 或 animator.CrossFade(...)
```

## 动画段参数语义
- `crossFadeDuration`
  - 0 到 1 的归一化过渡比例
  - 0 表示瞬切
  - 1 表示按当前 Animator 默认完整过渡比例切换
- `startTime`
  - 小于 0 表示顺延上一段
  - 大于等于 0 表示显式放置在行为时间轴上的时刻
- `layer`
  - 播放到哪个 Animator Layer

## 作者工具
- `BehaviorCoreAnimatorControllerSetupWindow`
  - 用于创建或校验正式的 BehaviorCore 基础 Controller 资源
- `BehaviorCoreTimelineExporterWindow`
  - 负责 Timeline ↔ `BehaviorClip` 双向编排
- `BehaviorTimelineEventClipAssetEditor`
  - 编辑行为事件片段
- `BehaviorTimelineHitboxClipAssetEditor`
  - 编辑 Hitbox 片段

## 编辑态预览边界
- 动画：走原生 `Animation Track`，可在 Timeline 中直接预览
- 音频：走原生 `Audio Track`，可对齐试听
- 特效显隐：走 `Control / Activation Track`，可在编辑态拖轴查看
- 命中判定 / 伤害 / 数值效果：不在编辑态完整模拟，运行态验证

## 运行时性能点
- `AnimatorSegmentPlayer` 初始化后缓存槽位、状态 Hash、占位名
- 同层当前激活槽位相同且可用时，会尝试换到备选槽，避免同状态重复覆盖
- 行为解释器端的骨骼缓存、目标物体路径缓存、碰撞体 ID 缓存，都是为动作演出链路服务

## 相关
- [[skill-system]] · [[combat-system]] · [[character-numeric]]
