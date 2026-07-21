using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using MessagePack.Unity;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 二进制数据管理器，基于 MessagePack 实现持久化数据的序列化与反序列化。
    /// 数据存储于 Application.persistentDataPath/Data/ 目录下。
    /// </summary>
    public class BinaryDataManager
    {
        private static readonly BinaryDataManager _instance = new BinaryDataManager();
        public static BinaryDataManager Instance => _instance;

        private static readonly string DataPath = Path.Combine(Application.persistentDataPath, "Data");
        private readonly MessagePackRuntime _runtime = new MessagePackRuntime();

        private BinaryDataManager() { }

        /// <summary>
        /// 将对象序列化为 MessagePack 二进制并写入文件。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="data">要保存的数据对象</param>
        public void Save<T>(string path, string fileName, T data)
        {
            string directoryPath = GetDirectoryPath(path);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            byte[] bytes = _runtime.Serialize(data);
            File.WriteAllBytes(GetFilePath(path, fileName), bytes);
        }

        /// <summary>
        /// 从文件读取 MessagePack 二进制并反序列化为对象。文件不存在或反序列化失败时返回 default。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>反序列化后的对象，失败返回 default(T)</returns>
        public T Load<T>(string path, string fileName)
        {
            string filePath = GetFilePath(path, fileName);
            if (!File.Exists(filePath))
                return default;

            byte[] bytes = File.ReadAllBytes(filePath);
            return _runtime.Deserialize<T>(bytes);
        }

        /// <summary>
        /// 检查指定文件是否存在。
        /// </summary>
        public bool FileExists(string fileName)
        {
            return File.Exists(Path.Combine(DataPath, fileName + ".bin"));
        }

        /// <summary>
        /// 检查指定路径和名称的文件是否存在。
        /// </summary>
        public bool FileExists(string path, string fileName)
        {
            return File.Exists(GetFilePath(path, fileName));
        }

        private static string GetDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return DataPath;

            string normalizedPath = path.Replace("\\", "/").Trim('/');
            return string.IsNullOrEmpty(normalizedPath) ? DataPath : Path.Combine(DataPath, normalizedPath);
        }

        private static string GetFilePath(string path, string fileName)
        {
            return Path.Combine(GetDirectoryPath(path), fileName + ".bin");
        }
    }

    /// <summary>
    /// 通用数据容器，以 object 列表形式存储异构数据，用于兼容旧代码。
    /// </summary>
    [MessagePackObject]
    public class GenericDataContainer
    {
        [Key(0)]
        public List<GenericDataValue> serializedData = new List<GenericDataValue>();

        public List<object> data = new List<object>();

        public void LoadData(string dataPath, string fileName)
        {
            GenericDataContainer storage = BinaryDataManager.Instance.Load<GenericDataContainer>(dataPath, fileName);
            if (storage == null)
            {
                serializedData = new List<GenericDataValue>();
                data = new List<object>();
                return;
            }

            serializedData = storage.serializedData ?? new List<GenericDataValue>();
            data = new List<object>(serializedData.Count);
            for (int i = 0; i < serializedData.Count; i++)
                data.Add(serializedData[i].ToObject());
        }

        public void SaveData(string dataPath, string fileName)
        {
            serializedData.Clear();

            for (int i = 0; i < data.Count; i++)
                serializedData.Add(GenericDataValue.FromObject(data[i]));

            BinaryDataManager.Instance.Save(dataPath, fileName, this);
        }

        /// <summary>
        /// 清空并重新填充数据。
        /// </summary>
        public void PushData(params object[] items)
        {
            data.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
                data.Add(items[i]);
        }

        /// <summary>
        /// 获取指定索引的数据，超出范围返回 default。
        /// </summary>
        public object GetDataAt(int index)
        {
            return data.Count > index ? data[index] : default;
        }
    }

    [MessagePackObject]
    public class GenericDataValue
    {
        public enum ValueType
        {
            Null = 0,
            Int = 1,
            Float = 2,
            Bool = 3,
            String = 4,
            Long = 5,
            Double = 6
        }

        [Key(0)]
        public ValueType type;

        [Key(1)]
        public int intValue;

        [Key(2)]
        public float floatValue;

        [Key(3)]
        public bool boolValue;

        [Key(4)]
        public string stringValue;

        [Key(5)]
        public long longValue;

        [Key(6)]
        public double doubleValue;

        public static GenericDataValue FromObject(object value)
        {
            if (value == null)
                return new GenericDataValue { type = ValueType.Null };
            if (value is int intValue)
                return new GenericDataValue { type = ValueType.Int, intValue = intValue };
            if (value is float floatValue)
                return new GenericDataValue { type = ValueType.Float, floatValue = floatValue };
            if (value is bool boolValue)
                return new GenericDataValue { type = ValueType.Bool, boolValue = boolValue };
            if (value is string stringValue)
                return new GenericDataValue { type = ValueType.String, stringValue = stringValue };
            if (value is long longValue)
                return new GenericDataValue { type = ValueType.Long, longValue = longValue };
            if (value is double doubleValue)
                return new GenericDataValue { type = ValueType.Double, doubleValue = doubleValue };

            Debug.LogWarning($"GenericDataContainer 不支持持久化类型 {value.GetType().FullName}，将退化为字符串存储。");
            return new GenericDataValue
            {
                type = ValueType.String,
                stringValue = value.ToString()
            };
        }

        public object ToObject()
        {
            switch (type)
            {
                case ValueType.Int:
                    return intValue;
                case ValueType.Float:
                    return floatValue;
                case ValueType.Bool:
                    return boolValue;
                case ValueType.String:
                    return stringValue;
                case ValueType.Long:
                    return longValue;
                case ValueType.Double:
                    return doubleValue;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// MessagePack 运行时封装。统一固定项目使用的序列化选项，避免依赖脆弱的反射签名匹配。
    /// </summary>
    internal sealed class MessagePackRuntime
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackRuntime()
        {
            _options = MessagePackSerializerOptions.Standard.WithResolver(
                CompositeResolver.Create(ProjectSaveResolver.Instance, UnityResolver.InstanceWithStandardResolver));
        }

        public byte[] Serialize<T>(T data)
        {
            return MessagePackSerializer.Serialize(data, _options);
        }

        /// <summary>
        /// 反序列化字节数组为指定类型。旧版 typeless 数据或格式不兼容时返回 default。
        /// </summary>
        public T Deserialize<T>(byte[] bytes)
        {
            try
            {
                return MessagePackSerializer.Deserialize<T>(bytes, _options);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"MessagePack 反序列化失败，将使用默认数据。"
                    + $"\n  目标类型: {typeof(T).FullName}"
                    + $"\n  内部异常: {ex.InnerException?.Message ?? ex.Message}");
                return default;
            }
        }
    }

    public sealed class ProjectSaveResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ProjectSaveResolver();
        private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>();

        static ProjectSaveResolver()
        {
            Register(new AudioDataFormatter());
            Register(new BagDataFormatter());
            Register(new QuestProgressFormatter());
            Register(new QuestSaveDataFormatter());
            Register(new GenericDataContainerFormatter());
            Register(new GenericDataValueFormatter());
        }

        private ProjectSaveResolver() { }

        public static void Register<T>(IMessagePackFormatter<T> formatter)
        {
            FormatterMap[typeof(T)] = formatter;
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterMap.TryGetValue(typeof(T), out object formatter) ? (IMessagePackFormatter<T>)formatter : null;
        }
    }

    internal sealed class AudioDataFormatter : IMessagePackFormatter<global::AudioData>
    {
        public void Serialize(ref MessagePackWriter writer, global::AudioData value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(4);
            writer.Write(value.musicEnabled);
            writer.Write(value.musicVolume);
            writer.Write(value.soundEnabled);
            writer.Write(value.soundVolume);
        }

        public global::AudioData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            global::AudioData value = new global::AudioData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.musicEnabled = reader.ReadBoolean();
                        break;
                    case 1:
                        value.musicVolume = reader.ReadSingle();
                        break;
                    case 2:
                        value.soundEnabled = reader.ReadBoolean();
                        break;
                    case 3:
                        value.soundVolume = reader.ReadSingle();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return value;
        }
    }

    internal sealed class BagDataFormatter : IMessagePackFormatter<BagData>
    {
        public void Serialize(ref MessagePackWriter writer, BagData value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(2);
            writer.Write(value.currency);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>().Serialize(ref writer, value.stackableItems, options);
        }

        public BagData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            BagData value = new BagData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.currency = reader.ReadInt32();
                        break;
                    case 1:
                        value.stackableItems = options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            value.stackableItems ??= new Dictionary<string, int>();
            return value;
        }
    }

    internal sealed class QuestProgressFormatter : IMessagePackFormatter<QuestProgress>
    {
        public void Serialize(ref MessagePackWriter writer, QuestProgress value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(3);
            writer.Write(value.questID);
            writer.Write(value.currentStageIndex);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>().Serialize(ref writer, value.conditionProgress, options);
        }

        public QuestProgress Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            QuestProgress value = new QuestProgress();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.questID = reader.ReadString();
                        break;
                    case 1:
                        value.currentStageIndex = reader.ReadInt32();
                        break;
                    case 2:
                        value.conditionProgress = options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            value.conditionProgress ??= new Dictionary<string, int>();
            return value;
        }
    }

    internal sealed class QuestSaveDataFormatter : IMessagePackFormatter<QuestSaveData>
    {
        public void Serialize(ref MessagePackWriter writer, QuestSaveData value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<List<string>>().Serialize(ref writer, value.completedQuestIDs, options);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, QuestProgress>>().Serialize(ref writer, value.activeQuests, options);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, long>>().Serialize(ref writer, value.dailyLastClaimTime, options);
        }

        public QuestSaveData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            QuestSaveData value = new QuestSaveData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.completedQuestIDs = options.Resolver.GetFormatterWithVerify<List<string>>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        value.activeQuests = options.Resolver.GetFormatterWithVerify<Dictionary<string, QuestProgress>>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        value.dailyLastClaimTime = options.Resolver.GetFormatterWithVerify<Dictionary<string, long>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            value.completedQuestIDs ??= new List<string>();
            value.activeQuests ??= new Dictionary<string, QuestProgress>();
            value.dailyLastClaimTime ??= new Dictionary<string, long>();
            return value;
        }
    }

    internal sealed class GenericDataContainerFormatter : IMessagePackFormatter<GenericDataContainer>
    {
        public void Serialize(ref MessagePackWriter writer, GenericDataContainer value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<List<GenericDataValue>>().Serialize(ref writer, value.serializedData, options);
        }

        public GenericDataContainer Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            GenericDataContainer value = new GenericDataContainer();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.serializedData = options.Resolver.GetFormatterWithVerify<List<GenericDataValue>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            value.serializedData ??= new List<GenericDataValue>();
            value.data = new List<object>(value.serializedData.Count);
            for (int i = 0; i < value.serializedData.Count; i++)
                value.data.Add(value.serializedData[i].ToObject());
            return value;
        }
    }

    internal sealed class GenericDataValueFormatter : IMessagePackFormatter<GenericDataValue>
    {
        public void Serialize(ref MessagePackWriter writer, GenericDataValue value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(7);
            writer.Write((int)value.type);
            writer.Write(value.intValue);
            writer.Write(value.floatValue);
            writer.Write(value.boolValue);
            writer.Write(value.stringValue);
            writer.Write(value.longValue);
            writer.Write(value.doubleValue);
        }

        public GenericDataValue Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader();
            GenericDataValue value = new GenericDataValue();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.type = (GenericDataValue.ValueType)reader.ReadInt32();
                        break;
                    case 1:
                        value.intValue = reader.ReadInt32();
                        break;
                    case 2:
                        value.floatValue = reader.ReadSingle();
                        break;
                    case 3:
                        value.boolValue = reader.ReadBoolean();
                        break;
                    case 4:
                        value.stringValue = reader.ReadString();
                        break;
                    case 5:
                        value.longValue = reader.ReadInt64();
                        break;
                    case 6:
                        value.doubleValue = reader.ReadDouble();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return value;
        }
    }
}
