namespace VoxelKing.Core.Llm;

/// <summary>
/// Options and budgeting parameters for building LLM prompts.
/// </summary>
public sealed record PromptBuilderOptions
{
    /// <summary>
    /// Maximum number of recent memory events to include before trimming (default: 15 per LLM_INTEGRATION §2.2).
    /// </summary>
    public int MaxEvents { get; set; } = 15;

    /// <summary>
    /// Maximum number of recent conversation turns to include before trimming (default: 4 per LLM_INTEGRATION §2.2).
    /// </summary>
    public int MaxConversationTurns { get; set; } = 4;

    /// <summary>
    /// Maximum estimated token budget for the dynamic user content portion (default: 2000).
    /// </summary>
    public int MaxUserContentTokens { get; set; } = 2000;

    /// <summary>
    /// Maximum number of few-shot examples to include in the system instruction (default: 3).
    /// </summary>
    public int MaxFewShotExamples { get; set; } = 3;
}
