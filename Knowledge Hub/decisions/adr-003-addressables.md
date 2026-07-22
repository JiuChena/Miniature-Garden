---
tags: [decision, resources, addressables]
created: 2026-06-19
updated: 2026-06-19
---

# ADR-003: Addressables 唯一资源通道

## 状态
已采纳

## 背景
旧项目同时存在 Resources.Load 和 AddressableManager 两套加载路径。

## 决策
只保留 AddressableManager，Resources 文件夹不放运行时资源。

## 后果
- AudioManager.SetAudio 只支持 addressableKey 和 AudioClip 两种重载
- 删除旧的 Resources 路径加载重载
