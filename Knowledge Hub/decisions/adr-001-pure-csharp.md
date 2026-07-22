---
tags: [decision, architecture, csharp]
created: 2026-06-19
updated: 2026-06-19
---

# ADR-001: 纯 C# 架构

## 状态
已采纳

## 背景
旧项目使用了 XLua + C# 混合架构，XLua 集成不完整，维护成本高。

## 决策
纯 C#，不使用 Lua/XLua。

## 后果
- 技能行为用 CharacterSkillBehavior 抽象类 + SO 配置替代 Lua 脚本
- 热更新能力受限（但当前项目不需要）
- 开发效率提升（统一语言、编译期类型检查）
