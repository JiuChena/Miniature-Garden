using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 鍙厤缃埌 UnitAssetInformation 鐨勬潯浠舵簮璧勪骇鍩虹被銆?/// 灞炰簬褰撳墠椤圭洰绛栫暐璧勪骇锛屼笉灞炰簬杩愯鏃舵ˉ鎺ュ眰銆?/// </summary>
[MovedFrom(false, null, null, "CharacterConditionSourceAsset")]
public abstract class UnitConditionSourceAsset : ScriptableObject, ICharacterConditionSource
{
    public abstract CharacterConditions CreateConditions(CharacterContext context);
}
