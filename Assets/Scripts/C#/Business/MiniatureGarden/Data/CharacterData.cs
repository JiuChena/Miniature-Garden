using System;
using MessagePack;

/// <summary>
/// 玩家拥有的单个角色的可变培养数据。
/// </summary>
[Serializable]
[MessagePackObject]
public class CharacterData
{
    [Key(0)]
    public int characterLevel = 1;

    [Key(1)]
    public int attackLevel = 1;

    [Key(2)]
    public int talentLevel = 1;

    [Key(3)]
    public int burstLevel = 1;
}
