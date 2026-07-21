using System;
using UnityEngine;

/// <summary>
/// 角色主属性线性成长公式。最终基础值 = A * (Level - 1) + C。
/// </summary>
[Serializable]
public sealed class CharacterPrimaryStatFormula
{
    [Tooltip("角色 1 级时的基础数值，对应公式中的 C")]
    [Min(0f)]
    public float baseValueAtLevel1 = 1f;

    [Tooltip("角色每提升 1 级时增加的成长值，对应公式中的 A")]
    public float growthPerLevel;

    public float Evaluate(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return Mathf.Max(0f, growthPerLevel * (safeLevel - 1) + baseValueAtLevel1);
    }
}
