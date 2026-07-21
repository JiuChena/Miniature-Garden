/// <summary>
/// 敌人默认转换策略。
/// 当前首版直接复用默认角色规则，只在 Load 状态期间禁止常规行为切换。
/// </summary>
public sealed class EnemyTransitionPolicy : ICharacterTransitionPolicy
{
    private readonly DefaultCharacterTransitionPolicy _fallback = new DefaultCharacterTransitionPolicy();

    public void Initialize(CharacterContext context)
    {
        _fallback.Initialize(context);
    }

    public bool TryResolveTransition(CharacterStateId currentState, out CharacterTransitionRequest request)
    {
        if (currentState == CharacterStateId.Load)
        {
            request = default;
            return false;
        }

        return _fallback.TryResolveTransition(currentState, out request);
    }
}
