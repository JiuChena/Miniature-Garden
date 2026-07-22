---
tags: [module, buff, effect]
created: 2026-06-19
updated: 2026-06-20
---

# 效果系统

## TL;DR
当前代码已经不是旧的 `BuffSystem + BonusPanel` 方案，而是“单位承载器 + 全局效果系统 + 效果定义 SO”的结构。`UnitEffectController` 负责单位侧登记，`GlobalEffectSystem` 负责场景统一推进，具体效果由 `EffectDefinitionSO` 子类实现。

## 核心要点
- 行为层不直接执行 Buff / Heal / DOT 逻辑，只发出 `ApplyEffect` 请求
- `GlobalEffectSystem` 统一维护所有运行时效果实例，后台角色也能继续结算
- `UnitEffectController` 既是效果入口，也是 UI / 调试展示的数据来源
- `EffectDefinitionSO` 是抽象基类，不强行把所有效果揉成一个数据结构
- `HealEffectSO` 已经是首个完整落地的效果实现，可支持单体、群体、后台、多次治疗

## 核心组件
- `GlobalEffectSystem`
  - 全局单例 `MonoBehaviour`
  - 维护注册单位、活跃效果、待激活效果
  - 每帧 `Update()` 推进所有 `RuntimeEffectInstance`
- `UnitEffectController`
  - 必须挂在单位侧
  - 负责登记当前单位身上的运行时效果
  - 可自动收集 `IUnitEffectPresenter`
  - 可选择失活后是否仍保留注册
- `EffectDefinitionSO`
  - 抽象基类
  - 定义 `ResolveTargets / BuildSchedule / OnEffectStarted / OnEffectTick / OnEffectEnded`
- `RuntimeEffectInstance`
  - 单个效果实例的运行时对象
  - 记录来源、目标、调度、剩余时间、Tick 计数
- `HealEffectSO`
  - 当前已实现的治疗效果
  - 支持 `Self / ExplicitTarget / AllAllies / AlliesInRadius`
- `GameplayEffectSO` / `BuffDataSO`
  - 用于属性修正类效果

## 执行链路
```
行为事件 / 技能逻辑
  → UnitEffectController.ApplyEffect(...)
      → GlobalEffectSystem.ApplyEffect(request)
          → EffectDefinitionSO.ResolveTargets(...)
          → EffectDefinitionSO.BuildSchedule(...)
          → 创建 RuntimeEffectInstance
          → Begin / Tick / End
          → 通过 StatusData 改值
```

## 与 StatusData 的关系
- `StatusData` 仍然是底层数值承载体
- 效果系统并不自己持有“最终面板”
- 治疗、伤害、属性增减最终都落到 `StatusData`
- 是否显示默认生命/防御、是否带驱动信息，取决于 `StatusData` 当前挂载环境

## HealEffectSO 当前能力
- 固定值：`baseHealValue`
- 等级数值：`baseHealNumericKey`
- 按来源属性缩放：`scalingStat + scalingMultiplierNumericKey`
- 调度：
  - `tickCount = 1` 时为瞬时治疗
  - `tickCount > 1` + `tickInterval` 时为多次治疗
- 目标：
  - 自身
  - 显式目标
  - 全队
  - 半径内友方

## 文档边界
- 知识库里不再把 `BonusPanel`、`BuffApplied/BuffRemoved` 当作当前实现
- 如果未来要补纯 UI 展示层，应围绕 `UnitEffectController.RuntimeEffectEntries` 或 `UnitEffectChangedEvent` 写，不应回退到旧 BuffSystem 叙事

## 相关
- [[combat-system]] · [[character-numeric]]
