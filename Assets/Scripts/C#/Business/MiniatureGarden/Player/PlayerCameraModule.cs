using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家相机模块：全权负责把玩家镜头跟随目标同步到 Player 根节点。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCameraModule : MonoBehaviour, IPlayerModule
{
    [Header("Camera")]
    [FormerlySerializedAs("syncCameraToCurrentCharacter")]
    [SerializeField, Tooltip("启用后自动把玩家相机跟随目标绑定到 Player 根节点。")]
    private bool syncCameraToPlayerRoot = true;

    private PlayerController _owner;

    public void Initialize(PlayerController owner, PlayerContext context)
    {
        _owner = owner;
    }

    public void Enable()
    {
    }

    public void Disable()
    {
    }

    public void Tick(CoreFramework.Blackboard board, float deltaTime)
    {
        
    }

    public void FollowPlayerRoot()
    {
        if (_owner == null || !syncCameraToPlayerRoot)
            return;

        if (PlayerCameraController.Instance == null)
            return;

        PlayerCameraController.Instance.SetFollowTarget(_owner.transform);
    }

    public void SetFollowSyncEnabled(bool enabled)
    {
        syncCameraToPlayerRoot = enabled;
    }

    
}
