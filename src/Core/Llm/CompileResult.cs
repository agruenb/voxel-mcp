namespace VoxelKing.Core.Llm;

using System;
using System.Linq;
using VoxelKing.Core.Brain;

/// <summary>
/// Result of an instruction compilation operation performed by <see cref="IBrainCompiler"/> (F-404).
/// Encapsulates spoken dialogue reply, script swap payload, validation telemetry, and status.
/// </summary>
public sealed record CompileResult
{
    /// <summary>
    /// Fallback line spoken by the NPC when script validation fails after exhausting retries (LLM_INTEGRATION §3).
    /// </summary>
    public const string DefaultInvalidFallbackReply = "Sorry, I didn't quite get that.";

    /// <summary>
    /// Fallback line spoken by the NPC when a network, timeout, or quota error occurs (LLM_INTEGRATION §3).
    /// </summary>
    public const string DefaultErrorFallbackReply = "Hmm… give me a moment, I can't think right now.";

    /// <summary>
    /// Final status of the compilation.
    /// </summary>
    public CompileStatus Status { get; init; }

    /// <summary>
    /// Spoken in-character or fallback response to be spoken by the NPC to the player.
    /// </summary>
    public string Reply { get; init; } = string.Empty;

    /// <summary>
    /// Short task summary sentence to be recorded in NPC state and memory.
    /// </summary>
    public string TaskSummary { get; init; } = string.Empty;

    /// <summary>
    /// Validated Lua brain source code to be swapped into the NPC's brain host, or empty string.
    /// </summary>
    public string Script { get; init; } = string.Empty;

    /// <summary>
    /// Brain validation result from the final attempt, or null if no script validation occurred.
    /// </summary>
    public ValidationResult? Validation { get; init; }

    /// <summary>
    /// Total number of LLM generation attempts made (initial request + repair retries).
    /// </summary>
    public int Attempts { get; init; }

    /// <summary>
    /// LLM error details if compilation failed due to a provider or communication error.
    /// </summary>
    public LlmError? Error { get; init; }

    /// <summary>
    /// Last raw LLM response received from the queue or client.
    /// </summary>
    public LlmResponse? FinalResponse { get; init; }

    /// <summary>
    /// Gets a value indicating whether compilation was completely successful and produced a validated script.
    /// </summary>
    public bool IsSuccess => Status == CompileStatus.Success;

    public static CompileResult Success(
        string reply,
        string taskSummary,
        string script,
        ValidationResult validation,
        int attempts,
        LlmResponse? finalResponse = null) =>
        new()
        {
            Status = CompileStatus.Success,
            Reply = reply ?? string.Empty,
            TaskSummary = taskSummary ?? string.Empty,
            Script = script ?? string.Empty,
            Validation = validation,
            Attempts = attempts,
            FinalResponse = finalResponse
        };

    public static CompileResult Clarification(
        string reply,
        int attempts,
        LlmResponse? finalResponse = null) =>
        new()
        {
            Status = CompileStatus.ClarificationRequested,
            Reply = reply ?? string.Empty,
            Attempts = attempts,
            FinalResponse = finalResponse
        };

    public static CompileResult KeepCurrent(
        string reply,
        int attempts,
        LlmResponse? finalResponse = null) =>
        new()
        {
            Status = CompileStatus.KeepCurrentTask,
            Reply = reply ?? string.Empty,
            Attempts = attempts,
            FinalResponse = finalResponse
        };

    public static CompileResult ValidationFailed(
        ValidationResult validation,
        int attempts,
        string? fallbackReply = null,
        LlmResponse? finalResponse = null) =>
        new()
        {
            Status = CompileStatus.ValidationFailed,
            Reply = fallbackReply ?? DefaultInvalidFallbackReply,
            Validation = validation,
            Attempts = attempts,
            FinalResponse = finalResponse
        };

    public static CompileResult Failed(
        LlmError error,
        int attempts,
        string? fallbackReply = null,
        LlmResponse? finalResponse = null) =>
        new()
        {
            Status = error.Kind == LlmErrorKind.Cancelled ? CompileStatus.Cancelled : CompileStatus.Error,
            Reply = fallbackReply ?? DefaultErrorFallbackReply,
            Error = error,
            Attempts = attempts,
            FinalResponse = finalResponse
        };

    public static CompileResult Cancelled(string? message = null) =>
        new()
        {
            Status = CompileStatus.Cancelled,
            Reply = string.Empty,
            Error = LlmError.Cancelled(message ?? "Compilation was cancelled."),
            Attempts = 0
        };

    public override string ToString() =>
        $"CompileResult: {Status} (Attempts={Attempts}, Reply=\"{Reply}\", Summary=\"{TaskSummary}\", Script={Script.Length} chars)";
}
