using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 鍗曟潯瑙掕壊鎶€鑳芥暟鍊煎畾涔夈€?/// </summary>
[Serializable]
[MovedFrom(false, null, null, "CharacterAbilityNumericEntry")]
public class UnitAbilityNumericEntry
{
    [Tooltip("数值定义唯一 key，例如 Attack_1_Bullet_1。")]
    public string key;

    [Tooltip("该数值条目使用哪一类能力等级来解析。")]
    public UnitAbilityLevelGroup levelGroup = UnitAbilityLevelGroup.Independent;

    [Tooltip("该条目各等级对应的最终倍率或基础值表，运行时按等级索引读取。")]
    public float[] levelValues = Array.Empty<float>();
}
