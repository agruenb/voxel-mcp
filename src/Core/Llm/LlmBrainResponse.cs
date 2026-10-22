namespace VoxelKing.Core.Llm;

using System.Text.Json.Serialization;

/// <summary>
/// Strongly-typed structured output received from Gemini corresponding to LLM_INTEGRATION §3.
/// </summary>
public sealed record LlmBrainResponse
{
    /// <summary>
    /// In-character spoken answer to the player (max 200 chars).
    /// </summary>
    [JsonPropertyName("reply")]
    public string Reply { get; init; } = string.Empty;

    /// <summary>
    /// True if the instruction is too ambiguous to produce a valid action; then script is empty and reply asks a question.
    /// </summary>
    [JsonPropertyName("needs_clarification")]
    public bool NeedsClarification { get; init; }

    /// <summary>
    /// True for pure chit-chat that should not change ongoing behaviour; script is empty.
    /// </summary>
    [JsonPropertyName("keep_current_task")]
    public bool KeepCurrentTask { get; init; }

    /// <summary>
    /// One short sentence describing the new task, used in future prompts.
    /// </summary>
    [JsonPropertyName("task_summary")]
    public string TaskSummary { get; init; } = string.Empty;

    /// <summary>
    /// Complete executable Lua brain source code, or empty string.
    /// </summary>
    [JsonPropertyName("script")]
    public string Script { get; init; } = string.Empty;

    public LlmBrainResponse() { }

    public LlmBrainResponse(
        string reply,
        bool needsClarification = false,
        bool keepCurrentTask = false,
        string taskSummary = "",
        string script = "")
    {
        Reply = reply ?? string.Empty;
        NeedsClarification = needsClarification;
        KeepCurrentTask = keepCurrentTask;
        TaskSummary = taskSummary ?? string.Empty;
        Script = script ?? string.Empty;
    }

    public override string ToString() =>
        $"Reply: \"{Reply}\" | Clarify: {NeedsClarification} | Keep: {KeepCurrentTask} | Summary: \"{TaskSummary}\" | Script: {Script.Length} chars";
}
