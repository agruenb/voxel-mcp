namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VoxelKing.Core.Common;

/// <summary>
/// Provides loading and parsing of NPC profiles from JSON configurations (F-406).
/// </summary>
public static class NpcProfileLoader
{
    /// <summary>
    /// Parses a JSON string containing an array of NPC profiles or an object containing an "npcs" array.
    /// </summary>
    /// <param name="json">Raw JSON configuration text.</param>
    /// <returns>A read-only list of parsed <see cref="NpcProfile"/>s.</returns>
    public static IReadOnlyList<NpcProfile> Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be empty or whitespace.", nameof(json));
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var profiles = new List<NpcProfile>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in root.EnumerateArray())
            {
                profiles.Add(ParseProfileElement(elem));
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("npcs", out var npcsProp) && npcsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in npcsProp.EnumerateArray())
                {
                    profiles.Add(ParseProfileElement(elem));
                }
            }
            else if (root.TryGetProperty("profiles", out var profilesProp) && profilesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in profilesProp.EnumerateArray())
                {
                    profiles.Add(ParseProfileElement(elem));
                }
            }
            else if (root.TryGetProperty("name", out _))
            {
                // Single profile defined as root object
                profiles.Add(ParseProfileElement(root));
            }
            else
            {
                throw new ArgumentException("JSON object does not contain an 'npcs' or 'profiles' array or valid NPC profile.");
            }
        }
        else
        {
            throw new ArgumentException("Root JSON element must be an array or object.");
        }

        return profiles;
    }

    /// <summary>
    /// Loads NPC profiles from a file path on disk.
    /// </summary>
    public static IReadOnlyList<NpcProfile> LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"NPC profiles configuration file not found at '{filePath}'.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return Load(json);
    }

    /// <summary>
    /// Attempts to locate the <c>config/npcs.json</c> file by searching upwards from candidate directories.
    /// </summary>
    public static string? FindConfigFile(string? startDirectory = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(startDirectory))
        {
            candidates.Add(startDirectory);
        }
        candidates.Add(Directory.GetCurrentDirectory());
        candidates.Add(AppContext.BaseDirectory);

        foreach (var start in candidates)
        {
            string? current = Path.GetFullPath(start);
            for (int depth = 0; depth < 8 && current != null; depth++)
            {
                string path1 = Path.Combine(current, "config", "npcs.json");
                if (File.Exists(path1))
                {
                    return path1;
                }

                string path2 = Path.Combine(current, "npcs.json");
                if (File.Exists(path2))
                {
                    return path2;
                }

                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns default hardcoded NPC profiles (Bob, Alice, Charlie, Diana, Edward, Fiona) as a fallback.
    /// </summary>
    public static IReadOnlyList<NpcProfile> LoadDefaultProfiles()
    {
        return new List<NpcProfile>
        {
            new NpcProfile(
                "Bob",
                "grumpy but helpful farmer and builder",
                new[] { "farming", "building", "mining" },
                "#3D7BEB",
                new Vec3(3.0f, 0.0f, 2.0f),
                "farm_loop"),

            new NpcProfile(
                "Alice",
                "cheerful and energetic woodcutter and gatherer",
                new[] { "woodcutting", "foraging", "crafting" },
                "#2ECC71",
                new Vec3(-3.5f, 0.0f, 3.0f),
                "gather_logs"),

            new NpcProfile(
                "Charlie",
                "diligent architect and stonemason constructing fortifications",
                new[] { "masonry", "building", "architecture" },
                "#E74C3C",
                new Vec3(4.5f, 0.0f, -2.5f),
                "build_wall"),

            new NpcProfile(
                "Diana",
                "cautious and vigilant village guard keeping watch over the area",
                new[] { "guarding", "patrol", "scouting" },
                "#9B59B6",
                new Vec3(-4.0f, 0.0f, -3.5f),
                "guard_area"),

            new NpcProfile(
                "Edward",
                "friendly and dependable courier delivering supplies to storage",
                new[] { "hauling", "organizing", "delivery" },
                "#F39C12",
                new Vec3(1.5f, 0.0f, -4.5f),
                "deliver_items"),

            new NpcProfile(
                "Fiona",
                "inquisitive explorer and loyal companion following friends",
                new[] { "following", "pathfinding", "exploration" },
                "#1ABC9C",
                new Vec3(-2.0f, 0.0f, -2.0f),
                "follow_player")
        };
    }

    private static NpcProfile ParseProfileElement(JsonElement elem)
    {
        string name = string.Empty;
        if (elem.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            name = nameProp.GetString()?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("NPC profile must contain a non-empty 'name' property.");
        }

        string personality = string.Empty;
        if (elem.TryGetProperty("personality", out var pProp) && pProp.ValueKind == JsonValueKind.String)
        {
            personality = pProp.GetString()?.Trim() ?? string.Empty;
        }
        else if (elem.TryGetProperty("profile", out var profProp) && profProp.ValueKind == JsonValueKind.String)
        {
            personality = profProp.GetString()?.Trim() ?? string.Empty;
        }

        var skills = new List<string>();
        if (elem.TryGetProperty("skills", out var skillsProp) && skillsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var skillElem in skillsProp.EnumerateArray())
            {
                if (skillElem.ValueKind == JsonValueKind.String)
                {
                    string? skill = skillElem.GetString();
                    if (!string.IsNullOrWhiteSpace(skill))
                    {
                        skills.Add(skill.Trim());
                    }
                }
            }
        }

        string appearanceColor = string.Empty;
        if (elem.TryGetProperty("appearanceColor", out var colProp) && colProp.ValueKind == JsonValueKind.String)
        {
            appearanceColor = colProp.GetString()?.Trim() ?? string.Empty;
        }
        else if (elem.TryGetProperty("appearanceColour", out var colProp2) && colProp2.ValueKind == JsonValueKind.String)
        {
            appearanceColor = colProp2.GetString()?.Trim() ?? string.Empty;
        }
        else if (elem.TryGetProperty("color", out var colProp3) && colProp3.ValueKind == JsonValueKind.String)
        {
            appearanceColor = colProp3.GetString()?.Trim() ?? string.Empty;
        }
        else if (elem.TryGetProperty("appearance", out var colProp4) && colProp4.ValueKind == JsonValueKind.String)
        {
            appearanceColor = colProp4.GetString()?.Trim() ?? string.Empty;
        }

        Vec3 homeOffset = Vec3.Zero;
        if (elem.TryGetProperty("homeOffset", out var offsetProp) ||
            elem.TryGetProperty("home_offset", out offsetProp) ||
            elem.TryGetProperty("offset", out offsetProp))
        {
            homeOffset = ParseVec3(offsetProp);
        }

        string? initialBrain = null;
        if (elem.TryGetProperty("initialBrain", out var brainProp) && brainProp.ValueKind == JsonValueKind.String)
        {
            initialBrain = brainProp.GetString()?.Trim();
        }
        else if (elem.TryGetProperty("brain", out var brainProp2) && brainProp2.ValueKind == JsonValueKind.String)
        {
            initialBrain = brainProp2.GetString()?.Trim();
        }

        Dictionary<string, int>? startingInventory = null;
        if ((elem.TryGetProperty("startingInventory", out var invProp) || elem.TryGetProperty("inventory", out invProp))
            && invProp.ValueKind == JsonValueKind.Object)
        {
            startingInventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var itemProp in invProp.EnumerateObject())
            {
                if (itemProp.Value.ValueKind == JsonValueKind.Number && itemProp.Value.TryGetInt32(out int count))
                {
                    startingInventory[itemProp.Name] = count;
                }
            }
        }

        string? archetype = null;
        if (elem.TryGetProperty("archetype", out var archProp) && archProp.ValueKind == JsonValueKind.String)
        {
            archetype = archProp.GetString()?.Trim();
        }

        return new NpcProfile(
            name,
            personality,
            skills,
            appearanceColor,
            homeOffset,
            initialBrain,
            startingInventory,
            archetype);
    }

    private static Vec3 ParseVec3(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Object)
        {
            float x = elem.TryGetProperty("x", out var xProp) && xProp.TryGetSingle(out float valX) ? valX : 0f;
            float y = elem.TryGetProperty("y", out var yProp) && yProp.TryGetSingle(out float valY) ? valY : 0f;
            float z = elem.TryGetProperty("z", out var zProp) && zProp.TryGetSingle(out float valZ) ? valZ : 0f;
            return new Vec3(x, y, z);
        }

        if (elem.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            float x = 0f, y = 0f, z = 0f;
            foreach (var item in elem.EnumerateArray())
            {
                if (item.TryGetSingle(out float val))
                {
                    if (index == 0) x = val;
                    else if (index == 1) y = val;
                    else if (index == 2) z = val;
                }
                index++;
            }
            return new Vec3(x, y, z);
        }

        return Vec3.Zero;
    }
}
