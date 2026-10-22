namespace VoxelKing.Core.Llm;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Parser and validator for structured Gemini JSON responses matching LLM_INTEGRATION §3 and §4 Stage 1.
/// </summary>
public static class LlmResponseParser
{
    public const int MaxReplyLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Attempts to parse raw JSON text (with or without markdown code fences) into an <see cref="LlmBrainResponse"/>.
    /// </summary>
    /// <param name="rawText">Raw text received from LLM candidate part.</param>
    /// <param name="response">Populated response if successful, otherwise null.</param>
    /// <param name="errorMessage">Error description if parsing failed, otherwise null.</param>
    /// <returns>True if parsing and schema validation succeeded, otherwise false.</returns>
    public static bool TryParse(
        string? rawText,
        [NotNullWhen(true)] out LlmBrainResponse? response,
        [NotNullWhen(false)] out string? errorMessage)
    {
        response = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errorMessage = "Raw response text is empty or null.";
            return false;
        }

        string cleanedJson = StripCodeFences(rawText.Trim());

        try
        {
            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Expected JSON object at root, got '{root.ValueKind}'.";
                return false;
            }

            // Verify required property per schema: script
            string? script = GetStringProperty(root, "script");
            if (script == null)
            {
                errorMessage = "Required property 'script' is missing or not a string.";
                return false;
            }

            // Optional properties with defaults
            string reply = GetStringProperty(root, "reply") ?? string.Empty;
            if (reply.Length > MaxReplyLength)
            {
                reply = reply[..MaxReplyLength];
            }

            TryGetBooleanProperty(root, "needs_clarification", out bool needsClarification);
            TryGetBooleanProperty(root, "keep_current_task", out bool keepCurrentTask);
            string taskSummary = GetStringProperty(root, "task_summary") ?? string.Empty;

            response = new LlmBrainResponse
            {
                Reply = reply,
                NeedsClarification = needsClarification,
                KeepCurrentTask = keepCurrentTask,
                TaskSummary = taskSummary,
                Script = script
            };

            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"JSON parsing error: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected parsing error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Parses raw JSON text, throwing an exception if invalid.
    /// </summary>
    public static LlmBrainResponse Parse(string rawText)
    {
        if (!TryParse(rawText, out var response, out var error))
        {
            throw new FormatException($"Failed to parse LLM structured response: {error}");
        }

        return response;
    }

    /// <summary>
    /// Strips leading and trailing markdown code block delimiters (e.g. ```json ... ```).
    /// </summary>
    public static string StripCodeFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string trimmed = text.Trim();

        // Check if starts with code fence
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..].Trim();
            }
            else
            {
                trimmed = trimmed[3..].Trim();
            }
        }

        // Check if ends with code fence
        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3].Trim();
        }

        return trimmed;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name.Replace("_", ""), propertyName.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Null => string.Empty,
                    _ => null
                };
            }
        }

        return null;
    }

    private static bool TryGetBooleanProperty(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name.Replace("_", ""), propertyName.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    value = prop.Value.GetBoolean();
                    return true;
                }

                return false;
            }
        }

        return false;
    }
}
