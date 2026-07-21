# MiniatureGarden Camera Layer

这一层承载当前项目的相机业务实现，不属于可直接迁移的框架能力。

## 当前内容

- `PlayerCameraController`
  - 当前项目的第三人称镜头控制器
  - 负责滚轮缩放、镜头构图联动、光标锁定与震屏
  - 直接依赖当前项目的 Cinemachine 使用方式和玩家输入约定

## 边界规则

- 这里属于 `Business/MiniatureGarden`
- 可以依赖 Unity、Cinemachine 和当前项目玩家层
- 不应被 `Framework` 反向依赖

## 迁移建议

新项目若要复用框架层：

- 不要直接复制这里
- 应在自己的 `Business/<YourProject>/Camera` 中重建相机业务
- 如果只是参考当前项目镜头手感，可以把这里视为项目样例而不是框架标准实现
