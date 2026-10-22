namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Provides discovery, header parsing, and loading for the example Lua brain library (F-305).
/// </summary>
public static class BrainExampleLibrary
{
    private static readonly Regex InstructionRegex = new(
        @"^--\s*instruction:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex ReplyRegex = new(
        @"^--\s*reply:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>
    /// Parses a Lua script string into a <see cref="BrainExample"/>, extracting header comments.
    /// </summary>
    /// <param name="name">Identifier or basename of the script.</param>
    /// <param name="relativePath">Relative path inside the examples directory (e.g. "gather_logs.lua" or "invalid/syntax_error.lua").</param>
    /// <param name="scriptText">Raw Lua source text.</param>
    /// <param name="fullPath">Optional absolute path on disk.</param>
    /// <returns>A populated <see cref="BrainExample"/>.</returns>
    public static BrainExample Parse(string name, string relativePath, string scriptText, string? fullPath = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(scriptText);

        string fileName = Path.GetFileName(relativePath);
        string instruction = ExtractHeaderValue(InstructionRegex, scriptText);
        string reply = ExtractHeaderValue(ReplyRegex, scriptText);

        string normalizedPath = relativePath.Replace('\\', '/');
        bool isInvalid = normalizedPath.StartsWith("invalid/", StringComparison.OrdinalIgnoreCase);

        string? errorCategory = null;
        if (isInvalid)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            if (baseName.Contains("syntax"))
            {
                errorCategory = "syntax_error";
            }
            else if (baseName.Contains("unknown") || baseName.Contains("api"))
            {
                errorCategory = "unknown_api";
            }
            else if (baseName.Contains("busy") || baseName.Contains("loop"))
            {
                errorCategory = "busy_loop";
            }
            else if (baseName.Contains("blocking") || baseName.Contains("handler"))
            {
                errorCategory = "blocking_in_handler";
            }
            else
            {
                errorCategory = baseName;
            }
        }

        return new BrainExample(
            name,
            fileName,
            relativePath,
            instruction,
            reply,
            scriptText,
            isValid: !isInvalid,
            fullPath: fullPath,
            errorCategory: errorCategory);
    }

    /// <summary>
    /// Attempts to locate the <c>brains/examples</c> directory from the current or base directory by searching upwards.
    /// </summary>
    /// <param name="startDirectory">Optional directory to begin searching from; defaults to current directory.</param>
    /// <returns>Absolute directory path if found, or null if not found.</returns>
    public static string? FindExamplesDirectory(string? startDirectory = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(startDirectory))
        {
            candidates.Add(startDirectory);
        }
        candidates.Add(Directory.GetCurrentDirectory());
        candidates.Add(AppContext.BaseDirectory);

        foreach (var start in candidates)
        {
            string? current = Path.GetFullPath(start);
            for (int depth = 0; depth < 8 && current != null; depth++)
            {
                string path1 = Path.Combine(current, "brains", "examples");
                if (Directory.Exists(path1))
                {
                    return path1;
                }

                string path2 = Path.Combine(current, "examples");
                if (Directory.Exists(path2) && File.Exists(Path.Combine(path2, "gather_logs.lua")))
                {
                    return path2;
                }

                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads all <see cref="BrainExample"/>s from the specified directory.
    /// </summary>
    /// <param name="directoryPath">Directory path containing the example Lua files.</param>
    /// <returns>List of discovered and parsed <see cref="BrainExample"/>s.</returns>
    public static IReadOnlyList<BrainExample> LoadFromDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Brain examples directory not found: {directoryPath}");
        }

        var results = new List<BrainExample>();
        var files = Directory.GetFiles(directoryPath, "*.lua", SearchOption.AllDirectories);

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(directoryPath, file).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(file);
            string script = File.ReadAllText(file);

            var example = Parse(name, relative, script, fullPath: Path.GetFullPath(file));
            results.Add(example);
        }

        return results;
    }

    /// <summary>
    /// Automatically locates the examples directory and loads all examples.
    /// </summary>
    public static IReadOnlyList<BrainExample> LoadAll(string? startDirectory = null)
    {
        string? dir = FindExamplesDirectory(startDirectory);
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate brains/examples directory.");
        }

        return LoadFromDirectory(dir);
    }

    private static string ExtractHeaderValue(Regex regex, string scriptText)
    {
        var match = regex.Match(scriptText);
        if (!match.Success || match.Groups.Count < 2)
        {
            return string.Empty;
        }

        string val = match.Groups[1].Value.Trim();
        if (val.Length >= 2 &&
            ((val.StartsWith("\"") && val.EndsWith("\"")) ||
             (val.StartsWith("'") && val.EndsWith("'"))))
        {
            val = val.Substring(1, val.Length - 2).Trim();
        }

        return val;
    }
}
