# MiniatureGarden Character Layer

这一层只承载当前项目的角色运行时实现，不再放行为资产定义，也不再放框架核心逻辑。

## 目录职责

- `Core/`
  - 角色运行时中控与上下文
  - `CharacterDriver` 是角色侧唯一中控入口
  - `CharacterContext` 是角色运行时共享上下文
  - `Core/Modules` 放角色挂载式运行时模块
  - `Core/Runtime` 放角色数据运行时接入等非挂载运行时实现

- `States/`
  - 当前项目角色状态机实现
  - 例如 `IdleState`、`MoveState`、`AttackState`、`TalentState`、`BurstState`、`ReloadState`、`VaultState`、`DeathState`

- `Combat/`
  - 当前项目角色战斗条件与冷却数据
  - 例如 `CharacterConditions`、`CharacterCooldowns`

- `Interaction/`
  - 当前项目角色交互体积与交互请求结构

## 明确不再承载的内容

以下内容已经从 `Character` 目录剥离：

- 行为配置资产与数值资产
  - 已迁到 `Business/MiniatureGarden/SkillCore/ProjectAssets`

- 行为运行时桥接与项目行为规则实现
  - 已迁到 `Business/MiniatureGarden/SkillCore/Runtime`

## 中控规则

- `CharacterDriver`
  - 负责统一持有并调度角色模块
  - 对外提供角色侧唯一访问入口
  - 不应继续回流项目行为资产定义或作者期逻辑

- `CharacterContext`
  - 只作为角色运行时共享数据容器
  - 供状态机、行为桥接、数据运行时和交互层共享

## 与 SkillCore 的分工

- `Character/`
  - 负责“当前角色现在处于什么状态、如何推进状态机、如何承载角色运行时数据”

- `SkillCore/`
  - 负责“当前项目如何把行为系统接到角色规则和项目资产上”

这两层应保持协作，但避免重新混成同一个目录职责。
