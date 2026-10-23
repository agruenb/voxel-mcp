namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;

/// <summary>
/// Encapsulates a complete generation request sent to an <see cref="ILlmClient"/>.
/// Supports single-prompt requests and multi-turn conversations (for validation repair retries).
/// </summary>
public sealed record LlmRequest
{
    /// <summary>
    /// Gets the static system instruction defining roles, rules, API reference, few-shot examples, and schema.
    /// </summary>
    public string SystemInstruction { get; init; }

    /// <summary>
    /// Gets the sequence of message turns (user instructions, previous model outputs, error repair turns).
    /// </summary>
    public IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>
    /// Optional model override (e.g. "gemini-2.5-flash"). If null, uses client configured default.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional max output tokens override. If null, uses client configured default.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    public LlmRequest(
        string systemInstruction,
        IReadOnlyList<LlmMessage> messages,
        string? model = null,
        int? maxOutputTokens = null)
    {
        SystemInstruction = systemInstruction ?? throw new ArgumentNullException(nameof(systemInstruction));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        Model = model;
        MaxOutputTokens = maxOutputTokens;
    }

    /// <summary>
    /// Convenience constructor for a single user prompt turn.
    /// </summary>
    public LlmRequest(
        string systemInstruction,
        string userPrompt,
        string? model = null,
        int? maxOutputTokens = null)
        : this(
            systemInstruction,
            new[] { LlmMessage.User(userPrompt) },
            model,
            maxOutputTokens)
    {
    }

    /// <summary>
    /// Convenience constructor taking a pre-built <see cref="BuiltPrompt"/> from <see cref="IPromptBuilder"/>.
    /// </summary>
    public LlmRequest(BuiltPrompt prompt, string? model = null, int? maxOutputTokens = null)
        : this(prompt.SystemInstruction, prompt.UserContent, model, maxOutputTokens)
    {
    }
}
