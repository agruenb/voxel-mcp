namespace VoxelKing.Core.Llm;

/// <summary>
/// Event broadcast across the EventBus when an LLM operational issue or fallback occurs (e.g. no API key).
/// </summary>
public readonly record struct LlmWarningEvent(string Message, string? Detail = null);
