# MiniatureGarden Business Layer

这一层承载当前项目 `MiniatureGarden` 的业务逻辑实现，不属于可直接迁移到新 RPG 项目的基础框架。

## 目录职责

- `SkillCore/`
  - 当前项目对 `Framework/SkillCore` 的行为系统适配层
  - 负责项目行为资产、项目策略资产、行为运行时桥接与项目规则实现

- `Character/`
  - 当前项目角色运行时层
  - 负责 `CharacterDriver` 中控、角色状态机、角色上下文、交互体积、角色调试模块

- `Player/`
  - 当前项目玩家运行时层
  - 负责 `PlayerController` 中控、输入分发、编队切换、相机跟随、切人入场占位与 Player 根节点同步

- `Movement/`
  - 当前项目角色/玩家移动实现
  - 当前已由 `PlayerMovementModule` 承担默认玩家地面移动实现

- `Camera/`
  - 当前项目相机业务实现

- `Data/`
  - 当前项目本地数据与运行时角色数据接入

- `Rendering/`
  - 当前项目渲染与后处理业务实现

- `Test/`
  - 当前项目测试或临时验证代码

## 与 Framework 的边界

- `Framework/`
  - 只放可复用的基础框架能力
  - 不应反向依赖 `Business/MiniatureGarden`

- `Business/MiniatureGarden/`
  - 只放当前项目规则、项目资产、项目桥接实现
  - 可以依赖 `Framework`，但不应被 `Framework` 反向依赖

## 当前推荐理解

如果新项目要复用这套基础框架，应整体迁移：

- `Assets/Scripts/C#/Framework`
- `Assets/Editor/Framework`

然后在新项目自己建立：

- `Assets/Scripts/C#/Business/<YourProject>`
- `Assets/Editor/Business/<YourProject>`

当前 `MiniatureGarden` 目录更适合作为“项目适配示例”和“当前项目正式业务实现”，而不是未来项目的直接复制模板。
