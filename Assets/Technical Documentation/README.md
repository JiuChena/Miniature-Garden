# Technical Documentation Index

## 新项目技术文档（微型花园）

> 以下文档为重构后的新项目文档，由 `api-docs` skill 自动生成。

## 玩家控制系统
- [输入处理模块.md](玩家控制系统/输入处理模块.md) — Blackboard + IInputProvider + PlayerInputProvider，Debug 模式，PlayerController 待办事项
- [角色动作演出系统实现方案.md](角色动作演出系统实现方案.md) — BehaviorClip SO 数据驱动 + BehaviorInterpreter 时间轴调度器 + Hitbox 区域伤害 + HSM 集成
- [框架迁移说明.md](框架迁移说明.md) — 当前项目中 `Framework` 与 `Business/MiniatureGarden` 的边界、行为编辑器三层结构，以及新项目复用时的最小迁移集
- [../Scripts/C#/Framework/README.md](../Scripts/C#/Framework/README.md) — 代码目录内的框架层边界说明，约束哪些目录可以直接迁移复用
- [../Scripts/C#/Business/README.md](../Scripts/C#/Business/README.md) — 代码目录内的业务层边界说明，约束哪些内容不得混入基础框架

---

> 以下文档待生成：核心框架 API 文档、其余业务模块文档将在代码模块完成后逐步补充

---

## 旧项目参考文档（什亭之箱）

> 以下为旧项目的设计文档，作为新架构的参考对照。

- [A什亭之箱技术文档.txt](A什亭之箱技术文档.txt) — 新架构技术决策、旧项目教训总结、各系统设计规范
- [A什亭之箱模块划分.txt](A什亭之箱模块划分.txt) — 旧项目模块职责划分（战斗/玩家/敌人/交互/数据/UI）
- [战斗系统实现方案.txt](战斗系统实现方案.txt) — 战斗系统数据流、伤害计算、弹道、Buff、死亡流
- [玩家控制系统实现方案.txt](玩家控制系统实现方案.txt) — PlayerControlModule 拆分、输入/队伍/摄像机/能量
- [敌人AI实现方案.txt](敌人AI实现方案.txt) — NavMesh + HSM 状态机（Idle/Patrol/Chase/Attack/Search/Death）
- [交互系统实现方案.txt](交互系统实现方案.txt) — IInteractable 接口、InteractionDetector、各交互实现
- [数据&经济系统实现方案.txt](数据&经济系统实现方案.txt) — BagData/Store/芯片/技能升级/ScriptableObject 配置
- [UI系统实现方案.txt](UI系统实现方案.txt) — PanelManager、HUD、BagPanel、伤害跳字
- [任务系统实现方案.txt](任务系统实现方案.txt) — QuestDataSO、QuestManager、QuestConditionTracker、DialogueSystem
- [成就系统实现方案.txt](成就系统实现方案.txt) — AchievementManager、AchievementPopup 队列通知
