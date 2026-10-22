namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Result returned by an <see cref="ILlmClient"/> call.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Gets a value indicating whether the request succeeded and produced a valid structured response.
    /// </summary>
    public bool IsSuccess => Error == null && Content != null;

    /// <summary>
    /// Gets the structured brain response if successful, or null on error.
    /// </summary>
    public LlmBrainResponse? Content { get; init; }

    /// <summary>
    /// Gets the raw text returned by the model before parsing.
    /// </summary>
    public string RawText { get; init; } = string.Empty;

    /// <summary>
    /// Gets token consumption statistics reported or estimated for this call.
    /// </summary>
    public LlmUsage Usage { get; init; }

    /// <summary>
    /// Gets error details if the call failed.
    /// </summary>
    public LlmError? Error { get; init; }

    public static LlmResponse Success(LlmBrainResponse content, string rawText, LlmUsage usage)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new LlmResponse
        {
            Content = content,
            RawText = rawText ?? string.Empty,
            Usage = usage,
            Error = null
        };
    }

    public static LlmResponse Failure(LlmError error, string? rawText = null, LlmUsage? usage = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LlmResponse
        {
            Content = null,
            RawText = rawText ?? string.Empty,
            Usage = usage ?? LlmUsage.Zero,
            Error = error
        };
    }

    public override string ToString() =>
        IsSuccess
            ? $"LlmResponse: Success ({Usage}, Summary=\"{Content?.TaskSummary}\")"
            : $"LlmResponse: Failure ({Error})";
}
