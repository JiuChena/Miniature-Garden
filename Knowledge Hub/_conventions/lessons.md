---
tags: [convention, lessons]
created: 2026-06-19
updated: 2026-06-20
---

# 旧项目教训

## 架构层面
- PlayerControlModule 800 行上帝对象 → 拆分为独立 Module
- HSM 绑定具体类型 → 接口驱动
- 效果系统不要再拆成“逻辑 BuffSystem + 单独 BonusPanel 真正持有状态”两套真相源 → 当前统一以 `UnitEffectController + GlobalEffectSystem` 为准
- Resources + AddressableManager 两套加载 → 只保留 Addressables
- BinaryFormatter（已废弃）→ 统一 MessagePack
- XLua 维护成本高 → 纯 C#
- EventCenter 裸字符串 → EventNames 常量类
- DataCenter.Start() 混有测试代码 → Editor 工具脚本注入

## Bug 模式
- 子类重复声明字段遮蔽基类 → protected 字段，子类不重声明
- `Object.name` 遮蔽 → 用 `assetID`/`displayName`
- 条件逻辑写反 → 统一用 LayerMask 位运算
- 字段未赋值 → 结构化方法一次性计算全部属性
- 未检查空引用 → 统一用 `?.` 操作符
- 循环内修改集合 → 循环前缓存 Count

## 技术决策偏好
- 见 [[decisions/adr-001-pure-csharp]]
- 见 [[decisions/adr-002-messagepack]]
