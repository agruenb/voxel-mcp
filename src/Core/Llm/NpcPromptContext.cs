namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;

/// <summary>
/// Data transfer object holding all dynamic context needed to render user content for an LLM prompt (LLM_INTEGRATION §2.2).
/// </summary>
public sealed record NpcPromptContext
{
    /// <summary>
    /// Gets the display name of the NPC.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the world position of the NPC.
    /// </summary>
    public Vec3 Position { get; init; }

    /// <summary>
    /// Gets the item currently held by the player, or null if none/empty.
    /// </summary>
    public string? PlayerHeldItem { get; init; }

    /// <summary>
    /// Gets the personality description of the NPC (e.g. "grumpy but helpful farmer").
    /// </summary>
    public string Personality { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of skills or proficiencies of the NPC (e.g. ["farming", "mining"]).
    /// </summary>
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the short summary of the currently active task, or null if idle.
    /// </summary>
    public string? CurrentTask { get; init; }

    /// <summary>
    /// Gets the duration in seconds that the current task has been running.
    /// </summary>
    public double TaskRunningSeconds { get; init; }

    /// <summary>
    /// Gets the list of known named place identifiers remembered by the NPC (e.g. ["home", "player_chest"]).
    /// </summary>
    public IReadOnlyList<string> KnownPlaces { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the inventory counts for all non-empty items held by the NPC.
    /// </summary>
    public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the chronological list of recent memory events (newest last).
    /// </summary>
    public IReadOnlyList<NpcMemoryEvent> RecentEvents { get; init; } = Array.Empty<NpcMemoryEvent>();

    /// <summary>
    /// Gets the description of the instructor who gave the current instruction (e.g. "Player \"Adrian\""), or null.
    /// </summary>
    public string? Instructor { get; init; }

    /// <summary>
    /// Gets recent conversation turns between the instructor and this NPC (newest last).
    /// </summary>
    public IReadOnlyList<ConversationTurn> Conversation { get; init; } = Array.Empty<ConversationTurn>();

    /// <summary>
    /// Gets the instruction string given by the player/instructor.
    /// </summary>
    public string Instruction { get; init; } = string.Empty;

    /// <summary>
    /// Gets the in-game simulation timestamp in seconds, used for computing relative times in memory events.
    /// </summary>
    public double CurrentTime { get; init; } = -1;

    /// <summary>
    /// Populates an <see cref="NpcPromptContext"/> snapshot from an active <see cref="NpcState"/> instance.
    /// </summary>
    public static NpcPromptContext FromNpc(
        NpcState npc,
        string instruction,
        string? instructor = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        string? playerHeldItem = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(instruction);

        double taskRunningSeconds = 0;
        if (!string.IsNullOrEmpty(npc.CurrentTaskSummary) && npc.TaskStartTime > 0 && currentTime >= npc.TaskStartTime)
        {
            taskRunningSeconds = currentTime - npc.TaskStartTime;
        }

        var places = new List<string>();
        if (npc.Memory?.Places != null)
        {
            places.AddRange(npc.Memory.Places);
        }
        if (npc.HasExplicitHome && !places.Contains("home", StringComparer.OrdinalIgnoreCase))
        {
            places.Insert(0, "home");
        }

        var inventoryCounts = npc.Inventory?.GetAllCounts() ?? new Dictionary<string, int>();

        var recentEvents = npc.Memory?.GetRecentEvents(15) ?? Array.Empty<NpcMemoryEvent>();

        string personality = !string.IsNullOrEmpty(npc.Personality)
            ? npc.Personality
            : npc.Profile;

        return new NpcPromptContext
        {
            Name = npc.Name,
            Position = npc.Position,
            PlayerHeldItem = playerHeldItem,
            Personality = personality,
            Skills = npc.Skills ?? Array.Empty<string>(),
            CurrentTask = npc.CurrentTaskSummary,
            TaskRunningSeconds = taskRunningSeconds,
            KnownPlaces = places,
            Inventory = inventoryCounts,
            RecentEvents = recentEvents,
            Instructor = instructor,
            Conversation = conversation ?? Array.Empty<ConversationTurn>(),
            Instruction = instruction,
            CurrentTime = currentTime
        };
    }
}
