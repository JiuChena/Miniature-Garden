using System;
using System.Collections.Generic;
using MessagePack;

/// <summary>
/// 角色存档根对象。
/// </summary>
[Serializable]
[MessagePackObject]
public class CharacterDataStorage
{
    [Key(0)]
    public List<CharacterDataEntry> characters = new List<CharacterDataEntry>();
}
