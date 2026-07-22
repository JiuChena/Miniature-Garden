---
tags: [module, player, control]
created: 2026-06-19
updated: 2026-06-23
---

# 玩家控制系统

## TL;DR
PlayerController 作为玩家侧中控，持有 5 个 `IPlayerModule`（输入/编队/相机/切人占位/移动）。相机固定跟随 Player 根节点，`PlayerMovementModule` 既负责 Player 根节点上的实际移动，也负责把当前受控角色对齐到 Player。玩法输入总开关统一收束在 `PlayerInputModule`，`PlayerCameraController` 只消费该开关来控制鼠标与镜头输入。

## 核心组件
- PlayerController — 玩家侧唯一访问入口，初始化/调度模块并持有当前角色
- PlayerInputModule — 读取 Unity Input System，写入 Blackboard，只允许数字键 `1/2/3/4` 切人；同时维护玩法输入总开关（Alt / UI 禁用）
- PlayerPartyModule — 管理角色列表、独立切回冷却、驻场隐藏、位置继承
- PlayerCameraModule — 让相机始终 `Follow = Player.transform`
- PlayerMovementModule — 挂在 Player 上，持有 Player 根节点 `CharacterController`，执行实际移动，并同步当前受控角色到 Player
- PlayerSwitchPlacementModule — 驻场切人时为离场角色求安全错位入场点
- UnitTargetingModule — 挂在 Player 上，为角色/投射物提供统一索敌结果
- InteractionReceiver — 玩家级交互接收器；交互范围信息再由 `PlayerInputModule` 汇总为 `ICharacterInteractionSource`
- PlayerCameraController — 摄像机具体控制脚本，负责镜头缩放、鼠标锁定以及在玩法输入禁用时冻结镜头输入

## 数据流
```
Input System
  → PlayerInputModule.Tick → Blackboard
    → Alt / UI 禁用时直接清空新的玩法输入
  → PlayerController.Update
    → TickModules(Blackboard, deltaTime)
      → PlayerPartyModule.Tick → 处理切人与待隐藏角色
      → PlayerInputModule.DispatchCurrentCharacterControl
        → CurrentCharacter.ReceivePlayerControl(board)
  → PlayerInputModule.SetGameplayInputEnabled(bool)
    → PlayerCameraController.SetGameplayInputEnabled(bool)
      → 控制鼠标锁定/释放
      → 控制 CinemachinePOV / CinemachineInputProvider 是否允许镜头输入
```

## 切人语义
- 仅支持数字键 `1/2/3/4` 切人；鼠标滚轮不参与切人
- 被切走栏位进入独立冷却，默认 `0.5s`
- 若上一个角色 `CanSwitchOut == false`，则禁止切出
- 若上一个角色需要驻场且下一个角色当前离场，优先通过 `PlayerSwitchPlacementModule` 求入场点
- 若上一个角色处于 Move 状态，切入角色会用当前持续输入做一次预推进
- 当前受控角色死亡时，`PlayerPartyModule` 会按“当前索引 + 1，再对编队人数取模”的顺序循环查找下一个存活角色并自动切入
- 死亡切人不会阻塞新角色入场；死亡角色会保留到 Death 行为播放完成后再自动隐藏
- 已死亡角色不能再被手动切入；如果整队都已死亡，当前版本只会 `Debug.LogError` 并停止后续切人
- 新角色离场重进时支持一段可调的缩放入场动画；缩放目标不再是角色根节点，而是 `PlayerPartyModule.switchSpawnScaleTargetPath` 指定的角色骨骼相对路径
- 切人时可由 `PlayerPartyModule` 统一指定一个切人特效预制体，在新角色入场点生成，和切人音效一样由编队模块集中管理

## 空间锚点
- 相机固定跟随 `Player`
- `PlayerMovementModule` 负责 Player 根节点移动，并把 `Player` 世界位姿同步到当前角色
- 角色自身的行为执行仍发生在 `CharacterDriver` 一侧；当前受控角色在移动/朝向上跟随 Player
- `Player` 不再只是纯逻辑容器，而是玩家级系统共享的空间锚点

## 输入总开关
- `Alt` 按住时，由 `PlayerInputModule` 判定为玩法输入禁用
- UI 也应调用 `PlayerController.SetGameplayInputEnabled(false/true)` 走同一套总开关
- 输入禁用时只终止“新的玩法输入”进入 Blackboard，不负责强行打断已经在执行中的角色行为
- `PlayerCameraController` 不再自己判断 Alt，而是只根据 `SetGameplayInputEnabled(bool)` 的结果控制：
  - 鼠标锁定/释放
  - 镜头旋转启停
  - 滚轮缩放是否继续生效

## 相关
- [[modules/skill-system]] · [[modules/character-numeric]] · [[modules/combat-system]]
- [[references/player-control-old]] — 旧项目玩家控制设计（参考）
