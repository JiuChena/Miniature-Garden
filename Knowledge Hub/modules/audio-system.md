---
tags: [module, audio]
created: 2026-06-19
updated: 2026-07-22
---

# 音频系统

## TL;DR
音频系统由 `AudioManager`、`AudioManagerHost`、`AudioDataManager`、`AudioData`、`AudioTypes`、`ActiveAudioInfo` 六个文件组成。`AudioManager` 负责播放与 AudioSource 池化回收，`AudioDataManager` 负责音量/开关设置和本地持久化。项目内所有运行时音频都应统一走 `AudioManager`，才会自动应用玩家设置。

## 文件结构
| 文件 | 职责 |
|------|------|
| `AudioManager` | 核心播放器：AudioSource 池化、Play/Stop 句柄式、SetAudio/RemoveAudio 组件式、Tick 回收 |
| `AudioManagerHost` | MonoBehaviour 宿主：每帧驱动 Tick、协程追踪播放结束后释放 Clip Lease |
| `AudioDataManager` | 设置管理器：读写、持久化、向已注册 AudioSource 广播音量变更 |
| `AudioData` | 纯数据类：musicEnabled / musicVolume / soundEnabled / soundVolume |
| `AudioTypes` | 枚举：AudioType、StopAudioMode、RemoveAudioMode |
| `ActiveAudioInfo` | 活跃音频快照：记录 Source、循环、跟随目标、类型、音量、Clip Lease |

## AudioManager
- AudioSource 池化 — idleSources 队列复用
- 全局设置 — `globalSettings`（AudioData 实例）替代散字段
- **Play 三种重载**：
  - `Play(clip, type, position, loop, volume, followTarget)` — 世界空间位置播放
  - `Play(clip, type, parent, loop, volume)` — 挂载到父对象本地原点
  - `Play(clip, type, parent, localPosition, loop, volume)` — 挂载到父对象相对位置
- 句柄模式 — 返回 int handle，Tick 自动回收播放完毕的 Source
- **SetAudio 两种重载**：
  - 已加载 `AudioClip` 直接播放
  - Addressable Key 异步加载后播放
- `SetAudio` 在 `obj == null` 时走池化 AudioSource 播放
- AudioManagerHost — DontDestroyOnLoad 驱动 Tick + 协程回收 Clip Lease
- Tick 自动回收播放完毕的 Source + FollowTarget 追位置

## AudioDataManager
- 本地持久化：`PlayerData/Setting/GlobalAudio`（MessagePack）
- 设置项：musicEnabled / musicVolume / soundEnabled / soundVolume
- 每次修改设置 → 立即 SaveData + ApplyRuntimeSettings
- 监听者模式：注册的 AudioSource 在设置变更时自动同步音量/静音

## AudioType
- `AudioType.Music` — 受 musicEnabled / musicVolume 控制
- `AudioType.Sound` — 受 soundEnabled / soundVolume 控制
- 所有播放调用都应显式传入 `AudioType`

## 运行时设置同步
```
AudioDataManager.LoadData()
  → ApplyRuntimeSettings()
      → AudioManager.ApplyAudioSettings(...)
      → AudioManager.globalSettings 更新
      → 逐个活跃 AudioSource 同步音量
      → SettingsChanged 事件广播
```

## 相关
- [[core-framework]] · [[data-economy]]
