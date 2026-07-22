---
tags: [module, combat, damage, hitbox]
created: 2026-06-19
updated: 2026-06-20
---

# 战斗系统

## TL;DR
当前战斗链路由 `BehaviorInterpreter` / `ProjectileBase` 触发命中，伤害通过 `ProjectileDamageResolver` 结算，生命与死亡由 `StatusData` 处理，效果层由 `UnitEffectController + GlobalEffectSystem` 统一维护。

## 数据流
```
输入 / AI
  → HSM / 状态机进入行为
  → BehaviorInterpreter 或 ProjectileBase 触发命中
    → ProjectileDamageResolver.CalculateDamage(attacker, defender, multiplier, numericKey)
    → StatusData.ReceiveDamage / RestoreHealth
      → TypedEventBus.Publish(UnitDiedEvent)
      → EventCenter.SetEventTrigger(EventNames.UnitDeath, gameObject)  # 兼容旧监听
```

## 伤害计算
- 数值倍率优先从 `numericKey` 解析；解析失败回退事件或子弹自带倍率
- `rawDamage = attacker.AttackPower * multiplier`
- `damageWithBonus = rawDamage * (1 + attacker.DamageBonus)`
- `effectiveDefense = defender.Defense * (1 - attacker.Penetration)`
- 最终伤害：`max(1, damageWithBonus - effectiveDefense)`

## Hitbox 系统
- 形状：球/胶囊/盒
- 近战命中由 `BehaviorInterpreter` 执行 NonAlloc 物理查询
- 远程命中由 `ProjectileBase` 负责投射物链路
- hitGroupId 防重复命中
- 已命中目标用 HashSet<int>（instanceId）缓存

## 状态与效果
- `StatusData` 同时承载生命、攻击、防御、暴击、穿透、免伤、可被索敌等运行时状态
- 没有驱动脚本的单位也可以仅凭兜底 `StatusData` 作为低优先级可攻击目标存在
- `UnitEffectController` 负责单位身上的效果登记与展示数据
- `GlobalEffectSystem` 负责全局效果更新，不依赖目标是否仍在场上

## 死亡流
```
StatusData.CurrentHealth ≤ 0 → IsDead = true
  → TypedEventBus.Publish(UnitDiedEvent)
  → EventCenter.SetEventTrigger(EventNames.UnitDeath, gameObject)
    → HSM → DeathState
    → 玩家侧后续切人与隐藏由 PlayerPartyModule 负责
    → 敌人 / 中立单位按各自后续逻辑处理
```

## 相关
- [[modules/skill-system]] · [[modules/buff-system]] · [[modules/character-numeric]]
