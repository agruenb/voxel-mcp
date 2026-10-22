namespace VoxelKing.Core.Brain;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Interface for the NPC Lua brain host executing sandboxed scripts and event handlers (ARCHITECTURE §5.4).
/// Hooks into the behaviour arbiter as the Priority 2 Task layer.
/// </summary>
public interface IBrainHost : ITaskLayer, IDisposable
{
    /// <summary>
    /// Current execution status of the brain host.
    /// </summary>
    BrainStatus Status { get; }

    /// <summary>
    /// The raw Lua script source currently assigned, or null if uninitialized.
    /// </summary>
    string? CurrentScript { get; }

    /// <summary>
    /// Last error message encountered during compilation or execution, if faulted.
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// The underlying Lua execution context provided to API bindings.
    /// </summary>
    INpcBrainContext Context { get; }

    /// <summary>
    /// Recent log messages emitted by this brain via <c>log(...)</c>, <c>npc.say</c>, or lifecycle errors.
    /// </summary>
    System.Collections.Generic.IReadOnlyList<string> RecentLogs { get; }

    /// <summary>
    /// Appends a log message to the brain's recent log history.
    /// </summary>
    void AddLog(string message);

    /// <summary>
    /// Loads and initializes a new Lua brain script for the NPC, replacing any existing script.
    /// Validates script size and runs top-level code within the top-level instruction budget.
    /// </summary>
    /// <param name="script">The Lua 5.2 script source.</param>
    /// <param name="instructorId">Optional entity ID that assigned this task.</param>
    /// <returns>True if script loaded and validated successfully; false otherwise.</returns>
    bool LoadScript(string script, EntityId? instructorId = null);

    /// <summary>
    /// Swaps the current brain script for a newly validated Lua script (LLM_INTEGRATION §3).
    /// </summary>
    /// <param name="script">The Lua 5.2 script source.</param>
    /// <param name="instructorId">Optional entity ID that assigned this task.</param>
    /// <returns>True if script loaded and validated successfully; false otherwise.</returns>
    bool Swap(string script, EntityId? instructorId = null);

    /// <summary>
    /// Restarts the main coroutine from the beginning with the current script.
    /// State and progress must be recovered from memory.
    /// </summary>
    void Restart();

    /// <summary>
    /// Queues an event to be delivered to Lua event handlers (e.g. on_heard, on_attacked).
    /// </summary>
    /// <param name="handlerName">Name of the handler function (e.g. "on_heard").</param>
    /// <param name="args">Arguments passed to the Lua handler.</param>
    void QueueEvent(string handlerName, params object?[] args);

    /// <summary>
    /// Delivers an on_heard event to the Lua handler.
    /// </summary>
    void OnHeard(EntityId speakerId, string text);

    /// <summary>
    /// Delivers an on_attacked event to the Lua handler.
    /// </summary>
    void OnAttacked(EntityId attackerId, float damage);

    /// <summary>
    /// Delivers an on_player_nearby event to the Lua handler.
    /// </summary>
    void OnPlayerNearby(EntityId playerId);

    /// <summary>
    /// Delivers an on_interrupted event to the Lua handler.
    /// </summary>
    void OnInterrupted(string reason);
}
