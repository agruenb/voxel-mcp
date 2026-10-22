namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Encapsulates a compiled prompt ready to be transmitted to an LLM provider (Google Gemini).
/// Separates the static, cacheable system instruction from the dynamic user content.
/// </summary>
public sealed record BuiltPrompt
{
    /// <summary>
    /// Gets the static system instruction (role, rules, API reference, few-shot examples, schema).
    /// Suitable for LLM context caching.
    /// </summary>
    public string SystemInstruction { get; init; }

    /// <summary>
    /// Gets the dynamic user content containing the NPC state snapshot and player instruction.
    /// </summary>
    public string UserContent { get; init; }

    /// <summary>
    /// Gets the estimated token count of the system instruction.
    /// </summary>
    public int SystemTokens { get; init; }

    /// <summary>
    /// Gets the estimated token count of the user content.
    /// </summary>
    public int UserTokens { get; init; }

    /// <summary>
    /// Gets the total estimated token count across system instruction and user content.
    /// </summary>
    public int TotalEstimatedTokens => SystemTokens + UserTokens;

    public BuiltPrompt(string systemInstruction, string userContent, int systemTokens, int userTokens)
    {
        SystemInstruction = systemInstruction ?? throw new ArgumentNullException(nameof(systemInstruction));
        UserContent = userContent ?? throw new ArgumentNullException(nameof(userContent));
        SystemTokens = systemTokens >= 0 ? systemTokens : 0;
        UserTokens = userTokens >= 0 ? userTokens : 0;
    }
}
