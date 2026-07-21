using BehaviorCore;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 鍙厤缃埌 UnitAssetInformation 鐨勬敾鍑昏В鏋愬櫒璧勪骇鍩虹被銆?/// 灞炰簬褰撳墠椤圭洰绛栫暐璧勪骇锛屼笉灞炰簬杩愯鏃舵ˉ鎺ュ眰銆?/// </summary>
[MovedFrom(false, null, null, "CharacterAttackResolverAsset")]
public abstract class UnitAttackResolverAsset : ScriptableObject, ICharacterAttackResolver
{
    public abstract void Initialize(CharacterContext context);
    public abstract bool TryResolveEnterAttack(out CharacterAttackPlayRequest request);
    public abstract bool TryResolveLoopAttack(int currentLoopIndex, BehaviorClip completedClip, out CharacterAttackPlayRequest request);
    public abstract bool TryResolveEndAttack(BehaviorClip completedClip, out CharacterAttackPlayRequest request);
    public abstract string ResolveLoopGroupKey();
}
