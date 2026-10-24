namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Thread-safe in-memory registry of block types and attributes.
/// </summary>
public sealed class BlockRegistry : IBlockRegistry
{
    private readonly BlockDefinition[] _definitionsById;
    private readonly Dictionary<string, ushort> _stringToId;
    private readonly List<BlockDefinition> _allDefinitions;

    public ushort AirId => 0;
    public BlockDefinition Air => _definitionsById[0];
    public int Count => _allDefinitions.Count;
    public IReadOnlyList<BlockDefinition> AllDefinitions => _allDefinitions;

    public BlockRegistry(IEnumerable<BlockDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var list = definitions.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Block registry must contain at least one definition (air).", nameof(definitions));
        }

        int maxId = -1;
        _stringToId = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in list)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (string.IsNullOrWhiteSpace(def.StringId))
            {
                throw new ArgumentException($"Block definition with ID {def.Id} has an empty string identifier.");
            }

            if (_stringToId.ContainsKey(def.StringId))
            {
                throw new ArgumentException($"Duplicate string identifier '{def.StringId}' in block registry.");
            }

            _stringToId[def.StringId] = def.Id;
            if (def.Id > maxId)
            {
                maxId = def.Id;
            }
        }

        if (!_stringToId.TryGetValue("air", out var airId) || airId != 0)
        {
            throw new ArgumentException("Block registry must contain 'air' with numeric ID 0.");
        }

        _definitionsById = new BlockDefinition[maxId + 1];
        foreach (var def in list)
        {
            if (_definitionsById[def.Id] != null)
            {
                throw new ArgumentException($"Duplicate numeric identifier '{def.Id}' in block registry.");
            }
            _definitionsById[def.Id] = def;
        }

        _allDefinitions = list.OrderBy(d => d.Id).ToList();
    }

    public BlockDefinition GetDefinition(ushort id)
    {
        if (id < _definitionsById.Length && _definitionsById[id] is { } def)
        {
            return def;
        }
        throw new KeyNotFoundException($"No block definition found for numeric ID {id}.");
    }

    public BlockDefinition GetDefinition(string stringId)
    {
        ArgumentNullException.ThrowIfNull(stringId);
        if (_stringToId.TryGetValue(stringId, out var id))
        {
            return _definitionsById[id];
        }
        throw new KeyNotFoundException($"No block definition found for string ID '{stringId}'.");
    }

    public bool TryGetDefinition(ushort id, [NotNullWhen(true)] out BlockDefinition? definition)
    {
        if (id < _definitionsById.Length && _definitionsById[id] is { } def)
        {
            definition = def;
            return true;
        }
        definition = null;
        return false;
    }

    public bool TryGetDefinition(string stringId, [NotNullWhen(true)] out BlockDefinition? definition)
    {
        if (stringId != null && _stringToId.TryGetValue(stringId, out var id))
        {
            definition = _definitionsById[id];
            return true;
        }
        definition = null;
        return false;
    }

    public ushort GetId(string stringId)
    {
        ArgumentNullException.ThrowIfNull(stringId);
        if (_stringToId.TryGetValue(stringId, out var id))
        {
            return id;
        }
        throw new KeyNotFoundException($"No block ID found for string ID '{stringId}'.");
    }

    public bool TryGetId(string stringId, out ushort id)
    {
        if (stringId != null && _stringToId.TryGetValue(stringId, out id))
        {
            return true;
        }
        id = 0;
        return false;
    }

    public string GetStringId(ushort id)
    {
        return GetDefinition(id).StringId;
    }

    public bool TryGetStringId(ushort id, [NotNullWhen(true)] out string? stringId)
    {
        if (TryGetDefinition(id, out var def))
        {
            stringId = def.StringId;
            return true;
        }
        stringId = null;
        return false;
    }

    public bool Contains(ushort id) => id < _definitionsById.Length && _definitionsById[id] != null;

    public bool Contains(string stringId) => stringId != null && _stringToId.ContainsKey(stringId);

    public bool IsIndexed(ushort id) =>
        id < _definitionsById.Length && _definitionsById[id] is { } def && def.IsIndexed;

    public bool IsIndexed(string stringId) =>
        TryGetDefinition(stringId, out var def) && def.IsIndexed;

    /// <summary>
    /// Loads block definitions from a JSON string, optionally resolving texture names to atlas indices.
    /// </summary>
    public static BlockRegistry Load(string json, IReadOnlyDictionary<string, int>? textureNameToIndex = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        JsonElement blocksArray;

        if (root.ValueKind == JsonValueKind.Array)
        {
            blocksArray = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("blocks", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            blocksArray = prop;
        }
        else
        {
            throw new JsonException("Invalid blocks JSON: expected a root array or an object containing a 'blocks' array.");
        }

        var definitions = new List<BlockDefinition>();
        ushort nextId = 0;

        foreach (var elem in blocksArray.EnumerateArray())
        {
            string id = elem.GetProperty("id").GetString()
                ?? throw new JsonException("Block definition missing 'id'.");

            ushort numericId;
            if (elem.TryGetProperty("numericId", out var numProp))
            {
                numericId = (ushort)numProp.GetUInt32();
            }
            else
            {
                numericId = nextId;
            }

            bool solid = elem.TryGetProperty("solid", out var solidProp) && solidProp.GetBoolean();
            bool transparent = elem.TryGetProperty("transparent", out var transProp) && transProp.GetBoolean();
            float hardness = elem.TryGetProperty("hardness", out var hardProp) ? (float)hardProp.GetDouble() : 1.0f;
            string? drop = elem.TryGetProperty("drop", out var dropProp) && dropProp.ValueKind == JsonValueKind.String
                ? dropProp.GetString()
                : null;
            bool container = elem.TryGetProperty("isContainer", out var contProp) && contProp.GetBoolean();
            bool indexed = elem.TryGetProperty("isIndexed", out var idxProp) && idxProp.GetBoolean();

            BlockAtlasIndices atlas = ParseAtlasIndices(elem, textureNameToIndex);

            definitions.Add(new BlockDefinition(
                numericId,
                id,
                solid,
                transparent,
                hardness,
                drop,
                container,
                indexed,
                atlas
            ));

            if (numericId >= nextId)
            {
                nextId = (ushort)(numericId + 1);
            }
        }

        return new BlockRegistry(definitions);
    }

    private static BlockAtlasIndices ParseAtlasIndices(JsonElement elem, IReadOnlyDictionary<string, int>? textureNameToIndex)
    {
        // 1. Check for named "textures" property
        if (elem.TryGetProperty("textures", out var texElem))
        {
            if (texElem.ValueKind == JsonValueKind.String)
            {
                string texName = texElem.GetString() ?? string.Empty;
                int idx = ResolveTextureIndex(texName, textureNameToIndex);
                return new BlockAtlasIndices(idx);
            }

            if (texElem.ValueKind == JsonValueKind.Object)
            {
                int top = ResolveTextureFromProperty(texElem, "top", textureNameToIndex, 0);
                int bottom = ResolveTextureFromProperty(texElem, "bottom", textureNameToIndex, 0);

                if (texElem.TryGetProperty("sides", out var sProp) && sProp.ValueKind == JsonValueKind.String)
                {
                    int sides = ResolveTextureIndex(sProp.GetString() ?? string.Empty, textureNameToIndex);
                    return new BlockAtlasIndices(top, bottom, sides);
                }

                int north = ResolveTextureFromProperty(texElem, "north", textureNameToIndex, 0);
                int south = ResolveTextureFromProperty(texElem, "south", textureNameToIndex, 0);
                int east = ResolveTextureFromProperty(texElem, "east", textureNameToIndex, 0);
                int west = ResolveTextureFromProperty(texElem, "west", textureNameToIndex, 0);

                return new BlockAtlasIndices(top, bottom, north, south, east, west);
            }
        }

        // 2. Fall back to numeric "atlasIndices"
        if (elem.TryGetProperty("atlasIndices", out var atlasElem))
        {
            if (atlasElem.ValueKind == JsonValueKind.Number)
            {
                int all = atlasElem.GetInt32();
                return new BlockAtlasIndices(all);
            }

            if (atlasElem.ValueKind == JsonValueKind.Array)
            {
                var arr = atlasElem.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                if (arr.Length >= 6)
                {
                    return new BlockAtlasIndices(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5]);
                }
                if (arr.Length == 3)
                {
                    return new BlockAtlasIndices(arr[0], arr[1], arr[2]);
                }
                if (arr.Length == 1)
                {
                    return new BlockAtlasIndices(arr[0]);
                }
            }

            if (atlasElem.ValueKind == JsonValueKind.Object)
            {
                if (atlasElem.TryGetProperty("all", out var allProp))
                {
                    return new BlockAtlasIndices(allProp.GetInt32());
                }

                int top = atlasElem.TryGetProperty("top", out var tProp) ? tProp.GetInt32() : 0;
                int bottom = atlasElem.TryGetProperty("bottom", out var bProp) ? bProp.GetInt32() : 0;
                if (atlasElem.TryGetProperty("sides", out var sProp))
                {
                    int sides = sProp.GetInt32();
                    return new BlockAtlasIndices(top, bottom, sides);
                }

                int north = atlasElem.TryGetProperty("north", out var nProp) ? nProp.GetInt32() : 0;
                int south = atlasElem.TryGetProperty("south", out var soProp) ? soProp.GetInt32() : 0;
                int east = atlasElem.TryGetProperty("east", out var eProp) ? eProp.GetInt32() : 0;
                int west = atlasElem.TryGetProperty("west", out var wProp) ? wProp.GetInt32() : 0;

                return new BlockAtlasIndices(top, bottom, north, south, east, west);
            }
        }

        return new BlockAtlasIndices(0);
    }

    private static int ResolveTextureFromProperty(JsonElement elem, string propName, IReadOnlyDictionary<string, int>? map, int fallback)
    {
        if (elem.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return ResolveTextureIndex(prop.GetString() ?? string.Empty, map);
        }
        return fallback;
    }

    private static int ResolveTextureIndex(string textureName, IReadOnlyDictionary<string, int>? map)
    {
        if (map != null && map.TryGetValue(textureName, out int index))
        {
            return index;
        }

        // Standard fallback index mapping for canonical block textures
        return textureName switch
        {
            "grass_top" => 0,
            "grass_side" => 1,
            "dirt" => 2,
            "stone" => 3,
            "sand" => 4,
            "water" => 5,
            "oak_log_top" => 6,
            "oak_log_side" => 7,
            "oak_leaves" => 8,
            "planks" => 9,
            "cobblestone" => 10,
            "chest_top" => 11,
            "chest_side" => 12,
            "coal_ore" => 13,
            _ => 0
        };
    }

    /// <summary>
    /// Loads block definitions from a file path, optionally resolving texture names to atlas indices.
    /// </summary>
    public static BlockRegistry LoadFromFile(string filePath, IReadOnlyDictionary<string, int>? textureNameToIndex = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blocks configuration file not found at '{filePath}'.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return Load(json, textureNameToIndex);
    }

    /// <summary>
    /// Creates the standard default block registry with initial 12 blocks.
    /// </summary>
    public static BlockRegistry CreateDefault()
    {
        return new BlockRegistry(new[]
        {
            new BlockDefinition(0, "air", false, true, 0.0f, null, false, false, new BlockAtlasIndices(0)),
            new BlockDefinition(1, "grass", true, false, 0.6f, "dirt", false, false, new BlockAtlasIndices(0, 2, 1)),
            new BlockDefinition(2, "dirt", true, false, 0.5f, "dirt", false, false, new BlockAtlasIndices(2)),
            new BlockDefinition(3, "stone", true, false, 1.5f, "cobblestone", false, false, new BlockAtlasIndices(3)),
            new BlockDefinition(4, "sand", true, false, 0.5f, "sand", false, false, new BlockAtlasIndices(4)),
            new BlockDefinition(5, "water", false, true, 0.0f, null, false, false, new BlockAtlasIndices(5)),
            new BlockDefinition(6, "oak_log", true, false, 2.0f, "oak_log", false, true, new BlockAtlasIndices(6, 6, 7)),
            new BlockDefinition(7, "oak_leaves", true, true, 0.2f, null, false, false, new BlockAtlasIndices(8)),
            new BlockDefinition(8, "planks", true, false, 2.0f, "planks", false, false, new BlockAtlasIndices(9)),
            new BlockDefinition(9, "cobblestone", true, false, 2.0f, "cobblestone", false, false, new BlockAtlasIndices(10)),
            new BlockDefinition(10, "chest", true, false, 2.5f, "chest", true, true, new BlockAtlasIndices(11, 11, 12)),
            new BlockDefinition(11, "coal_ore", true, false, 3.0f, "coal_ore", false, true, new BlockAtlasIndices(13)),
        });
    }
}
