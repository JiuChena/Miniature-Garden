using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 鍙厤缃埌 UnitAssetInformation 鐨勮浆鎹㈢瓥鐣ヨ祫浜у熀绫汇€?/// 灞炰簬褰撳墠椤圭洰绛栫暐璧勪骇锛屼笉灞炰簬杩愯鏃舵ˉ鎺ュ眰銆?/// </summary>
[MovedFrom(false, null, null, "CharacterTransitionPolicyAsset")]
public abstract class UnitTransitionPolicyAsset : ScriptableObject, ICharacterTransitionPolicy
{
    public abstract void Initialize(CharacterContext context);
    public abstract bool TryResolveTransition(CharacterStateId currentState, out CharacterTransitionRequest request);
}
