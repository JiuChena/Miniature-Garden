using System;
using MessagePack;

/// <summary>
/// 角色存档条目。使用列表包装角色 ID 与数据，避免直接把可变字典作为持久化根结构。
/// </summary>
[Serializable]
[MessagePackObject]
public class CharacterDataEntry
{
    [Key(0)]
    public int characterId;

    [Key(1)]
    public CharacterData data = new CharacterData();
}
