using CoreFramework;
using UnityEngine;

/// <summary>
/// 旧版地面移动策略迁移占位器。
/// 默认玩家地面移动已并入 PlayerMovementModule，保留该脚本仅用于自动清理旧组件引用。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class GroundMovement : MonoBehaviour, IMovementStrategy
{
    public void Execute(Blackboard board, CharacterController cc)
    {
    }

    public void ApplyUnitConfig(IUnitDefinition config)
    {
    }

    public void StopImmediately()
    {
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        ScheduleRemoval();
    }

    private void OnValidate()
    {
        ScheduleRemoval();
    }

    private void ScheduleRemoval()
    {
        UnityEditor.EditorApplication.delayCall -= RemoveIfStillExists;
        UnityEditor.EditorApplication.delayCall += RemoveIfStillExists;
    }

    private void RemoveIfStillExists()
    {
        if (this == null)
            return;

        if (TryGetComponent(out PlayerMovementModule _))
            DestroyImmediate(this);
    }
#else
    private void Awake()
    {
        if (TryGetComponent(out PlayerMovementModule _))
            Destroy(this);
    }
#endif
}
