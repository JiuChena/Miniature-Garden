---
tags: [convention, preferences]
created: 2026-06-19
updated: 2026-06-20
---

# 技术决策偏好

## 语言
- 纯 C#，不使用 Lua/XLua

## 数据持久化
- MessagePack（二进制、类型安全）
- 拒绝 BinaryFormatter

## 资源加载
- Addressables 唯一通道
- 不使用 Resources 文件夹

## 模块化
- 上帝对象必须拆分，每个 Module ≤200 行
- 通过接口解耦，不直接依赖具体类

## 事件
- EventCenter 发布-订阅
- TypedEventBus 处理 struct 型跨系统通知
- 事件名用 `EventNames` 常量类

## 技能系统
- ScriptableObject 数据驱动
- BehaviorClip → BehaviorInterpreter 时间轴调度
- 行为表现优先用 Timeline 原生轨道预览，再导出成 BehaviorClip
- 角色专属逻辑优先通过资源配置 + 可替换策略差异化

## 单例
- 无生命周期的管理器用纯 C# 单例
- 需要 Update/协程的用懒创建 MonoBehaviour

## HSM
- 只依赖接口，不与具体类耦合
- 状态通过 TransitionPolicy 切换

## 相机
- Cinemachine
- 跟随目标是 Player 根节点空间锚点，不直接跟随具体角色
