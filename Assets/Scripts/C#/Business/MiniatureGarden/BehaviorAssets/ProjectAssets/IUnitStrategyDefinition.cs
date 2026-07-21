/// <summary>
/// 瑙掕壊杩愯鏃剁瓥鐣ヨ祫浜у绾︺€?/// </summary>
public interface IUnitStrategyDefinition
{
    UnitConditionSourceAsset ConditionSourceAsset { get; }
    UnitTransitionPolicyAsset TransitionPolicyAsset { get; }
    UnitAttackResolverAsset AttackResolverAsset { get; }
}
