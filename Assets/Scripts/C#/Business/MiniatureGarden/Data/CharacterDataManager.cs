using System.Collections.Generic;
using CoreFramework;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

/// <summary>
/// 角色可变数据管理器。负责玩家拥有角色的等级与技能等级本地持久化。
/// </summary>
public class CharacterDataManager
{
    private const string SaveFolder = "PlayerData/Character/";
    private const string SaveFileName = "CharacterData";

    private static CharacterDataManager instance;
    public static CharacterDataManager Instance => instance ??= new CharacterDataManager();

    private readonly Dictionary<int, CharacterData> characterDataMap = new Dictionary<int, CharacterData>();
    private static readonly CharacterData DefaultCharacterData = new CharacterData();
    private CharacterDataStorage storage;
    private bool isLoaded;

    private CharacterDataManager()
    {
        CharacterDataMessagePackRegistration.EnsureRegistered();
        LoadData();
    }

    public IReadOnlyDictionary<int, CharacterData> CharacterDataMap => characterDataMap;
    public event System.Action<int, CharacterData> CharacterDataChanged;

    public void LoadData()
    {
        storage = BinaryDataManager.Instance.Load<CharacterDataStorage>(SaveFolder, SaveFileName) ?? new CharacterDataStorage();
        characterDataMap.Clear();

        if (storage.characters == null)
            storage.characters = new List<CharacterDataEntry>();

        for (int i = 0; i < storage.characters.Count; i++)
        {
            CharacterDataEntry entry = storage.characters[i];
            if (entry == null || entry.characterId <= 0)
                continue;

            entry.data ??= new CharacterData();
            characterDataMap[entry.characterId] = entry.data;
        }

        isLoaded = true;
    }

    public void SaveData()
    {
        if (!isLoaded)
            return;

        storage ??= new CharacterDataStorage();
        storage.characters.Clear();
        foreach (KeyValuePair<int, CharacterData> pair in characterDataMap)
        {
            if (pair.Key <= 0 || pair.Value == null)
                continue;

            storage.characters.Add(new CharacterDataEntry
            {
                characterId = pair.Key,
                data = pair.Value
            });
        }

        BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, storage);
    }

    public bool TryGetCharacterData(int characterId, out CharacterData data)
    {
        EnsureLoaded();
        if (characterDataMap.TryGetValue(characterId, out data) && data != null)
            return true;

        data = CreateDefaultCharacterData();
        return false;
    }

    public CharacterData GetCharacterDataOrDefault(int characterId)
    {
        EnsureLoaded();
        if (characterId > 0 &&
            characterDataMap.TryGetValue(characterId, out CharacterData existingData) &&
            existingData != null)
        {
            return existingData;
        }

        return CreateDefaultCharacterData();
    }

    public CharacterData GetOrCreateCharacterData(int characterId)
    {
        EnsureLoaded();
        if (characterId <= 0)
            return new CharacterData();

        if (characterDataMap.TryGetValue(characterId, out CharacterData existingData) && existingData != null)
            return existingData;

        CharacterData createdData = new CharacterData();
        characterDataMap[characterId] = createdData;
        SaveData();
        NotifyCharacterDataChanged(characterId, createdData);
        return createdData;
    }

    public void SetCharacterLevel(int characterId, int level)
    {
        CharacterData data = GetOrCreateCharacterData(characterId);
        data.characterLevel = Mathf.Max(1, level);
        SaveData();
        NotifyCharacterDataChanged(characterId, data);
    }

    public void SetAttackLevel(int characterId, int level)
    {
        CharacterData data = GetOrCreateCharacterData(characterId);
        data.attackLevel = Mathf.Max(1, level);
        SaveData();
        NotifyCharacterDataChanged(characterId, data);
    }

    public void SetTalentLevel(int characterId, int level)
    {
        CharacterData data = GetOrCreateCharacterData(characterId);
        data.talentLevel = Mathf.Max(1, level);
        SaveData();
        NotifyCharacterDataChanged(characterId, data);
    }

    public void SetBurstLevel(int characterId, int level)
    {
        CharacterData data = GetOrCreateCharacterData(characterId);
        data.burstLevel = Mathf.Max(1, level);
        SaveData();
        NotifyCharacterDataChanged(characterId, data);
    }

    public bool RemoveCharacterData(int characterId)
    {
        EnsureLoaded();
        if (!characterDataMap.Remove(characterId))
            return false;

        SaveData();
        return true;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded)
            LoadData();
    }

    private static CharacterData CreateDefaultCharacterData()
    {
        return new CharacterData
        {
            characterLevel = DefaultCharacterData.characterLevel,
            attackLevel = DefaultCharacterData.attackLevel,
            talentLevel = DefaultCharacterData.talentLevel,
            burstLevel = DefaultCharacterData.burstLevel,
        };
    }

    private void NotifyCharacterDataChanged(int characterId, CharacterData data)
    {
        CharacterDataChanged?.Invoke(characterId, data ?? CreateDefaultCharacterData());
    }
}

internal static class CharacterDataMessagePackRegistration
{
    private static bool isRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterOnLoad()
    {
        EnsureRegistered();
    }

    public static void EnsureRegistered()
    {
        if (isRegistered)
            return;

        ProjectSaveResolver.Register(new CharacterDataFormatter());
        ProjectSaveResolver.Register(new CharacterDataEntryFormatter());
        ProjectSaveResolver.Register(new CharacterDataStorageFormatter());
        isRegistered = true;
    }
}

internal sealed class CharacterDataFormatter : IMessagePackFormatter<CharacterData>
{
    public void Serialize(ref MessagePackWriter writer, CharacterData value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(4);
        writer.Write(value.characterLevel);
        writer.Write(value.attackLevel);
        writer.Write(value.talentLevel);
        writer.Write(value.burstLevel);
    }

    public CharacterData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil()) return null;
        int count = reader.ReadArrayHeader();
        CharacterData value = new CharacterData();

        for (int i = 0; i < count; i++)
        {
            switch (i)
            {
                case 0:
                    value.characterLevel = reader.ReadInt32();
                    break;
                case 1:
                    value.attackLevel = reader.ReadInt32();
                    break;
                case 2:
                    value.talentLevel = reader.ReadInt32();
                    break;
                case 3:
                    value.burstLevel = reader.ReadInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return value;
    }
}

internal sealed class CharacterDataEntryFormatter : IMessagePackFormatter<CharacterDataEntry>
{
    public void Serialize(ref MessagePackWriter writer, CharacterDataEntry value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(2);
        writer.Write(value.characterId);
        options.Resolver.GetFormatterWithVerify<CharacterData>().Serialize(ref writer, value.data, options);
    }

    public CharacterDataEntry Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil()) return null;
        int count = reader.ReadArrayHeader();
        CharacterDataEntry value = new CharacterDataEntry();

        for (int i = 0; i < count; i++)
        {
            switch (i)
            {
                case 0:
                    value.characterId = reader.ReadInt32();
                    break;
                case 1:
                    value.data = options.Resolver.GetFormatterWithVerify<CharacterData>().Deserialize(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        value.data ??= new CharacterData();
        return value;
    }
}

internal sealed class CharacterDataStorageFormatter : IMessagePackFormatter<CharacterDataStorage>
{
    public void Serialize(ref MessagePackWriter writer, CharacterDataStorage value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<List<CharacterDataEntry>>().Serialize(ref writer, value.characters, options);
    }

    public CharacterDataStorage Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil()) return null;
        int count = reader.ReadArrayHeader();
        CharacterDataStorage value = new CharacterDataStorage();

        for (int i = 0; i < count; i++)
        {
            switch (i)
            {
                case 0:
                    value.characters = options.Resolver.GetFormatterWithVerify<List<CharacterDataEntry>>().Deserialize(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        value.characters ??= new List<CharacterDataEntry>();
        return value;
    }
}
