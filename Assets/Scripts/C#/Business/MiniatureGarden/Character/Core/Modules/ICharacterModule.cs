using CoreFramework;

/// <summary>
/// CharacterDriver 运行时模块接口。
/// </summary>
public interface ICharacterModule
{
    void Initialize(CharacterDriver owner, CharacterContext context);
    void OnOwnerEnabled();
    void OnOwnerDisabled();
    void Tick(Blackboard board, float deltaTime);
    void LateTick(Blackboard board, float deltaTime);
    void Dispose();
}
