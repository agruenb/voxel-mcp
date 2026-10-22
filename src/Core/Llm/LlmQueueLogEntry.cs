namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Record representing an LLM request/response interaction logged for diagnostics and prompt debugging (F-403).
/// </summary>
public sealed record LlmQueueLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int? NpcId { get; init; }
    public string NpcName { get; init; } = string.Empty;
    public double DurationMs { get; init; }
    public int Attempts { get; init; }
    public bool IsSuccess { get; init; }
    public LlmRequest Request { get; init; } = default!;
    public LlmResponse Response { get; init; } = default!;
}
