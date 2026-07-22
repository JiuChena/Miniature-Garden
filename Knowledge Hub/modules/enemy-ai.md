---
tags: [module, enemy, ai, hsm, implementation]
created: 2026-06-19
updated: 2026-06-24
---

# 敌人 AI 系统

## TL;DR
敌人 AI 第一版已经正式接入工程主链，目标是先把整条运行时流程跑通，再在后续迭代中细化巡逻、返回出生点、技能策略和更复杂的战术决策。当前实现坚持“尽量复用角色行为系统”的原则：敌人不复制一套新的行为编辑器与解释器，而是直接复用现有 `BehaviorClip`、`BehaviorInterpreter`、`HSM` 状态层、攻击解析器和数值/战斗/效果链路；敌人新增的部分只聚焦在 **AI 决策层**、**导航执行层** 和 **敌人 Driver 中控层**。

## 当前状态
已落地：
- `EnemyDriver`：敌人侧运行时中控，继承 `UnitDriverBase`
- `EnemyBehaviorRuntime`：敌人专用行为运行时壳，复用现有 `CharacterContext`、状态类、`BehaviorInterpreter`
- `EnemyBrainModule`：负责目标选择、追击/攻击/Crouch/Vault 决策，并把结果写入 AI `Blackboard`
- `EnemyNavigationModule`：负责 `NavMeshAgent` 导航执行，并把移动意图回写到黑板供状态机使用
- `EnemyTransitionPolicy`：首版敌人切换策略，结构上复用默认角色策略，只额外处理 `LoadState`
- `LoadState`：通用加载/入场状态，角色与敌人都可使用
- `CharacterInteractionVolume` 已泛化为“任意交互体接收者都可注册”，不再只认玩家
- `PlayerInputModule` 和 `EnemyDriver` 都可以作为 `ICharacterInteractionVolumeReceiver`

已验证：
- `Assembly-CSharp.csproj` 编译通过
- `Assembly-CSharp-Editor.csproj` 编译通过
- Unity 已正确导入敌人新增脚本资源
- 敌人样本构建菜单已再次成功执行，`Enemy_AI_Sample_CH0167.asset / .prefab / Enemy_DefaultAI.asset` 均有最新写入
- `SampleScene` 中旧敌人迁移入口已从“依赖 `CharacterDriver` 识别”改成“按敌人层/名称前缀/敌人 prefab 来源识别”
- 当前场景内旧敌人迁移菜单已实际命中并升级 `5` 个旧敌人对象
- 迁移脚本现在会自动生成默认敌人配置：
  - `Assets/ScriptableObjects/Enemy/Generated/LegacyEnemy_Default.asset`
  - `Assets/ScriptableObjects/Enemy/Generated/AI/LegacyEnemy_DefaultAI.asset`

未完成但已预留：
- 敌人场景预制体挂载与运行态实测
- 巡逻 / 返回出生点 / 丢失目标后的状态回退
- Talent / Burst / Reload 的更细 AI 使用策略
- 更专用的敌人数值配置资产（当前首版直接复用 `UnitAssetInformation`）

## 2026-06-23 本轮补充
本轮主要收了两条“代码层会直接挡住敌人主链跑通”的旧链路：

### 1. `LoadState` 不再接受伪装成 `Load` 的循环 Idle
之前敌人样本构建器会把 `Idle` 片段直接塞进 `Load` 行为里。因为 `Idle` 本身通常是 `WrapMode.Loop`，结果敌人进入 `LoadState` 后不会收到 `OnCompleted`，会卡在 `LoadState`，导航与普通行为切换都会被压住。

当前收束为：
- `LoadState` 只在 `Load` 行为存在且 `wrapMode == Once` 时才真正进入
- `EnemyBehaviorRuntime.EnterInitialState()` 也同步加了这层判断
- 敌人样本构建器会移除“仅仅拿 Idle 伪装出来的 Load”

结果：
- 没有正式 `Load` 行为的敌人会直接从 `IdleState` 开始
- 只有真正的一次性入场动作才会走 `LoadState`

### 2. Player 根节点碰撞体已可代理到当前受控角色战斗数据
当前项目里 `Player` 根节点承担玩家移动和 `CharacterController`，而真正的 `StatusData / UnitEffectController / CharacterDriver` 在当前受控角色上。敌人用球形或射线查询命中到 `Player` 根节点时，原本会因为 `GetComponentInParent<StatusData>()` 取不到真实战斗数据而把候选过滤掉。

当前新增了一层统一代理解析：
- `IUnitCombatProxyProvider`
- `UnitCombatResolver`
- `PlayerController` 已实现该代理接口

当前已接入代理解析的位置：
- `UnitTargetingModuleCore`
- `BehaviorInterpreter`
- `ProjectileBase`
- `CharacterBehaviorEventReceiver`

结果：
- 敌人命中 `Player` 根节点碰撞体时，能继续解析到当前受控角色的 `StatusData`
- 后续索敌、受击、命中特效、投射物命中链都能沿用同一条解析路径

### 3. 无 NavMesh 场景的敌人导航兜底
当前 `EnemyNavigationModule` 已调整为：
- 初始化时默认先禁用 `NavMeshAgent`
- 只有脚下采样到有效 NavMesh 时才启用并 `Warp`
- 如果当前场景没有可用 NavMesh，则退回到一个最小的“朝目标平移 + 回写 MoveInput”兜底逻辑

说明：
- 这只是为了让敌人 AI 首版在样本场景里先跑起来
- 后续正式战斗场景依然推荐使用标准 NavMesh

### 4. 角色状态切入时现在会立即收住敌人导航
之前敌人虽然在下一帧会因为 `RequiresNavigationSuppression` 停掉 `NavMeshAgent`，但在切入 `Attack / Reload / Talent / Burst / Vault` 的那一帧仍可能残留一点导航推进，表现为“刚进动作时脚还会滑一下”。

当前收束为：
- `EnemyNavigationModule` 新增 `StopMovementImmediately()`
- `CharacterStateBase.StopMovementImmediately()` 除了继续停玩家 `PlayerMovementModule`，也会尝试停掉当前宿主上的 `EnemyNavigationModule`

结果：
- 敌人进入攻击、装填、技能、翻越等状态时，导航位移会在状态切入时立即被收住
- 敌人复用角色状态机时，不再只对玩家移动链生效

### 5. 敌人样本资源已按当前运行时 key 语义清理
敌人样本配置资源当前已同步清掉两类旧残留：
- 假 `Load`：若 `Load` 指向的不是 `WrapMode.Once` 的正式入场片段，则从样本配置中移除
- 旧 `Vault` key：当前运行时统一走 `MoveJump`，样本配置不再保留旧的 `Vault` 行为组

结果：
- `Enemy_AI_Sample_CH0167.asset` 不再继续带着会误导运行态的旧 key
- 样本资源与当前状态机、条件判断和翻越链路语义一致

## 当前验证边界
- 代码与资源构建链已经完成
- 但 Unity MCP 在脚本刷新后发生了多次 HTTP 会话重连，导致这轮没有完整补齐最终 Play 态观察截图/对象运行态读取
- 因此目前“代码主链已落地并编译通过”是确定的，“敌人样本场景内最终 `Idle -> Move -> Attack` 实测 + PlayMode Test Runner 正式通过结果”还需要在下一轮 Unity 会话稳定后补一次

## 2026-06-23 本轮新增收束

### 7. 旧场景敌人迁移不再依赖旧 Driver
之前的 `Upgrade Legacy Scene Enemies` 菜单入口默认从 `CharacterDriver[]` 开始查找，这与 `SampleScene` 当前真实摆放的旧敌人结构不匹配。  
现在这条链已改为：
- 从当前激活场景内所有 `CharacterController` 根对象出发
- 结合以下条件识别敌人候选：
  - 根对象位于 `Enemy` 层
  - 名称前缀为 `Enemy_`
  - prefab 来源位于 `Assets/Prefabs/Enemy/`
  - 或旧 `CharacterDriver.Config.UnitAlignment == Enemy`
- 自动跳过角色 prefab 与已升级成 `EnemyDriver` 的对象

结果：
- `SampleScene` 中旧敌人对象不再因为没有旧 `CharacterDriver` 而漏迁移
- 当前已确认场景菜单实际升级了 `5` 个旧敌人实例

### 8. 迁移后会自动保存当前场景
之前迁移脚本只 `MarkSceneDirty`，但不主动保存。  
这会导致：
- 内存里迁移成功
- 磁盘上的 `SampleScene.unity` 仍可能保留旧状态

当前已改为：
- 迁移完成后继续 `SaveScene(activeScene)`
- 若当前场景没有有效保存路径，则输出明确警告

结果：
- 旧敌人迁移结果不再只是“本次编辑器会话内有效”
- 用户后续重新打开项目时，更容易直接拿到已经接入主链的场景状态

### 9. 已补“敌人运行主链静态校验”菜单
当前新增代码侧作者工具：
- `MiniatureGarden/Enemy/Validate Runtime Chain In Scene`

它会对当前场景中的敌人候选对象做静态检查，主要覆盖：
- `EnemyDriver`
- `CharacterController`
- `NavMeshAgent`
- `BehaviorInterpreter`
- `StatusData`
- `UnitTargetingModule`
- `EnemyBrainModule`
- `EnemyNavigationModule`
- 子节点 `Animator`
- 行为配置是否至少具备：
  - `Idle`
  - `Move`
  - `AttackStart/AttackLoop/Attack`
  - 若声明支持 `Jump`，则要求 `MoveJump`
  - 若存在 `Load`，则要求 `WrapMode.Once`

说明：
- 这一步是纯代码/作者链收束
- 不依赖 NavMesh 已经烘焙完成
- 不依赖敌人寻路参数已经调优

### 10. 当前职责边界已明确
本轮之后的默认边界收束为：
- Codex 负责：
  - 代码功能实现
  - 运行时流程闭环
  - 编辑器作者工具与迁移入口
  - 行为主链逻辑正确性
- 开发者自行负责：
  - AI Navigation 网格烘焙
  - 敌人寻路参数调优
  - 场景内摆放、路径可达性
  - 最终手感调参与实机验收

这意味着后续敌人系统推进时，不再把 NavMesh 烘焙与敌人参数调优视为当前代码任务的阻塞项。

## 已实现架构

### 1. 通用行为底座继续复用
当前敌人直接复用以下现有层：
- `BehaviorClip`
- `BehaviorInterpreter`
- `CharacterContext`
- `IdleState / MoveState / AttackState / TalentState / BurstState / ReloadState / VaultState / DeathState`
- `DefaultCharacterAttackResolver`
- `CharacterConditions`
- `StatusData`
- `UnitEffectController`
- `ProjectileDamageResolver`
- `UnitTargetingModule`

这意味着：
- 行为编辑器作者链路无需为敌人重新做一份
- 敌人与角色使用同一套行为 key 语义
- 数值、投射物、VFX、受击体、目标选择等战斗链可直接沿用

### 2. 敌人新增层
当前新增的敌人侧脚本职责如下：

#### `EnemyDriver`
职责：
- 敌人唯一运行时中控入口
- 持有敌人 AI 黑板
- 初始化并调度 `EnemyBrainModule`、`EnemyNavigationModule`、`EnemyBehaviorRuntime`
- 构建 `CharacterContext`
- 提供 `IUnitBehaviorRequester`、`IUnitAbilityLevelProvider`、`ICharacterInteractionSource`
- 提供 `StatusDataSnapshot`

说明：
- 当前敌人首版直接使用 `UnitAssetInformation` 作为配置资源
- 敌人不依赖玩家输入，也不依赖 `PlayerController`

#### `EnemyBehaviorRuntime`
职责：
- 敌人行为播放与状态机调度
- 绑定 `Animator` 和 `AnimatorSegmentPlayer`
- 组装 `HSM`
- 装配 `CharacterConditions`、`EnemyTransitionPolicy`、攻击解析器
- 决定初始进入 `LoadState` 还是 `IdleState`

说明：
- 本质上它是 `CharacterBehaviorRuntime` 的敌人适配壳
- 后续如果继续抽象，可上提为更中性的 `UnitBehaviorRuntime`

#### `EnemyBrainModule`
职责：
- 使用 `IUnitTargetingProvider` 选目标
- 判断当前是否需要追击或持续攻击
- 在支持时触发蹲下与翻越决策
- 将决策写入 `Blackboard`

当前实现规则：
- 有目标且在攻击距离内：写入 `AttackHeld / AttackPressed`
- 目标超出攻击距离：驱动追击
- 满足掩体范围与距离窗口时可请求 `CrouchPressed`
- 满足翻越范围与冷却时可请求 `JumpPressed`

#### `EnemyNavigationModule`
职责：
- 使用 `NavMeshAgent` 执行移动
- 根据 Brain 给出的目标点与停止距离更新导航
- 将导航速度回写成局部 `MoveInput`
- 在攻击、翻越、蹲下等不应位移的阶段自动抑制导航
- 当敌人进入 `OffMeshLink` 时，优先改为触发行为系统的 `VaultState`，而不是只让 `NavMeshAgent` 自动穿过

当前关键点：
- 实际位移由 `NavMeshAgent` 驱动，不让敌人硬塞进玩家移动链
- `MoveState` 仍然只负责行为播放，不直接驱动位移
- `NavMeshAgent.desiredVelocity` 会转换成局部 `MoveInput`，供现有状态机复用 `Idle <-> Move`
- 当前要求可翻越物体同时具备两层信息：
  - `NavMeshLink`：让寻路层知道“这里是可达路径”
  - `CharacterInteractionVolume`：提供翻越行为需要的起点/落点/朝向/弧高
- 当敌人已进入 `OffMeshLink` 但没有成功注册到交互触发器时，`EnemyNavigationModule` 现在会在 link 起点/终点附近按半径主动搜索 `CharacterInteractionVolume`，并把生成出的 `CharacterVaultRequest` 直接排队交给 `EnemyDriver`
- 如果运行时出现 `Partial Path`，说明 `NavMeshAgent` 并没有把该翻越点视为有效连通路径；优先检查 `NavMeshLink` 两端是否真的连到 NavMesh、`Agent Type` 是否匹配、`Area Mask` 是否允许通过

#### `EnemyTransitionPolicy`
职责：
- 首版敌人状态切换策略

当前实现：
- 直接复用 `DefaultCharacterTransitionPolicy`
- 仅在 `LoadState` 阶段禁止普通切换

说明：
- 这样做的优点是先跑通主链，不重复造轮子
- 后续若敌人需要更复杂策略，可以在这里逐步分叉出真正的敌人切换规则

## LoadState 现状
已新增：
- `BehaviorKeys.Load`
- `CharacterStateId.Load`
- `LoadState`

当前行为：
- 若单位配置了 `Load` 行为，则启动时先播 `Load`
- 若未配置，则直接回退到 `Idle`
- `Load` 播放完成后自动切到 `Idle`

## 交互体与敌人的 Crouch / Vault 接入
当前交互系统已经做了一个关键泛化：
- `CharacterInteractionVolume` 不再只识别玩家
- 通过新增 `ICharacterInteractionVolumeReceiver`，只要对象实现这个接收器，就可以接收掩体/翻越触发体通知

当前接收者：
- `PlayerInputModule`
- `EnemyDriver`

这意味着：
- 同一个场景交互体可以同时服务玩家和敌人
- 敌人也可以在交互范围内做蹲下与翻越判断

## 当前数据流
```text
EnemyDriver.Update
  -> Blackboard.ClearAllData()
  -> EnemyBrainModule.Tick
       -> 写 Attack / Crouch / Jump / 追击意图
  -> EnemyNavigationModule.Tick
       -> 执行 NavMeshAgent
       -> 回写 MoveInput
  -> EnemyBehaviorRuntime.Tick
       -> EnemyTransitionPolicy 根据 Blackboard 决定状态
       -> BehaviorInterpreter 播放对应行为
```

## 当前首版限制
### 1. 敌人配置资源仍复用 `UnitAssetInformation`
优点：
- 复用快，主链容易跑通
- 行为和数值表全部立即可用

限制：
- 还没有敌人专属 SO
- 巡逻、警戒、返回点、仇恨等 AI 参数还没有独立资源入口

### 2. 目标选择仍基于现有 `IUnitTargetingProvider`
优点：
- 与角色投射物/索敌一致
- 战斗判定统一

限制：
- 还没有真正的敌人视野、听觉、仇恨列表
- 当前更接近“直接取当前最优可攻击目标”

### 3. Talent / Burst / Reload 首版只是行为层可用
当前首版重点是：
- 让敌人能进入这些状态
- 保证行为编辑器链路对敌人不冲突

但 Brain 还没有主动使用这些能力的策略。

## 后续建议顺序
1. 给敌人预制体正式挂上：
   - `EnemyDriver`
   - `EnemyBrainModule`
   - `EnemyNavigationModule`
   - `UnitTargetingModule`
   - `StatusData`
   - `UnitEffectController`
   - `BehaviorInterpreter`
   - `NavMeshAgent`
2. 用一只最简单敌人验证：`Load -> Idle -> Move -> Attack -> Death`
3. 再验证：
   - 掩体内蹲下
   - 可翻越障碍前翻越
4. 后续再考虑拆出：
   - `EnemyAssetInformation`
   - `EnemyConditions`
   - 更完整的 `EnemyTransitionPolicy`
   - 巡逻 / 仇恨 / 返回出生点 / 技能策略

## 相关代码入口
- `Assets/Scripts/C#/Business/MiniatureGarden/Enemy/Core/EnemyDriver.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Enemy/Core/EnemyBehaviorRuntime.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Enemy/AI/EnemyBrainModule.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Enemy/AI/EnemyNavigationModule.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Enemy/AI/EnemyTransitionPolicy.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Character/States/LoadState.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Character/Interaction/CharacterInteractionVolume.cs`
- `Assets/Scripts/C#/Business/MiniatureGarden/Character/Interaction/ICharacterInteractionVolume.cs`

## 当前收尾状态
- 运行时代码、迁移工具、样例资源都保留
- 我这轮为验证主链临时补的 PlayMode 测试脚本与测试程序集已经清理，不再继续留在工程里
- `EnemyDriver` 里的测试专用手动推进入口也已移除，当前运行时层只保留正式链路
- 目前判断标准已经收束为：代码编译通过 + 你在场景里自行完成 NavMesh / 参数 / 实机校验

## 2026-06-23 额外收束
### 11. 敌人不再默认继承角色专属策略资产
敌人配置当前仍复用 `UnitAssetInformation`，但在从角色配置复制出敌人配置时，已经主动清空以下三个资产引用：
- `conditionSourceAsset`
- `transitionPolicyAsset`
- `attackResolverAsset`

原因：
- 角色配置上的这些资产很可能带有明显的玩家输入或角色特化假设
- 直接复制给敌人后，容易出现“编译没问题，但敌人状态切换/攻击解析逻辑仍沿用角色特化规则”的隐蔽问题

当前收束结果：
- 敌人默认回退到 `CharacterConditions + EnemyTransitionPolicy + DefaultCharacterAttackResolver`
- 如果后续确实需要敌人专属策略，再由开发者显式指定

### 12. 敌人索敌默认不再受玩家镜头朝向影响
`UnitTargetingModuleCore` 现在新增了本地开关：
- `useCameraFacingPreference`

当前约定：
- 玩家单位可继续开启，保留“摄像机正对优先”的锁敌体验
- 敌人 AI 在作者工具配置时会自动关闭该开关

原因：
- 敌人选目标时不应该被玩家镜头方向影响
- 否则会出现“玩家镜头没看向敌人时，敌人反而更难锁到玩家”的反直觉行为

### 13. 敌人导航停止后会清空旧路径缓存
`EnemyNavigationModule.StopAgent()` 现在除了 `ResetPath()`，还会同步清掉：
- `_hasRequestedDestination`
- `_lastRequestedDestination`
- `_nextRepathTime`

原因：
- 之前敌人在攻击、翻越或短暂停止后，可能因为旧的终点缓存仍在，恢复追击时要等一小段时间才重新发路径

当前收束结果：
- 敌人从 Attack/Load/Vault 等状态回到追击时，会更快重新进入导航推进

### 14. 已补“归一化当前场景敌人”菜单
当前新增作者工具：
- `MiniatureGarden/Enemy/Normalize Existing Scene Enemies`

它的用途不是“升级旧敌人”，而是把**已经升级过、已经挂上 EnemyDriver 的场景敌人**重新套一遍当前版本的默认规则，解决这类情况：
- 敌人是在更早版本迁移出来的
- 当时的 `UnitTargetingModule` 还没有关闭摄像机优先
- 当时复制出来的敌人配置里还保留着角色专属策略资产引用

当前菜单会做的事：
- 对场景中已升级敌人重新应用：
  - `EnemyDriver`
  - `EnemyBrainModule`
  - `EnemyNavigationModule`
  - `UnitTargetingModule`
  - `StatusData`
  - `NavMeshAgent`
  - Enemy 层设置
- 对绑定的敌人配置资产执行一次原地规范化：
  - 清空 `conditionSourceAsset / transitionPolicyAsset / attackResolverAsset`
  - 清理假 `Load`
  - 清理旧 `Vault` key

说明：
- 旧敌人第一次接入主链用 `Upgrade Legacy Scene Enemies`
- 已接入过但想追平当前默认规则，用 `Normalize Existing Scene Enemies`

## 相关
- [[modules/player-control]]
- [[modules/skill-system]]
- [[modules/combat-system]]
- [[modules/interaction-system]]

## 2026-06-24 新增收束
### 15. 旧场景霰弹枪敌人的根因不是“缺运行组件”，而是 prefab 本体缺 Animator
- `SampleScene` 里那 5 个 `Enemy_Solider_霰弹枪 (1~5)` 之前静态校验不过，表面提示是“子节点缺少 Animator”
- 继续排查后确认：
  - 场景对象最初确实是半成品 authoring 状态
  - 后续通过归一化链替换成 `Assets/Prefabs/Enemy/Enemy_Solider_霰弹枪.prefab` 之后，问题仍然存在
  - 根因变成了：**敌人 prefab 资产本身没有 Animator**

### 16. 敌人作者链已补“自动确保 Animator 可用”
- `EnemyAuthoringUtility.ConfigureEnemyObject(...)` 现在会先执行 `EnsureAnimatorComponent(target)`
- 当前逻辑：
  - 如果目标对象及其子节点里已经有 Animator，则沿用
  - 如果完全没有 Animator，则在根对象自动补一个 Animator
  - 如果 Animator 没有 `RuntimeAnimatorController`，则优先分配：
    - `Assets/Animations/General.controller`
    - 若该路径不存在，再回退到 `BehaviorCoreAnimatorControllerConvention` 约定的默认控制器路径

这样敌人对象不再依赖“场景里必须先手工挂好 Animator”这件事，迁移/归一化链可以直接把最小可运行 authoring 补齐。

### 17. 场景归一化链已补“缺 Animator 时尝试换成同名敌人 prefab”
- `EnemyAuthoringUtility.EnsureAnimatorReadyEnemyObject(target)` 已加入作者工具链
- 逻辑：
  - 若当前敌人实例没有 Animator
  - 会先尝试按场景对象名称匹配 `Assets/Prefabs/Enemy` 下的同名 prefab
  - 匹配成功则保留原世界位姿/层级顺序/启用状态，用 prefab 实例替换旧对象
  - 再继续套 `EnemyDriver / EnemyBrainModule / EnemyNavigationModule / StatusData / UnitTargetingModule / NavMeshAgent`

说明：
- 这一步解决的是“旧场景敌人对象结构残缺”的问题
- 如果连匹配到的敌人 prefab 本身也缺 Animator，则继续由第 16 条的 `EnsureAnimatorComponent` 兜底

### 18. 霰弹枪敌人 prefab 已落实为正式可运行 authoring
- 已直接补全：
  - `Assets/Prefabs/Enemy/Enemy_Solider_霰弹枪.prefab`
- 当前该 prefab 根节点已经有：
  - `Animator`
  - `RuntimeAnimatorController = Assets/Animations/General.controller`

场景中 5 个 `Enemy_Solider_霰弹枪` 实例也已经同步反映为：
- 根节点带 `Animator`
- 其余敌人运行主链组件齐全：
  - `BehaviorInterpreter`
  - `StatusData`
  - `UnitEffectController`
  - `UnitTargetingModule`
  - `EnemyBrainModule`
  - `NavMeshAgent`
  - `EnemyNavigationModule`
  - `EnemyDriver`

### 19. 当前敌人静态主链的等价证据
由于 Unity Console 当前持续被 `MemoryProfiler` 的 Burst 异常噪音污染，菜单日志证据不稳定，这轮改用等价静态证据收口：
- 场景层级已确认 5 个霰弹枪敌人根节点具备 `Animator`
- 资源读取已确认其 `Animator.runtimeAnimatorController = Assets/Animations/General.controller`
- `Assets/ScriptableObjects/Enemy/Generated/LegacyEnemy_Default.asset` 已确认具备：
  - `Idle`
  - `Move`
  - `MoveJump`
  - `AttackStart`
  - `AttackLoop`
  - `AttackEnd`
- 同一配置还确认：
  - `unitAlignment = Enemy`
  - `supportsAttack = true`
  - `supportsJump = true`
- `Assets/ScriptableObjects/Enemy/Generated/AI/LegacyEnemy_DefaultAI.asset` 已确认具备基础 AI 参数：
  - `targetRefreshInterval = 0.2`
  - `loseTargetDistance = 35`
  - `attackRange = 5.5`
  - `enableVaultDecision = true`

### 20. 当前阶段剩余的最后验证
代码与 authoring 静态链已经基本收束，剩下只建议保留一轮开发者手动运行态验证：
- `Load -> Idle -> Move -> Attack -> Death`
- 选做再看：
  - `Crouch`
  - `Vault`
  - 追击中断后恢复导航

## 2026-06-24 运行态补证
### 21. 当前已经拿到的运行态主链证据
这轮实际进入了 `SampleScene` 的 Play 模式做轻量运行态核验，不再只停留在静态组件检查。

已确认：
- 多个 `Enemy_Solider_霰弹枪` 与 `Enemy_AI_Sample_CH0167` 在运行时都会自动补齐：
  - `AnimatorSegmentPlayer`
- 抽查到的敌人运行态数据中，`EnemyDriver` 已经满足：
  - `IsInitialized = true`
  - `BrainModule.HasTarget = true`
  - `BrainModule.WantsAttack = true`
  - `CurrentStateName = AttackState`
  - `Context.CurrentBehaviorKey = AttackLoop`
- `UnitTargetingModule` 运行态已确认：
  - `runtimeLastResolveSucceeded = true`
  - `runtimeLastTargetName = CH0221`
- 至少部分霰弹枪敌人运行时位置发生变化，并且 `EnemyDriver.LastAcceptedTransitionDescription` 已出现：
  - `Move -> Attack`

这说明当前第一版敌人链路已经不只是“组件挂齐”，而是：
- 进入 Play 后能够初始化
- 读取敌人配置
- 锁定玩家目标
- 驱动 HSM 进入攻击态
- 播放 `AttackLoop`

### 22. 运行态发现并修掉的两个真实问题
#### 22.1 行为事件骨骼路径兼容
运行态曾持续报：
- `未找到骨骼路径：CH0167，行为解释器将退回宿主根节点`

根因：
- 一部分共用行为资源中的 `referenceBone` 仍然写着旧角色根节点名（例如 `CH0167`）
- 当行为资源被敌人对象复用、而敌人宿主根节点名字不同的时候，解释器会按字面路径查找失败

已修正：
- `BehaviorInterpreter.ResolveReferenceTransform(...)`
- `BehaviorInterpreter.ResolveTargetObjectTransformStrict(...)`

当前新增了“宿主根节点前缀兼容”：
- 如果路径第一段像旧角色根节点标记，并且不是宿主当前的直接子节点
- 会自动剥掉第一段，再按当前宿主根节点相对路径继续解析

效果：
- 再次进入 Play 后，该 warning 已不再出现
- 说明行为事件的锚点兼容已补齐

#### 22.2 霰弹枪敌人替换 prefab 后丢失 CharacterController
继续排查后发现：
- 旧场景霰弹枪敌人在被替换成正式 enemy prefab 之后
- 原本场景对象上的 `CharacterController` 丢了
- 导致它们虽然能在攻击距离内直接打，但 `Move / Vault` 链路基础不完整

这里有两个根因：
1. `Enemy_Solider_霰弹枪.prefab` 之前本体没有 `CharacterController`
2. 敌人工具菜单最初是靠 `CharacterController[]` 扫描候选对象
   一旦敌人把 `CharacterController` 丢了，就会直接从工具视野中消失

已修正：
- 正式 prefab `Assets/Prefabs/Enemy/Enemy_Solider_霰弹枪.prefab`
  - 现在已写入 `CharacterController`
- `EnemySceneMigrationMenu.CollectSceneEnemyCandidates(...)`
  - 候选收集改为同时扫描：
    - `CharacterController`
    - `EnemyDriver`
- `EnemyAuthoringUtility.ConfigureEnemyObject(...)`
  - 现在会先 `EnsureCharacterController(target)`
- `EnemyDriver`
  - 已补 `[RequireComponent(typeof(CharacterController))]`

当前结果：
- 场景霰弹枪敌人实例已经重新具备 `CharacterController`
- 再进 Play 时，运行态 `BehaviorInterpreter.Controller` 与 `EnemyDriver.Context.Controller` 已不再是 `null`

### 23. 当前可认为已经完成的范围
以“第一版敌人 AI 主链跑通”为标准，当前已经有足够证据认为代码侧目标完成：
- 敌人运行时中控、AI 决策、导航接口、状态推进已形成闭环
- 行为层明确复用现有角色体系：
  - `BehaviorInterpreter`
  - `AnimatorSegmentPlayer`
  - `CharacterConditions`
  - `DefaultCharacterAttackResolver`
  - `Load/Idle/Move/Attack/Talent/Burst/Reload/Vault/Death` 状态类
- 敌人自己新增的只保留在 AI 决策/导航边界：
  - `EnemyDriver`
  - `EnemyBehaviorRuntime`
  - `EnemyBrainModule`
  - `EnemyNavigationModule`
  - `EnemyTransitionPolicy`

仍然明确留给开发者手动处理的，不属于这次代码闭环缺失：
- NavMesh 烘焙
- NavMeshAgent 调参
- 场景级追击/绕障/翻越体验调试
- 最终数值与攻击节奏校验

## 2026-06-25 敌人移动链再收束
### 24. 敌人位移正式改为 CharacterController 主驱动
- `EnemyNavigationModule` 现在不再让 `NavMeshAgent` 直接推进 Transform。
- 当前职责改为：
  - `NavMeshAgent` 只负责路径规划、`steeringTarget / desiredVelocity` 方向计算、停止距离约束
  - `CharacterController` 负责实际位移、碰撞阻挡、障碍前停住与后续翻越切入
- 实现细节：
  - `NavMeshAgent.updatePosition = false`
  - `NavMeshAgent.updateRotation = false`
  - 每帧把 `agent.nextPosition` 回同步到敌人当前世界位置
  - 敌人实际前进统一走 `CharacterController.SimpleMove(...)`

### 25. 敌人翻越不再依赖 NavMeshLink
- 这一轮已经收掉原本那条 `OffMeshLink -> JumpPressed -> VaultState` 的链路。
- 当前规则改为：
  - 场景里只需要 `CharacterInteractionVolume`
  - 不再要求可翻越障碍额外挂 `NavMeshLink`
  - NavMesh 烘焙侧需要把该类障碍当作“路径可通过但运行时仍有碰撞阻挡”的 authoring 目标
- 也就是说：
  - 寻路层默认认为这条路能走
  - 运行时 `CharacterController` 真撞到障碍/进入交互区时，再由行为系统执行 `MoveJump`

### 26. 翻越触发规则改为“方向 + 范围”双条件
- `CharacterInteractionVolume` 现在新增了按接近方向校验翻越请求的能力。
- `EnemyDriver` 新增 `TryGetVaultRequestForDirection(...)`，会只在以下条件成立时返回翻越请求：
  - 敌人已进入对应交互体范围
  - 该交互体允许翻越
  - 敌人当前寻路想要前进的平面方向，与“障碍物参考点 - 敌人位置”向量夹角在阈值内
- 当前默认阈值先按需求落为 `45` 度，配置在 `EnemyNavigationModule.vaultApproachAngleDegrees`。

### 27. 决策边界调整
- `EnemyBrainModule` 继续负责：
  - 目标刷新
  - 追击 / 攻击主意图
  - 掩体蹲下决策
  - 对外提供翻越冷却与是否启用翻越的配置结果
- `EnemyNavigationModule` 现在负责：
  - 根据导航方向判断“这一帧是否真的在朝障碍物推进”
  - 满足条件时写入 `JumpPressed`
  - 把翻越请求排队到 `EnemyDriver`，供 `DefaultCharacterTransitionPolicy -> CanVault -> VaultState` 消费
- 这样翻越的“要不要跳”就不再只是看是否进了触发器，而是明确依赖当前导航前进方向。

### 28. 交互体命名语义补强
- `CharacterInteractionVolume` 类名暂时未做大范围重命名，避免连带改 prefab / 场景引用。
- 但组件菜单名已改成：
  - `MiniatureGarden/Interaction/Traversal Interaction Volume`
- 当前 authoring 语义已经明确成“通用翻越/掩体交互体”，不再只服务玩家角色。

### 29. 当前这轮代码验证结果
- `dotnet build Assembly-CSharp.csproj -nologo` 已通过
- 无新增编译错误
- 仍存在项目内原有 warning（URP 过时 API / XLua 示例 / 外部程序集版本冲突），与这轮敌人移动改造无关
- 敌人翻越触发现已分三层：
  - 已注册交互体 + 方向夹角匹配时直接触发
  - 若本帧被侧向碰撞阻挡，导航模块会做一次前方 OverlapSphere 探测
  - 若前向探测命中可翻越 `CharacterInteractionVolume` 且方向满足阈值，则立即排队翻越请求并写入 `JumpPressed`
- 这层兜底的目的，是避免 `CharacterController` 已经顶到矮墙，但因为触发注册时序略晚而一直不跳。
- 当前敌人翻越最终链路：
  - `EnemyNavigationModule` 负责判定需要翻越时机，并提前把 `CharacterVaultRequest` 写入 `CharacterContext` 的 pending 缓存
  - `CharacterConditions.CanVault()` 若发现 `HasPendingVaultRequest = true`，直接放行，不再额外调用 `InteractionSource.TryGetVaultRequest(...)`
  - `VaultState.TryInitializeVault()` 进入时优先消费 pending 缓存，这样不会再出现“校验阶段吃掉请求，真正切入状态时反而没有请求”的问题
- 2026-06-25 再补两条敌人翻越根因：
  - 敌人配置资源 `UnitAssetInformation_Solider_ShotGun.asset` 仍使用旧 key `Vault`，而当前运行时按 `MoveJump` 取行为，导致翻越条件始终不过。现已做两层处理：资源归正为 `MoveJump`，运行时 `UnitAssetInformation.GetBehaviorGroup()` 也补了 `MoveJump <-> Vault` 别名兼容。
  - `SampleScene` 中样例矮墙的 `CharacterInteractionVolume` 所在 `BoxCollider` 原本不是 trigger，导致交互体注册链失效。现已把场景对象修正为 trigger，并在脚本 `OnValidate()` 中强制兜底。
- 2026-06-25 再补一条敌人翻越运行态根因：旧的 `EnemyDriver._queuedVaultRequest` 与新的 `CharacterContext` pending vault request 共存，会在翻越结束后再次提供“障碍前起点”的旧请求，表现为敌人翻过去、走几步、突然闪回原位再翻。当前已删除敌人翻越流程对旧队列的使用，翻越请求统一收束到 `CharacterContext`。
