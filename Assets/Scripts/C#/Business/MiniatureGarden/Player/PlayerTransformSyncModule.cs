using UnityEngine;

/// <summary>
/// 旧版位姿同步模块迁移占位器。
/// 功能已并入 PlayerMovementModule，保留该脚本仅用于自动清理场景中的旧组件引用。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class PlayerTransformSyncModule : MonoBehaviour
{
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
