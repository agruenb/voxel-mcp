namespace VoxelKing.Core.Llm;

using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Entities;

/// <summary>
/// Service that coordinates, throttles, and manages generation requests sent to an LLM provider (F-403).
/// Enforces global concurrency limits, per-minute rate limits, per-NPC cancellation, timeouts, and retries with backoff.
/// </summary>
public interface ILlmRequestQueue
{
    /// <summary>
    /// Enqueues an LLM generation request for a specific NPC entity.
    /// If an in-flight request already exists for this NPC, it is cancelled in favour of the new instruction.
    /// </summary>
    /// <param name="npcId">Entity identifier of the NPC.</param>
    /// <param name="npcName">Display name of the NPC (used for logging and diagnostics).</param>
    /// <param name="request">Generation request containing system instructions and conversation turns.</param>
    /// <param name="cancellationToken">Optional caller cancellation token.</param>
    /// <returns>A task resolving to the parsed <see cref="LlmResponse"/>.</returns>
    Task<LlmResponse> EnqueueAsync(
        EntityId npcId,
        string npcName,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues an LLM generation request for a specific NPC entity using a default display name.
    /// </summary>
    Task<LlmResponse> EnqueueAsync(
        EntityId npcId,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any currently in-flight or queued request for the specified NPC.
    /// </summary>
    /// <param name="npcId">Entity identifier of the NPC to cancel.</param>
    /// <returns>True if an active request was found and cancelled; otherwise false.</returns>
    bool Cancel(EntityId npcId);

    /// <summary>
    /// Cancels all active and queued requests across all NPCs.
    /// </summary>
    void CancelAll();

    /// <summary>
    /// Gets cumulative LLM request and token usage statistics tracked by the queue.
    /// </summary>
    LlmUsageStats UsageStats { get; }
}
