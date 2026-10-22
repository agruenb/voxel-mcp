namespace VoxelKing.Core.Brain;

using System;

/// <summary>
/// Represents an example Lua brain script with metadata extracted from header comments (F-305).
/// </summary>
public sealed class BrainExample
{
    /// <summary>
    /// Gets the unique identifier/name of the brain example (e.g., "gather_logs" or "syntax_error").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the file name (e.g., "gather_logs.lua").
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the relative path from the examples root (e.g., "gather_logs.lua" or "invalid/syntax_error.lua").
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the absolute file path if loaded from disk, or null if loaded from memory.
    /// </summary>
    public string? FullPath { get; }

    /// <summary>
    /// Gets the instruction prompt extracted from the <c>-- instruction:</c> header.
    /// </summary>
    public string Instruction { get; }

    /// <summary>
    /// Gets the spoken reply extracted from the <c>-- reply:</c> header.
    /// </summary>
    public string Reply { get; }

    /// <summary>
    /// Gets the complete Lua source code.
    /// </summary>
    public string Script { get; }

    /// <summary>
    /// Gets a value indicating whether this is a valid production example or a deliberately broken test script.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error category for invalid samples (e.g., "syntax_error", "unknown_api", "busy_loop", "blocking_in_handler"), or null.
    /// </summary>
    public string? ErrorCategory { get; }

    public BrainExample(
        string name,
        string fileName,
        string relativePath,
        string instruction,
        string reply,
        string script,
        bool isValid = true,
        string? fullPath = null,
        string? errorCategory = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Instruction = instruction ?? string.Empty;
        Reply = reply ?? string.Empty;
        Script = script ?? throw new ArgumentNullException(nameof(script));
        IsValid = isValid;
        FullPath = fullPath;
        ErrorCategory = errorCategory;
    }

    public override string ToString() => $"{Name} (Valid={IsValid}): \"{Instruction}\"";
}
