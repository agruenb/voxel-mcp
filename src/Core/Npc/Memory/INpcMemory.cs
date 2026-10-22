namespace VoxelKing.Core.Npc.Memory;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VoxelKing.Core.Common;

/// <summary>
/// Persistent memory store for an individual NPC, providing:
/// 1. Typed event log ring buffer for recent actions and prompt generation.
/// 2. Bounded Key-Value store with strict size and type validation for Lua brain scripts.
/// 3. Known named places lookup (e.g. "home", "player_chest").
/// </summary>
public interface INpcMemory
{
    #region Key-Value Store

    /// <summary>
    /// Current number of stored keys in the KV store (max 64).
    /// </summary>
    int KeyCount { get; }

    /// <summary>
    /// Total estimated memory usage in bytes of all keys and values in the KV store (max 16 KB).
    /// </summary>
    int TotalSizeBytes { get; }

    /// <summary>
    /// Gets all active keys in the KV store.
    /// </summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>
    /// Gets a snapshot view of all key-value entries.
    /// </summary>
    IReadOnlyDictionary<string, object> KeyValues { get; }

    /// <summary>
    /// Reads a value by key, returning <paramref name="defaultValue"/> if not found.
    /// </summary>
    object? Get(string key, object? defaultValue = null);

    /// <summary>
    /// Reads a strongly typed value by key, converting if compatible or returning <paramref name="defaultValue"/>.
    /// </summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Attempts to read a strongly typed value by key.
    /// </summary>
    bool TryGet<T>(string key, [NotNullWhen(true)] out T? value);

    /// <summary>
    /// Stores a value in the KV store. Passing null removes the key.
    /// Value must be a boolean, number, string, Vec3i, or a flat dictionary/list of those.
    /// Limits: max 64 keys, max 16 KB total size.
    /// </summary>
    void Set(string key, object? value);

    /// <summary>
    /// Returns true if the key exists in the KV store.
    /// </summary>
    bool Has(string key);

    /// <summary>
    /// Deletes a key from the KV store.
    /// </summary>
    bool Delete(string key);

    /// <summary>
    /// Clears all keys from the KV store.
    /// </summary>
    void ClearKeys();

    #endregion

    #region Named Places

    /// <summary>
    /// Gets the list of all known place names.
    /// </summary>
    IReadOnlyList<string> Places { get; }

    /// <summary>
    /// Gets a read-only dictionary of all known place names and coordinates.
    /// </summary>
    IReadOnlyDictionary<string, Vec3i> KnownPlaces { get; }

    /// <summary>
    /// Looks up the voxel coordinate of a named place, or null if unknown.
    /// </summary>
    Vec3i? Place(string name);

    /// <summary>
    /// Remembers or updates a named place coordinate.
    /// </summary>
    void RememberPlace(string name, Vec3i position);

    /// <summary>
    /// Forgets a named place. Returns true if it existed and was removed.
    /// </summary>
    bool ForgetPlace(string name);

    #endregion

    #region Event Log (Ring Buffer)

    /// <summary>
    /// Current number of events stored in the ring buffer.
    /// </summary>
    int EventCount { get; }

    /// <summary>
    /// Maximum event capacity of the ring buffer.
    /// </summary>
    int EventCapacity { get; }

    /// <summary>
    /// Adds a typed event to the ring buffer, evicting the oldest event if capacity is reached.
    /// </summary>
    void AddEvent(NpcMemoryEvent evt);

    /// <summary>
    /// Returns all events currently in the ring buffer in chronological order (oldest to newest).
    /// </summary>
    IReadOnlyList<NpcMemoryEvent> GetAllEvents();

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> most recent events in chronological order (newest last).
    /// </summary>
    IReadOnlyList<NpcMemoryEvent> GetRecentEvents(int maxCount = 15);

    /// <summary>
    /// Renders up to <paramref name="maxCount"/> recent events formatted as bullet points for LLM prompts.
    /// </summary>
    string RenderEventsForPrompt(double currentTime = -1, int maxCount = 15);

    /// <summary>
    /// Convenience helper to record a speech heard event.
    /// </summary>
    void RecordSpeechHeard(string speaker, string text, bool isPlayer = false, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record a speech spoke event.
    /// </summary>
    void RecordSpeechSpoke(string text, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record an instruction received event.
    /// </summary>
    void RecordInstruction(string instructor, string instruction, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record a task finished event.
    /// </summary>
    void RecordTaskFinished(string taskSummary, bool success, string? detail = null, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record a Lua brain error event.
    /// </summary>
    void RecordBrainError(string error, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record an attacked event.
    /// </summary>
    void RecordAttacked(string attacker, float damage, double timestamp = 0);

    /// <summary>
    /// Convenience helper to record a custom event.
    /// </summary>
    void RecordCustom(string message, string category = "general", double timestamp = 0);

    #endregion

    #region Serialization

    /// <summary>
    /// Serializes the entire NPC memory (KV store, places, event log) to a JSON string.
    /// </summary>
    string ToJson(bool indented = false);

    #endregion
}
