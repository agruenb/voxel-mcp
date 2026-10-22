namespace VoxelKing.Core.Npc.Memory;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using VoxelKing.Core.Common;

/// <summary>
/// Authoritative NPC memory implementation providing:
/// - A typed FIFO event log ring buffer for prompt generation.
/// - A bounded KV store with strict type and size validation (max 64 keys, max 16 KB total).
/// - A named places registry (max 64 places).
/// </summary>
public sealed class NpcMemory : INpcMemory
{
    public const int DefaultMaxKeys = 64;
    public const int DefaultMaxTotalSizeBytes = 16 * 1024; // 16 KB
    public const int DefaultMaxKeyLength = 64;
    public const int DefaultMaxStringValueLength = 4096;
    public const int DefaultMaxPlaces = 64;
    public const int DefaultMaxPlaceNameLength = 64;
    public const int DefaultEventCapacity = 50;

    // Backward-compatible static aliases
    public const int MaxKeys = DefaultMaxKeys;
    public const int MaxTotalSizeBytes = DefaultMaxTotalSizeBytes;
    public const int MaxKeyLength = DefaultMaxKeyLength;
    public const int MaxStringValueLength = DefaultMaxStringValueLength;
    public const int MaxPlaces = DefaultMaxPlaces;
    public const int MaxPlaceNameLength = DefaultMaxPlaceNameLength;

    public int KeyLimit { get; }
    public int TotalSizeLimitBytes { get; }
    public int KeyLengthLimit { get; }
    public int StringValueLengthLimit { get; }
    public int PlacesLimit { get; }
    public int PlaceNameLengthLimit { get; }

    private readonly Dictionary<string, object> _kvStore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _entrySizes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vec3i> _places = new(StringComparer.Ordinal);
    private readonly RingBuffer<NpcMemoryEvent> _events;

    private int _totalSizeBytes;

    public int KeyCount => _kvStore.Count;
    public int TotalSizeBytes => _totalSizeBytes;
    public IReadOnlyCollection<string> Keys => _kvStore.Keys;
    public IReadOnlyDictionary<string, object> KeyValues => _kvStore;

    public IReadOnlyList<string> Places => _places.Keys.ToList();
    public IReadOnlyDictionary<string, Vec3i> KnownPlaces => _places;

    public int EventCount => _events.Count;
    public int EventCapacity => _events.Capacity;

    public NpcMemory(BrainLimitsConfig? config = null, int? eventCapacity = null)
    {
        var cfg = config ?? new BrainLimitsConfig();
        KeyLimit = cfg.MaxMemoryKeys;
        TotalSizeLimitBytes = cfg.MaxMemoryTotalBytes;
        KeyLengthLimit = cfg.MaxMemoryKeyLength;
        StringValueLengthLimit = cfg.MaxMemoryStringValueLength;
        PlacesLimit = cfg.MaxMemoryPlaces;
        PlaceNameLengthLimit = cfg.MaxMemoryPlaceNameLength;

        int capacity = eventCapacity ?? cfg.MemoryEventCapacity;
        _events = new RingBuffer<NpcMemoryEvent>(capacity > 0 ? capacity : DefaultEventCapacity);
    }

    public NpcMemory(int eventCapacity)
        : this(null, eventCapacity)
    {
    }

    #region Key-Value Store

    public object? Get(string key, object? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return defaultValue;
        return _kvStore.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public T Get<T>(string key, T defaultValue)
    {
        return TryGet<T>(key, out var val) && val is not null ? val : defaultValue;
    }

    public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (string.IsNullOrWhiteSpace(key) || !_kvStore.TryGetValue(key, out var raw) || raw is null)
        {
            value = default;
            return false;
        }

        if (raw is T exact)
        {
            value = exact;
            return true;
        }

        try
        {
            if (typeof(T) == typeof(int))
            {
                value = (T)(object)Convert.ToInt32(raw);
                return true;
            }
            if (typeof(T) == typeof(long))
            {
                value = (T)(object)Convert.ToInt64(raw);
                return true;
            }
            if (typeof(T) == typeof(float))
            {
                value = (T)(object)Convert.ToSingle(raw);
                return true;
            }
            if (typeof(T) == typeof(double))
            {
                value = (T)(object)Convert.ToDouble(raw);
                return true;
            }
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)raw.ToString()!;
                return true;
            }
            if (typeof(T) == typeof(bool) && raw is bool b)
            {
                value = (T)(object)b;
                return true;
            }
            if (typeof(T) == typeof(Vec3i) && raw is Vec3i v)
            {
                value = (T)(object)v;
                return true;
            }
        }
        catch
        {
            // Incompatible type conversion
        }

        value = default;
        return false;
    }

    public void Set(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        if (key.Length > KeyLengthLimit)
        {
            throw new ArgumentException($"Key length exceeds maximum of {KeyLengthLimit} characters.", nameof(key));
        }

        if (value is null)
        {
            Delete(key);
            return;
        }

        ValidateAndCalculateSize(value, out int valueSize);
        int keySize = Encoding.UTF8.GetByteCount(key);
        int newEntrySize = keySize + valueSize;

        bool isExisting = _kvStore.ContainsKey(key);
        int oldEntrySize = isExisting ? _entrySizes[key] : 0;

        if (!isExisting && _kvStore.Count >= KeyLimit)
        {
            throw new InvalidOperationException($"Memory key limit exceeded (maximum {KeyLimit} keys allowed).");
        }

        int newTotalSize = _totalSizeBytes - oldEntrySize + newEntrySize;
        if (newTotalSize > TotalSizeLimitBytes)
        {
            throw new InvalidOperationException($"Memory size limit exceeded (maximum {TotalSizeLimitBytes} bytes allowed).");
        }

        _kvStore[key] = value;
        _entrySizes[key] = newEntrySize;
        _totalSizeBytes = newTotalSize;
    }

    public bool Has(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return _kvStore.ContainsKey(key);
    }

    public bool Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (_kvStore.Remove(key))
        {
            if (_entrySizes.Remove(key, out int oldSize))
            {
                _totalSizeBytes -= oldSize;
            }
            return true;
        }
        return false;
    }

    public void ClearKeys()
    {
        _kvStore.Clear();
        _entrySizes.Clear();
        _totalSizeBytes = 0;
    }

    private void ValidateAndCalculateSize(object value, out int sizeBytes)
    {
        switch (value)
        {
            case bool:
                sizeBytes = 1;
                return;

            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                sizeBytes = 8;
                return;

            case string s:
                if (s.Length > StringValueLengthLimit)
                {
                    throw new ArgumentException($"String value length ({s.Length}) exceeds maximum of {StringValueLengthLimit} characters.");
                }
                sizeBytes = Encoding.UTF8.GetByteCount(s);
                return;

            case Vec3i:
                sizeBytes = 12; // 3 x 4-byte ints
                return;

            case IDictionary<string, object> dict:
                int dictSize = 4; // Overhead
                foreach (var (k, v) in dict)
                {
                    if (string.IsNullOrEmpty(k) || k.Length > KeyLengthLimit)
                    {
                        throw new ArgumentException($"Flat table keys must be non-empty strings up to {KeyLengthLimit} characters.");
                    }
                    ValidateFlatElement(v, out int elemSize);
                    dictSize += 4 + Encoding.UTF8.GetByteCount(k) + elemSize;
                }
                sizeBytes = dictSize;
                return;

            case IList<object> list:
                int listSize = 4; // Overhead
                foreach (var item in list)
                {
                    ValidateFlatElement(item, out int elemSize);
                    listSize += 4 + elemSize;
                }
                sizeBytes = listSize;
                return;

            default:
                throw new ArgumentException(
                    $"Unsupported memory value type '{value.GetType().Name}'. " +
                    "Value must be a boolean, number, string, Vec3i, or flat table/list of those.");
        }
    }

    private void ValidateFlatElement(object? element, out int sizeBytes)
    {
        if (element is null)
        {
            sizeBytes = 0;
            return;
        }

        switch (element)
        {
            case bool:
                sizeBytes = 1;
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                sizeBytes = 8;
                return;
            case string s:
                if (s.Length > StringValueLengthLimit)
                {
                    throw new ArgumentException($"String value length ({s.Length}) exceeds maximum of {StringValueLengthLimit} characters.");
                }
                sizeBytes = Encoding.UTF8.GetByteCount(s);
                return;
            case Vec3i:
                sizeBytes = 12;
                return;
            default:
                throw new ArgumentException(
                    $"Flat tables cannot contain nested collections or unsupported type '{element.GetType().Name}'.");
        }
    }

    #endregion

    #region Named Places

    public Vec3i? Place(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _places.TryGetValue(name, out var pos) ? pos : null;
    }

    public void RememberPlace(string name, Vec3i position)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Place name cannot be null or whitespace.", nameof(name));
        }

        if (name.Length > PlaceNameLengthLimit)
        {
            throw new ArgumentException($"Place name length exceeds maximum of {PlaceNameLengthLimit} characters.", nameof(name));
        }

        if (!_places.ContainsKey(name) && _places.Count >= PlacesLimit)
        {
            throw new InvalidOperationException($"Known places limit exceeded (maximum {PlacesLimit} places allowed).");
        }

        _places[name] = position;
    }

    public bool ForgetPlace(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _places.Remove(name);
    }

    #endregion

    #region Event Log (Ring Buffer)

    public void AddEvent(NpcMemoryEvent evt)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        _events.Add(evt);
    }

    public IReadOnlyList<NpcMemoryEvent> GetAllEvents()
    {
        return _events.ToList();
    }

    public IReadOnlyList<NpcMemoryEvent> GetRecentEvents(int maxCount = 15)
    {
        if (maxCount <= 0 || _events.Count == 0)
        {
            return Array.Empty<NpcMemoryEvent>();
        }

        int count = Math.Min(maxCount, _events.Count);
        int startIndex = _events.Count - count;

        var result = new List<NpcMemoryEvent>(count);
        for (int i = startIndex; i < _events.Count; i++)
        {
            result.Add(_events[i]);
        }

        return result;
    }

    public string RenderEventsForPrompt(double currentTime = -1, int maxCount = 15)
    {
        var recent = GetRecentEvents(maxCount);
        if (recent.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", recent.Select(e => e.Render(currentTime)));
    }

    public void RecordSpeechHeard(string speaker, string text, bool isPlayer = false, double timestamp = 0)
    {
        AddEvent(new SpeechHeardEvent(speaker, text, isPlayer, timestamp));
    }

    public void RecordSpeechSpoke(string text, double timestamp = 0)
    {
        AddEvent(new SpeechSpokeEvent(text, timestamp));
    }

    public void RecordInstruction(string instructor, string instruction, double timestamp = 0)
    {
        AddEvent(new InstructionReceivedEvent(instructor, instruction, timestamp));
    }

    public void RecordTaskFinished(string taskSummary, bool success, string? detail = null, double timestamp = 0)
    {
        AddEvent(new TaskFinishedEvent(taskSummary, success, detail, timestamp));
    }

    public void RecordBrainError(string error, double timestamp = 0)
    {
        AddEvent(new BrainErrorEvent(error, timestamp));
    }

    public void RecordAttacked(string attacker, float damage, double timestamp = 0)
    {
        AddEvent(new AttackedEvent(attacker, damage, timestamp));
    }

    public void RecordCustom(string message, string category = "general", double timestamp = 0)
    {
        AddEvent(new CustomMemoryEvent(message, category, timestamp));
    }

    #endregion

    #region Serialization

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string ToJson(bool indented = false)
    {
        var dto = new NpcMemoryDto
        {
            EventCapacity = _events.Capacity,
            Places = new Dictionary<string, Vec3i>(_places, StringComparer.Ordinal),
            Events = _events.Select(NpcMemoryEventDto.FromEvent).ToList()
        };

        foreach (var (k, v) in _kvStore)
        {
            dto.KvStore[k] = JsonSerializer.SerializeToElement(v, JsonOptions);
        }

        var options = indented
            ? new JsonSerializerOptions(JsonOptions) { WriteIndented = true }
            : JsonOptions;

        return JsonSerializer.Serialize(dto, options);
    }

    public static NpcMemory FromJson(string json, BrainLimitsConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be null or whitespace.", nameof(json));
        }

        var dto = JsonSerializer.Deserialize<NpcMemoryDto>(json, JsonOptions);
        if (dto is null)
        {
            throw new InvalidOperationException("Failed to deserialize NPC memory JSON.");
        }

        var memory = new NpcMemory(config, dto.EventCapacity);

        // Restore places
        foreach (var (name, pos) in dto.Places)
        {
            memory.RememberPlace(name, pos);
        }

        // Restore events
        foreach (var evtDto in dto.Events)
        {
            memory.AddEvent(evtDto.ToEvent());
        }

        // Restore KV Store
        foreach (var (key, elem) in dto.KvStore)
        {
            object? val = ConvertJsonElement(elem);
            if (val != null)
            {
                memory.Set(key, val);
            }
        }

        return memory;
    }

    private static object? ConvertJsonElement(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => elem.GetString(),
            JsonValueKind.Number => elem.TryGetInt64(out long l) && elem.GetDouble() == (double)l
                ? (object)l
                : elem.GetDouble(),
            JsonValueKind.Object => ConvertJsonObject(elem),
            JsonValueKind.Array => ConvertJsonArray(elem),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static object ConvertJsonObject(JsonElement elem)
    {
        // Check if object is a Vec3i {"x": ..., "y": ..., "z": ...}
        bool hasX = elem.TryGetProperty("x", out var propX) || elem.TryGetProperty("X", out propX);
        bool hasY = elem.TryGetProperty("y", out var propY) || elem.TryGetProperty("Y", out propY);
        bool hasZ = elem.TryGetProperty("z", out var propZ) || elem.TryGetProperty("Z", out propZ);

        int propCount = 0;
        foreach (var _ in elem.EnumerateObject()) propCount++;

        if (hasX && hasY && hasZ && propCount == 3 &&
            propX.TryGetInt32(out int x) &&
            propY.TryGetInt32(out int y) &&
            propZ.TryGetInt32(out int z))
        {
            return new Vec3i(x, y, z);
        }

        // Otherwise flat dictionary
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var prop in elem.EnumerateObject())
        {
            var parsed = ConvertJsonElement(prop.Value);
            if (parsed != null)
            {
                dict[prop.Name] = parsed;
            }
        }
        return dict;
    }

    private static object ConvertJsonArray(JsonElement elem)
    {
        var list = new List<object>();
        foreach (var item in elem.EnumerateArray())
        {
            var parsed = ConvertJsonElement(item);
            if (parsed != null)
            {
                list.Add(parsed);
            }
        }
        return list;
    }

    #endregion
}
