namespace VoxelKing.Core.Llm;

using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;

/// <summary>
/// Writes full request and response interactions as JSON files to user://logs/llm/&lt;timestamp&gt;_&lt;npc&gt;.json (F-403).
/// </summary>
public sealed class FileLlmRequestLogger : ILlmRequestLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _logDirectory;
    private readonly ILogger? _logger;

    public string LogDirectory => _logDirectory;

    public FileLlmRequestLogger(string logDirectory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _logger = logger;
    }

    public async Task LogAsync(LlmQueueLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            string sanitizedNpcName = SanitizeFileName(entry.NpcName);
            if (string.IsNullOrWhiteSpace(sanitizedNpcName))
            {
                sanitizedNpcName = entry.NpcId.HasValue ? $"npc_{entry.NpcId.Value}" : "global";
            }

            string timestamp = entry.Timestamp.ToString("yyyyMMdd_HHmmss_fff");
            string fileName = $"{timestamp}_{sanitizedNpcName}.json";
            string filePath = Path.Combine(_logDirectory, fileName);

            var dto = new
            {
                timestamp = entry.Timestamp.ToString("o"),
                npcId = entry.NpcId,
                npcName = entry.NpcName,
                durationMs = Math.Round(entry.DurationMs, 2),
                attempts = entry.Attempts,
                isSuccess = entry.IsSuccess,
                request = new
                {
                    systemInstruction = entry.Request.SystemInstruction,
                    messages = entry.Request.Messages,
                    model = entry.Request.Model,
                    temperature = entry.Request.Temperature,
                    maxOutputTokens = entry.Request.MaxOutputTokens
                },
                response = new
                {
                    isSuccess = entry.Response.IsSuccess,
                    rawText = entry.Response.RawText,
                    content = entry.Response.Content,
                    usage = entry.Response.Usage,
                    error = entry.Response.Error != null ? new
                    {
                        kind = entry.Response.Error.Kind.ToString(),
                        message = entry.Response.Error.Message,
                        statusCode = entry.Response.Error.StatusCode,
                        isRetryable = entry.Response.Error.IsRetryable
                    } : null
                }
            };

            string json = JsonSerializer.Serialize(dto, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug($"Wrote LLM audit log to {filePath}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed to write LLM audit log: {ex.Message}");
        }
    }

    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim('_');
    }
}
