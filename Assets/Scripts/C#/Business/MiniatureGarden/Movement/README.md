# MiniatureGarden Movement Layer

这一层承载当前项目的移动业务实现，不属于可直接迁移的框架标准模块。

## 当前内容

- 当前目录暂时没有独立移动策略脚本
- 默认玩家地面移动已收束到 `Player/PlayerMovementModule`
- 如果后续出现敌人独立移动策略或新的玩家移动模式，再在这里补充具体实现

## 边界规则

- 这里属于 `Business/MiniatureGarden`
- 可以依赖 `Framework/CoreFramework/Input`、`Framework/RPGGameplay` 的通用接口
- 不应反向耦合到 `Framework`

## 迁移建议

新项目若整包迁移框架：

- 把这里视为项目侧移动策略样例
- 新项目应按自己的玩家控制、移动规则、碰撞方案重建移动层
