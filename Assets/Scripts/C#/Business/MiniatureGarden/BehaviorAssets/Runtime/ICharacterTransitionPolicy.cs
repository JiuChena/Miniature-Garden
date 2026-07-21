/// <summary>
/// 角色行为转换策略接口，用于决定通用状态机在当前角色上的切换规则。
/// </summary>
public interface ICharacterTransitionPolicy
{
    void Initialize(CharacterContext context);
    bool TryResolveTransition(CharacterStateId currentState, out CharacterTransitionRequest request);
}
