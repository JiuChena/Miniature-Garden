---
tags: [module, skill, behavior, timeline]
created: 2026-06-19
updated: 2026-06-26
---

# 技能系统

## TL;DR
当前技能/行为系统是 `BehaviorClip` 数据驱动 + `BehaviorInterpreter` 时间轴执行 + `AnimatorSegmentPlayer` 动画段播放 + `CharacterBehaviorEventReceiver` 落地事件。作者期使用原生 Timeline 轨道配合自定义轨道导出为 `BehaviorClip`。

## 核心要点
- `BehaviorClip` 现在不只存运行时事件，还会保存 `authoringTracks` 轨道快照和 `transitions` 过渡定义
- `BehaviorInterpreter.Play()` 会先编译运行时缓存，再驱动动画段、事件、Hitbox 和完成回调
- VFX / Audio / Projectile / Effect 不在解释器内直接实现，而是经 `IBehaviorEventReceiver` 转交到 `CharacterBehaviorEventReceiver`
- 投射物事件已切到严格契约：`SpawnProjectile` 预制体必须直接挂 `ProjectileBase`
- Timeline 作者链路已经是“原生轨道做可预览表现，自定义轨道做行为数据”

## 核心组件
- `BehaviorClip` — 行为 SO，包含 `animationSegments`、`events`、`hitboxes`、`transitions`、`authoringTracks`
- `BehaviorInterpreter` — 行为执行器，负责时间轴推进、事件触发、Hitbox 检测、Loop / Clamp / Once 收尾
- `AnimatorSegmentPlayer` — 基于 `AnimatorOverrideController` 的动画段播放器
- `CharacterBehaviorEventReceiver` — 项目侧最小接收器，实现特效、音频、投射物、效果、伤害计算
- `UnitAbilityNumericProfile` — 技能倍率/数值表，供伤害与效果类按等级读取
- `BehaviorAuthoringTrackSnapshot` — Timeline 导出快照，用于把 SO 回填到多轨 Timeline

## 执行链路
```
CharacterDriver.Initialize
  → AnimatorSegmentPlayer.Initialize(Animator)
    → 包裹 AnimatorOverrideController / 校验槽位
  → BehaviorInterpreter.Configure(Animator, SegmentPlayer, cc, Data, Receiver, layerMask)

状态层请求某个行为
  → BehaviorInterpreter.Play(BehaviorClip)
      → BuildSortedEvents / BuildSegments / BuildHitboxes
      → Publish(BehaviorPlaybackStartedEvent)
      → PlaySegment(0)

每帧 Tick
  → UpdateAnimationSegments  → 检查是否跨段 → PlaySegment
  → UpdateNormalizedTime
  → ExecuteDueEvents         → Receiver 落地 VFX / 音频 / Projectile / Effect / CameraShake
  → UpdateHitboxes           → Physics.OverlapNonAlloc → 伤害 → hitGroup 去重
  → 完成后 Publish(BehaviorPlaybackCompletedEvent)
```

## 行为数据结构
- `AnimationSegment`
  - `clip`、`layer`、`crossFadeDuration`、`startTime`
  - `authoringTrackName` 用于回填 Timeline 时保留轨道来源
- `BehaviorTransitionDefinition`
  - `targetBehaviorKey`
  - `startTime / endTime`
  - `crossFadeDuration`
  - `authoringTrackName`
- `BehaviorAuthoringTrackSnapshot`
  - `trackKind` 标记 Meta / Animation / Audio / VfxControl / VfxActivation / Event / Hitbox / Transition
  - `sortIndex` 保留轨道顺序
  - `clips` 保存每个片段的作者期信息

## 作者链路
```
Timeline 原生轨道
  ├─ Animation Track      → 动作预览 / 导出 AnimationSegment
  ├─ Audio Track          → 声音预览 / 导出 Audio 行为事件
  ├─ Control / Activation → 特效显隐预览 / 导出控制类行为事件
  └─ 自定义轨道
       ├─ Behavior Event Track
       ├─ Behavior Hitbox Track
       └─ Behavior Transition Track
            ↓
BehaviorCoreTimelineExporterWindow
  → 导出为 BehaviorClip
  → 同时写入 authoringTracks 快照
  → 后续可按快照顺序回填为 Timeline
```

## Timeline 维护约束
- `Begin Behavior Authoring` 前会先清理 `TimelineAsset.m_Tracks` 中无效的根轨道引用
- 如果旧资源里残留了脚本已迁移或已失效的 `Behavior Meta / Events / Hitboxes / Transitions` 轨道，占位空壳不会继续留在顶部
- 轨道回填与排序前也会再次做一次相同清理，避免 `GetRootTracks()` 与序列化数组长度不一致，导致排序失效或重复建轨

## 当前边界
- 解释器只负责“按行为数据执行”，不直接承载角色请求策略
- 行为事件是否可预览取决于轨道类型：动画 / 音频 / Control 走原生 Timeline，可在编辑态直接看效果
- 命中、伤害、效果等纯运行时逻辑仍以 Play 模式实测为准

## 相关
- [[combat-system]] · [[character-animation]] · [[character-numeric]]
