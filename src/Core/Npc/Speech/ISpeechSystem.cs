namespace VoxelKing.Core.Npc.Speech;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Authoritative speech coordination service (ARCHITECTURE §5.6).
/// Broadcasts EntitySpoke events, enforces character limits and rate limiting,
/// records speech history in NPC memory, and routes on_heard events to nearby listening brains.
/// </summary>
public interface ISpeechSystem : IDisposable
{
    /// <summary>
    /// Default hearing radius in blocks (16.0f).
    /// </summary>
    float DefaultHearingRadius { get; }

    /// <summary>
    /// Default minimum interval in seconds between consecutive speeches by the same entity (2.0s).
    /// </summary>
    float RateLimitSeconds { get; }

    /// <summary>
    /// Maximum character length of a spoken message (200 chars).
    /// </summary>
    int MaxTextLength { get; }

    /// <summary>
    /// Current simulation time in seconds.
    /// </summary>
    double CurrentTime { get; set; }

    /// <summary>
    /// Speaks text aloud on behalf of an entity.
    /// Enforces character limits and per-entity rate limits.
    /// Publishes EntitySpoke, records SpeechSpokeEvent on speaker memory,
    /// and records SpeechHeardEvent on all NPC listeners within hearing radius.
    /// </summary>
    /// <param name="speakerId">ID of the speaking entity.</param>
    /// <param name="speakerName">Display name of the speaker.</param>
    /// <param name="position">Spatial world position of the speaker.</param>
    /// <param name="text">The message text.</param>
    /// <param name="hearingRadius">Radius in blocks within which entities can hear this speech.</param>
    /// <param name="timestamp">Optional simulation timestamp (defaults to CurrentTime).</param>
    /// <returns>True if spoken successfully; false if rate-limited or invalid.</returns>
    bool Speak(
        EntityId speakerId,
        string speakerName,
        Vec3 position,
        string text,
        float? hearingRadius = null,
        double? timestamp = null);
}
