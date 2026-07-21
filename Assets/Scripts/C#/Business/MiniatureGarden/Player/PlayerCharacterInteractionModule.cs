using UnityEngine;

/// <summary>
/// 旧版角色交互模块迁移占位器。
/// 功能已并入 PlayerInputModule，保留该脚本仅用于自动清理场景中的旧组件引用。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class PlayerCharacterInteractionModule : MonoBehaviour
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

        if (TryGetComponent(out PlayerInputModule _))
            DestroyImmediate(this);
    }
#else
    private void Awake()
    {
        if (TryGetComponent(out PlayerInputModule _))
            Destroy(this);
    }
#endif
}
