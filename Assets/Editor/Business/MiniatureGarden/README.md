# MiniatureGarden Business Editor Layer

这一层承载当前项目业务侧编辑器扩展，不属于可直接迁移的框架编辑器代码。

## 当前目录职责

- `SkillCore/`
  - 当前项目角色行为资产与数值资产的编辑器适配

- `Cartoon Shader Editor/`
  - 当前项目卡通渲染 Shader 的材质面板扩展

- `ProjectMaintenance/`
  - 当前项目的 Unity 工程同步与维护入口

## 当前状态说明

- `Character/` 目录当前为空，说明原有角色侧编辑器扩展已基本回收到别的业务目录或已被清理
- 这类空目录保留与否不影响框架边界，但后续可以继续按实际需要清理

## 边界规则

- 这里可以依赖 `Framework` 和 `Business/MiniatureGarden`
- `Editor/Framework` 不应反向依赖这里
- 新项目若要复用框架，不应直接复制这里，而应建立自己的 `Assets/Editor/Business/<YourProject>`
