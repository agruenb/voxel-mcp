namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;

/// <summary>
/// Service coordinating natural language instruction compilation into sandboxed Lua brains (F-404, LLM_INTEGRATION §3 &amp; §4).
/// Orchestrates prompt generation, LLM request execution, structured parsing, multi-stage validation,
/// repair retries, main-thread state application, and speech feedback.
/// </summary>
public interface IBrainCompiler
{
    /// <summary>
    /// Event raised on the main simulation thread when a compilation completes and its result is applied to an NPC.
    /// </summary>
    event Action<NpcState, CompileResult>? ResultApplied;

    /// <summary>
    /// Compiles a player/instructor natural language instruction into an executable Lua brain script for the given NPC.
    /// </summary>
    /// <param name="npc">Target NPC state.</param>
    /// <param name="instruction">Instruction text provided by the player or instructor.</param>
    /// <param name="instructorId">Optional entity ID of the instructor (e.g. player).</param>
    /// <param name="instructorName">Optional display name of the instructor (defaults to "Player").</param>
    /// <param name="conversation">Optional recent dialogue turns for conversational context.</param>
    /// <param name="currentTime">Optional simulation time in seconds.</param>
    /// <param name="autoApply">If true, automatically dispatches and applies the result to the NPC on the main thread.</param>
    /// <param name="cancellationToken">Optional caller cancellation token.</param>
    /// <returns>A task resolving to the completed <see cref="CompileResult"/>.</returns>
    Task<CompileResult> CompileAsync(
        NpcState npc,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        bool autoApply = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles a natural language instruction for an NPC identified by its <see cref="EntityId"/>.
    /// </summary>
    Task<CompileResult> CompileAsync(
        EntityId npcId,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        bool autoApply = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a compile result to an NPC's state, speaking the reply, swapping the brain script,
    /// updating task summaries, and recording memory events per LLM_INTEGRATION §3.
    /// Must be executed on the main simulation thread.
    /// </summary>
    void ApplyResult(
        NpcState npc,
        CompileResult result,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        double? currentTime = null);

    /// <summary>
    /// Cancels any active compilation in progress for the specified NPC.
    /// </summary>
    /// <param name="npcId">Entity identifier of the NPC.</param>
    /// <returns>True if an active compilation was found and cancelled; otherwise false.</returns>
    bool Cancel(EntityId npcId);

    /// <summary>
    /// Cancels all active compilations across all NPCs.
    /// </summary>
    void CancelAll();
}
