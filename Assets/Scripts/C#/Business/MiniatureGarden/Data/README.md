# MiniatureGarden Data Layer

这一层承载当前项目的本地可变数据接入，不属于框架核心。

## 当前内容

- `CharacterData`
  - 当前项目角色本地可变数据
  - 包含角色等级、普攻等级、天赋等级、爆发等级

- `CharacterDataEntry`
  - 当前项目角色数据字典的可序列化条目结构

- `CharacterDataStorage`
  - 当前项目角色数据本地存档容器

- `CharacterDataManager`
  - 当前项目角色本地数据管理器
  - 基于 `BinaryDataManager` 进行加载、保存和默认值回退

## 边界规则

- 这里属于 `Business/MiniatureGarden`
- 这里可以依赖 `Framework/CoreFramework/二进制数据管理器`
- 不应反向被 `Framework` 依赖

## 迁移建议

新项目如果复用框架：

- 数据持久化基础能力可以继续用 `CoreFramework`
- 但具体存什么、如何组织角色可变数据，应该在自己的 `Business/<YourProject>/Data` 中定义
