namespace VoxelKing.Core.Brain;

using MoonSharp.Interpreter;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Perception;

/// <summary>
/// Bridge interface providing live simulation state and action dispatch capabilities
/// to the Lua brain API bindings during script execution.
/// </summary>
public interface INpcBrainContext
{
    /// <summary>
    /// The authoritative NPC state associated with this brain runtime.
    /// </summary>
    NpcState Npc { get; }

    /// <summary>
    /// Persistent memory storage for the NPC.
    /// </summary>
    INpcMemory Memory { get; }

    /// <summary>
    /// Scoped spatial perception service for the NPC, or null if perception is unavailable.
    /// </summary>
    INpcPerception? Perception { get; }

    /// <summary>
    /// Whether the brain runtime is currently executing an event handler (e.g. on_heard).
    /// Calling blocking actions while true must throw a runtime error.
    /// </summary>
    bool IsInsideHandler { get; set; }

    /// <summary>
    /// Whether the brain has completed its task (npc.done).
    /// </summary>
    bool IsDone { get; }

    /// <summary>
    /// Completion summary message provided when npc.done was called, if any.
    /// </summary>
    string? DoneSummary { get; }

    /// <summary>
    /// Whether npc.restart was called, indicating main() should restart from the top.
    /// </summary>
    bool RestartRequested { get; }

    /// <summary>
    /// The most recent blocking action request initiated by the brain.
    /// </summary>
    ActionRequest? LastActionRequest { get; }

    /// <summary>
    /// Current world/simulation timestamp in seconds.
    /// </summary>
    float CurrentTime { get; set; }

    /// <summary>
    /// Logs a debug line from the brain script.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Speaks a message, subject to rate limits and length restrictions.
    /// </summary>
    void Say(string text);

    /// <summary>
    /// Turns the NPC's facing direction towards a target block position.
    /// </summary>
    void LookAt(Vec3i pos);

    /// <summary>
    /// Marks the active task as completed.
    /// </summary>
    void Done(string? summary = null);

    /// <summary>
    /// Requests that the brain restart execution from the top of main().
    /// </summary>
    void RequestRestart();

    /// <summary>
    /// Cancels/interrupts the currently executing blocking action.
    /// </summary>
    void Interrupt();

    /// <summary>
    /// Initiates a blocking action request, converting arguments and recording the request.
    /// </summary>
    ActionRequest BeginAction(string actionName, CallbackArguments args);
}
