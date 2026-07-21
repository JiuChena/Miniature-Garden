/// <summary>
/// 场景交互体接口。用于向角色声明当前交互区域是否允许蹲下/翻越，并提供翻越参数。
/// </summary>
public interface ICharacterInteractionVolume
{
    bool AllowsCover { get; }
    bool AllowsVault { get; }
    bool TryBuildVaultRequest(CharacterContext context, out CharacterVaultRequest request);
}

/// <summary>
/// 交互体接收器。用于让玩家、敌人等不同驱动都能接收同一套掩体/翻越触发体通知。
/// </summary>
public interface ICharacterInteractionVolumeReceiver
{
    void RegisterInteractionVolume(ICharacterInteractionVolume volume);
    void UnregisterInteractionVolume(ICharacterInteractionVolume volume);
}
