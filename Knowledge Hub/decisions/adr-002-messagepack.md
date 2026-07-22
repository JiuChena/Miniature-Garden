---
tags: [decision, persistence, messagepack]
created: 2026-06-19
updated: 2026-06-19
---

# ADR-002: MessagePack 持久化

## 状态
已采纳

## 背景
旧项目使用 BinaryFormatter（安全漏洞，已废弃）。

## 决策
统一使用 MessagePack 做二进制序列化。

## 后果
- BinaryDataManager 封装 Save/Load，路径 `persistentDataPath/Data/`
- 持久化数据类需加 `[MessagePackObject]` + `[Key(n)]`
- 复杂类型（SerializableDictionary 等）需自定义 Formatter
