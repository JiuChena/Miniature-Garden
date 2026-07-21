# MiniatureGarden Rendering Layer

这一层承载当前项目渲染业务实现，不属于行为编辑器或 RPG 框架的通用核心。

## 当前内容

- `后处理/SobelEdgeFeature`
  - 当前项目 URP Sobel 描边后处理特性
  - 直接依赖当前项目使用的渲染管线、Shader 与画面风格

## 边界规则

- 这里属于 `Business/MiniatureGarden`
- 可以依赖 URP 和项目 Shader
- 不应被 `Framework` 反向依赖

## 迁移建议

新项目若沿用框架：

- 渲染与美术风格实现单独迁移评估
- 不应把这里视为框架必带内容
