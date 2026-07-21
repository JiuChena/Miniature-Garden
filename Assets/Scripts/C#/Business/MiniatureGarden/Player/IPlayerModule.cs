using CoreFramework;

/// <summary>
/// 玩家模块接口。由 PlayerController 统一创建与调度。
/// </summary>
public interface IPlayerModule
{
    void Initialize(PlayerController owner, PlayerContext context);
    void Enable();
    void Disable();
    void Tick(Blackboard board, float deltaTime);
}
