---
tags: [module, audio]
created: 2026-06-19
updated: 2026-06-20
---

# 音频系统

## TL;DR
音频系统当前由 `AudioManager` 和 `AudioDataManager` 组成：前者负责播放与回收，后者负责音量/开关设置和本地持久化。项目内所有运行时音频都应尽量统一走 `AudioManager`，这样才会自动应用玩家设置。

## AudioManager
- AudioSource 池化 — idleSources 队列复用
- 句柄模式 — `Play(clip, type, position, loop) → int handle`
- 绑定模式 — `SetAudio(clip, obj, type)` 挂 GameObject 上
- AudioManagerHost — DontDestroyOnLoad 驱动 Tick
- Tick 自动回收播放完毕的 Source + FollowTarget 追位置
- `SetAudio` 同时支持
  - 已加载 `AudioClip`
  - Addressable Key 异步加载
- `SetAudio` 在 `obj == null` 时也能正常播放，不要求预先挂音源

## AudioDataManager
- 本地持久化：`PlayerData/Setting/GlobalAudio`（MessagePack）
- 设置项：musicEnabled / musicVolume / soundEnabled / soundVolume
- 每次修改设置 → 立即 SaveData + ApplyRuntimeSettings
- 默认设置：
  - `musicEnabled = true`
  - `soundEnabled = true`
  - `musicVolume = 0.5`
  - `soundVolume = 0.5`

## AudioType
- `AudioType.Music`
  - 受 `musicEnabled / musicVolume` 控制
- `AudioType.Sound`
  - 受 `soundEnabled / soundVolume` 控制
- 所有播放调用都应显式传入 `AudioType`

## 运行时设置同步
```
AudioDataManager.LoadData()
  → ApplyRuntimeSettings()
      → AudioManager.ApplyAudioSettings(...)
      → 刷新已登记 AudioSource
```

## 当前边界
- 角色行为解释器、行为事件接收器、世界音频、绑定对象音频都应优先走 `AudioManager`
- 不建议在业务脚本里直接手写 `AudioSource.volume` / `mute` 作为长期方案

## 相关
- [[core-framework]]
