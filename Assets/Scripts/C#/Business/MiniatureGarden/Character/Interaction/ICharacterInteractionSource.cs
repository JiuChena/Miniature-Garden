/// <summary>
/// 角色交互来源。用于查询掩体蹲下与翻越障碍等环境交互信息。
/// </summary>
public interface ICharacterInteractionSource
{
    bool IsInCoverInteractionRange(CharacterContext context);
    bool TryGetVaultRequest(CharacterContext context, out CharacterVaultRequest request);
}
