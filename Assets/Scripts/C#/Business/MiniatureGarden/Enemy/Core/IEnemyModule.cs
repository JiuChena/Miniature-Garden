using CoreFramework;

/// <summary>
/// EnemyDriver 运行时模块接口。
/// 负责把 AI、导航等敌人侧能力拆成可替换模块，由 Driver 统一调度。
/// </summary>
public interface IEnemyModule
{
    void Initialize(EnemyDriver owner, CharacterContext context);
    void OnOwnerEnabled();
    void OnOwnerDisabled();
    void Tick(Blackboard board, float deltaTime);
    void LateTick(Blackboard board, float deltaTime);
    void Dispose();
}
