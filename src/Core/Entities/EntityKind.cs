namespace VoxelKing.Core.Entities;

using System;

/// <summary>
/// Categorization of entities in the simulation matching the Lua brain API ("player", "npc", "item", "animal").
/// </summary>
public enum EntityKind : byte
{
    Player = 0,
    Npc = 1,
    Item = 2,
    Animal = 3
}

/// <summary>
/// Extension methods for converting entity kinds to and from their API strings.
/// </summary>
public static class EntityKindExtensions
{
    public static string ToApiString(this EntityKind kind) => kind switch
    {
        EntityKind.Player => "player",
        EntityKind.Npc => "npc",
        EntityKind.Item => "item",
        EntityKind.Animal => "animal",
        _ => kind.ToString().ToLowerInvariant()
    };

    public static bool TryParse(string? value, out EntityKind kind)
    {
        if (string.Equals(value, "player", StringComparison.OrdinalIgnoreCase))
        {
            kind = EntityKind.Player;
            return true;
        }
        if (string.Equals(value, "npc", StringComparison.OrdinalIgnoreCase))
        {
            kind = EntityKind.Npc;
            return true;
        }
        if (string.Equals(value, "item", StringComparison.OrdinalIgnoreCase))
        {
            kind = EntityKind.Item;
            return true;
        }
        if (string.Equals(value, "animal", StringComparison.OrdinalIgnoreCase))
        {
            kind = EntityKind.Animal;
            return true;
        }

        kind = default;
        return false;
    }
}
