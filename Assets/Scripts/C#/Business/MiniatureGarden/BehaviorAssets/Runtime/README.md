# MiniatureGarden SkillCore Runtime Adapters

这一层只承载当前项目对 `Framework/SkillCore` 运行时链路的项目适配，不再混放项目配置资产。

## 当前物理状态

- 当前目录仍是单层物理结构
- 这样做是为了避免 Unity 编辑器打开期间，外部 `.csproj` 同步不到真实源码路径的问题再次出现
- 但逻辑职责已经按三类收口，后续若 Unity 工程同步问题解决，可以再做物理细分

## 当前逻辑分层

- 运行时桥接类
  - 负责把 `BehaviorInterpreter`、投射物索敌、VFX/Audio/Projectile 事件桥接到当前项目运行时
  - 例如 `CharacterBehaviorRuntime`、`CharacterBehaviorEventReceiver`、`CharacterTargetingUtility`

- 项目行为规则类
  - 负责默认攻击解析、默认切换策略、条件源接口等“当前项目如何解释行为”的规则实现
  - 例如 `DefaultCharacterAttackResolver`、`DefaultCharacterTransitionPolicy`

- 共享数据类
  - 放请求结构、状态枚举、姿态枚举、播放阶段枚举等纯数据类型
  - 例如 `CharacterTransitionRequest`、`CharacterAttackPlayRequest`、`CharacterStateId`

## 边界规则

- `Runtime/` 只保留运行时桥接、运行时规则实现和共享数据
- `ProjectAssets/` 负责项目侧 `ScriptableObject` 资产与策略资产基类
- `Framework/SkillCore` 不应该反向依赖这里
- 新项目如果要复用框架，应自己重建这一层，而不是直接照搬当前实现
