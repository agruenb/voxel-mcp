namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// Data definition for an NPC profile loaded from configuration (F-406).
/// Defines identity, personality, skills, visual appearance colour, home offset, and optional starting brain/inventory.
/// </summary>
public sealed class NpcProfile
{
    /// <summary>
    /// Display name of the NPC (e.g. "Bob", "Alice").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Personality description used in prompt generation and UI subtitles (e.g. "grumpy but helpful farmer").
    /// </summary>
    public string Personality { get; set; } = string.Empty;

    /// <summary>
    /// List of skills or proficiencies (e.g. ["farming", "building", "mining"]).
    /// </summary>
    public IReadOnlyList<string> Skills { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Visual appearance colour as a hex code or named color (e.g. "#3D7BEB", "green").
    /// </summary>
    public string AppearanceColor { get; set; } = string.Empty;

    /// <summary>
    /// Spatial offset from the world/player spawn origin to place this NPC and its home anchor.
    /// </summary>
    public Vec3 HomeOffset { get; set; } = Vec3.Zero;

    /// <summary>
    /// Optional identifier or script of the initial Lua brain to execute upon spawning (e.g. "farm_loop", "gather_logs").
    /// </summary>
    public string? InitialBrain { get; set; }

    /// <summary>
    /// Optional starting items and quantities given to this NPC at spawn.
    /// </summary>
    public IReadOnlyDictionary<string, int>? StartingInventory { get; set; }

    /// <summary>
    /// Optional character archetype (e.g. "farmer", "miner", "guard", "woodcutter", "courier", "explorer").
    /// </summary>
    public string? Archetype { get; set; }

    public NpcProfile()
    {
    }

    public NpcProfile(
        string name,
        string personality,
        IReadOnlyList<string>? skills = null,
        string appearanceColor = "",
        Vec3? homeOffset = null,
        string? initialBrain = null,
        IReadOnlyDictionary<string, int>? startingInventory = null,
        string? archetype = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("NPC profile name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        Personality = personality ?? string.Empty;
        Skills = skills ?? Array.Empty<string>();
        AppearanceColor = appearanceColor ?? string.Empty;
        HomeOffset = homeOffset ?? Vec3.Zero;
        InitialBrain = initialBrain;
        StartingInventory = startingInventory;
        Archetype = archetype;
    }
}
