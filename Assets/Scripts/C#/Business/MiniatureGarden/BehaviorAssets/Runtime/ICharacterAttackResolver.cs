using BehaviorCore;

/// <summary>
/// 攻击解析器接口。用于决定“进入攻击时播哪种起手”“持续攻击播哪组行为”“收尾时播哪种结束动作”。
/// </summary>
public interface ICharacterAttackResolver
{
    void Initialize(CharacterContext context);
    bool TryResolveEnterAttack(out CharacterAttackPlayRequest request);
    bool TryResolveLoopAttack(int currentLoopIndex, BehaviorClip completedClip, out CharacterAttackPlayRequest request);
    bool TryResolveEndAttack(BehaviorClip completedClip, out CharacterAttackPlayRequest request);
    string ResolveLoopGroupKey();
}
