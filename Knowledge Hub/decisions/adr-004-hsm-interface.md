---
tags: [decision, hsm, interface]
created: 2026-06-19
updated: 2026-06-19
---

# ADR-004: HSM 接口驱动

## 状态
已采纳

## 背景
旧项目 HSM 绑定了具体类型 CharacterAnimatorDriver/EnemyAnimatorDriver，无法复用。

## 决策
HSM 只依赖 IStateOwner 接口，与具体类完全解耦。

## 后果
- HSM 不依赖 AnimatorDriverBase 的具体子类
- StateBase 通过 Owner 属性访问驱动器
- 玩家/敌人共用同一套 HSM 框架
- 需要角色特有功能时通过 `as` 安全转型
