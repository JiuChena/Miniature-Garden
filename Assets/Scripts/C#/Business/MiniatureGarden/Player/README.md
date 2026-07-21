# MiniatureGarden Player Layer

这一层只承载当前项目的玩家级运行时实现。

## 当前结构

当前 `Player` 目录采用“单中控 + 多模块”模式：

- `PlayerController`
  - 玩家侧唯一中控入口
  - 负责统一持有并调度玩家模块

- `PlayerContext`
  - 玩家运行时共享上下文

- `PlayerInputModule`
  - 输入采集、输入黑板写入、当前角色输入分发
  - 同时维护玩家进入的角色交互体积，作为角色交互来源

- `PlayerPartyModule`
  - 编队持有、角色切换、当前角色控制权切换

- `PlayerCameraModule`
  - 玩家级相机跟随与切人后镜头目标维护

- `PlayerSwitchPlacementModule`
  - 切人入场点位与驻场角色避让逻辑

- `PlayerMovementModule`
  - Player 根节点移动执行
  - 当前受控角色位姿同步到 Player 根节点
  - 持有 Player 运行时 CharacterController 与移动策略

- `UnitTargetingModule`
  - 玩家级索敌与投射物方向修正提供器
  - 当前项目侧已只公开 `IUnitTargetingProvider` 作为主入口，旧 `ProjectileTargetingProvider` 不再作为玩家层公开 API

## 设计边界

- `PlayerController` 只作为中控层，不应继续回流具体业务细节
- 玩家输入、编队、相机、切人占位、移动都应各自收在独立模块里
- 角色行为播放、角色状态机、角色行为规则不属于 `Player` 层，应留在 `Character` 与 `SkillCore` 层

## 与 Character 的分工

- `Player/`
  - 管“谁是当前受控角色、输入怎么分发、镜头和切人怎么运作”

- `Character/`
  - 管“单个角色自己如何推进状态机、行为和数据”

这种分工的目标是：

- 玩家级逻辑统一在 `PlayerController` 视角组织
- 角色级逻辑统一在 `CharacterDriver` 视角组织
- 两层通过中控访问彼此，而不是彼此随意下钻到具体模块实现
