using System;
using BehaviorCore;
using UnityEngine;

/// <summary>
/// 角色行为表中的单条行为组配置。
/// </summary>
[Serializable]
public class BehaviorEntry
{
    [Tooltip("行为唯一键。建议核心行为统一使用 BehaviorKeys 中的常量名称")]
    public string key;

    [Tooltip("该行为键对应的一组 BehaviorClip。单段行为填一个元素，连段或多候选行为可填多个")]
    public BehaviorClip[] clips = Array.Empty<BehaviorClip>();
}