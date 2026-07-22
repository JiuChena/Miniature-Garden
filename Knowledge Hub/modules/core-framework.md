---
tags: [module, framework, core]
created: 2026-06-19
updated: 2026-06-20
---

# 核心框架层

## TL;DR
核心框架当前是“双轨事件 + 轻量 HSM + 输入黑板 + 音频运行时 + MessagePack 持久化”的组合。它提供的是基础设施，不直接承担角色/玩家具体业务。

## 核心要点
- 事件中心已经是双轨：
  - `EventCenter` 处理字符串命名事件，偏兼容和广播
  - `TypedEventBus` 处理 `struct` 事件，偏跨系统通知
- `HSM` 目前很轻，只负责状态注册、切换、打断守卫，不包业务逻辑
- `Blackboard` 区分持续输入和单帧输入，`ScrollDelta` 已不再承担切人
- 音频运行时和音频设置已拆成 `AudioManager + AudioDataManager`
- 持久化层当前统一通过 `BinaryDataManager`

## 事件系统
- `EventCenter`
  - 支持无参 / 一参 / 二参事件
  - 通过 `EventNames` 常量消除裸字符串
  - 适合 UI、交互、兼容旧链路广播
- `TypedEventBus`
  - 仅处理 `struct` 事件
  - `Subscribe / Unsubscribe / Publish`
  - 当前用于行为开始/结束、单位效果变化等跨系统通知

## HSM
- `AddState(state)`
- `SwitchState<T>(priority, bypassGuard)`
- `Tick()`
- `TransitionGuard`
  - 类型是 `Func<InterruptPriority, bool>`
  - 用来决定当前状态是否允许被指定优先级打断
- 当前 HSM 本身不绑定 `CharacterDriver` 或 `EnemyDriver` 具体类型

## Blackboard
- 持续输入
  - `MoveInput / LookInput`
  - `AttackHeld / IsShooting / IsAiming / IsSprinting`
- 单帧输入
  - `AttackPressed / AttackReleased / JumpPressed / CrouchPressed`
  - `TalentPressed / BurstPressed / ReloadPressed / InteractPressed`
- 角色切换与滚轮
  - `SwitchIndex`
  - `ScrollDelta`
- 约定
  - `ResetFrameData()` 清空单帧数据
  - `ClearAllData()` 清空全部输入
  - `CopyFrom()` 做玩家输入分发

## 音频与持久化
- `AudioManager`
  - 运行时播放
  - `Play / Stop / SetAudio / RemoveAudio`
  - 句柄模式 + 绑定对象模式
- `AudioDataManager`
  - 音量/开关持久化
  - 写回后立即 `ApplyRuntimeSettings`
- `BinaryDataManager`
  - MessagePack 二进制读写
  - 供角色数据、音频设置等领域管理器复用
- `AddressableManager`
  - 资源加载唯一通道

## 当前边界
- 框架层不应该写角色专属策略
- 能放到领域管理器的数据，不回收拢成新的 `DataCenter`
- 事件总线只做通知，不承担查询或复杂返回值链路

## 相关
- [[skill-system]] · [[combat-system]]
