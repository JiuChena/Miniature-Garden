---
tags: [reference, legacy, architecture]
created: 2026-06-19
updated: 2026-06-20
---

# 旧项目架构参考

> 这篇只用于保留旧项目术语来源，不代表当前项目真实实现。若与当前代码冲突，以模块笔记和源码为准。

## 模块划分（什亭之箱）

| 领域 | 模块 |
|------|------|
| 战斗核心 | 角色系统、技能系统、Buff 系统、伤害系统、子弹/弹道 |
| 玩家控制 | 输入系统、编队系统、摄像机系统、能量系统 |
| 敌人 AI | NavMesh + HSM（Idle/Patrol/Chase/Attack/Search/Death） |
| 交互 | IInteractable、InteractionDetector、掩体/低墙/道具 |
| 数据&经济 | 背包、商店、芯片/强化、技能升级 |
| UI | HUD、角色面板、背包&商店、主菜单 |

## 新架构改进点
详见 [[../_conventions/lessons]] 和 [[../decisions/adr-001-pure-csharp]]

## 关键变化
- PlayerControlModule 800行 → 拆分为 4+ 个独立 Module
- Lua + C# → 纯 C#
- BinaryFormatter → MessagePack
- Resources + Addressables → 仅 Addressables
- EventCenter 裸字符串 → EventNames 常量类
- HSM 绑定具体类 → IStateOwner 接口驱动
- 技能硬编码 → SO 数据驱动（BehaviorClip）
