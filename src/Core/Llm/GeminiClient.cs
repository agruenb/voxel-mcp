namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;

/// <summary>
/// Production REST client for Google Gemini generateContent with structured JSON schema output (LLM_INTEGRATION §2).
/// </summary>
public sealed class GeminiClient : ILlmClient
{
    private const string BaseApiUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly ILogger? _logger;

    public GeminiClient(HttpClient httpClient, LlmConfig config, ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string model = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : _config.Model;
        string endpoint = $"{BaseApiUrl}/{Uri.EscapeDataString(model)}:generateContent";

        string requestJson = BuildRequestBodyJson(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _config.ApiKey);
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            _logger?.LogDebug($"Sending Gemini generateContent request to model '{model}'...");

            using var httpResponse = await _httpClient.SendAsync(httpRequest, linkedCts.Token).ConfigureAwait(false);
            string responseBody = await httpResponse.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = MapHttpError((int)httpResponse.StatusCode, responseBody);
                _logger?.LogWarning($"Gemini request failed: {error}");
                return LlmResponse.Failure(error, rawText: responseBody);
            }

            return ParseSuccessResponse(responseBody);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogInfo("Gemini request was cancelled by caller.");
            return LlmResponse.Failure(LlmError.Cancelled());
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning($"Gemini request timed out after {_config.TimeoutSeconds} seconds.");
            return LlmResponse.Failure(LlmError.Timeout());
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError($"Network exception while sending Gemini request: {ex.Message}");
            return LlmResponse.Failure(LlmError.Network(ex.Message, ex));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Unexpected exception in GeminiClient: {ex.Message}");
            return LlmResponse.Failure(LlmError.Unknown(ex.Message, ex));
        }
    }

    private string BuildRequestBodyJson(LlmRequest request)
    {
        var root = new JsonObject();

        // 1. System instruction
        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            root["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = request.SystemInstruction }
                }
            };
        }

        // 2. Contents (turn-based messages)
        var contentsArray = new JsonArray();
        foreach (var msg in request.Messages)
        {
            contentsArray.Add(new JsonObject
            {
                ["role"] = msg.Role,
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = msg.Text }
                }
            });
        }
        root["contents"] = contentsArray;

        // 3. Generation config with structured JSON response schema (LLM_INTEGRATION §3)
        var properties = new JsonObject
        {
            ["reply"] = new JsonObject
            {
                ["type"] = "STRING",
                ["description"] = "In-character spoken answer, max 200 chars"
            },
            ["needs_clarification"] = new JsonObject
            {
                ["type"] = "BOOLEAN",
                ["description"] = "true if the instruction is too ambiguous; then script is empty and reply asks a question"
            },
            ["keep_current_task"] = new JsonObject
            {
                ["type"] = "BOOLEAN",
                ["description"] = "true for pure chit-chat that should not change behaviour"
            },
            ["task_summary"] = new JsonObject
            {
                ["type"] = "STRING",
                ["description"] = "One short sentence describing the new task, used in future prompts"
            },
            ["script"] = new JsonObject
            {
                ["type"] = "STRING",
                ["description"] = "Complete Lua brain source, or empty"
            }
        };

        var required = new JsonArray
        {
            "reply",
            "needs_clarification",
            "keep_current_task",
            "task_summary",
            "script"
        };

        var responseSchema = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = properties,
            ["required"] = required
        };

        root["generationConfig"] = new JsonObject
        {
            ["responseMimeType"] = "application/json",
            ["responseSchema"] = responseSchema,
            ["temperature"] = request.Temperature ?? _config.Temperature,
            ["maxOutputTokens"] = request.MaxOutputTokens ?? _config.MaxOutputTokens
        };

        return root.ToJsonString();
    }

    private static LlmResponse ParseSuccessResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract candidate text
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return LlmResponse.Failure(LlmError.Unknown("No candidates returned in Gemini response."), rawText: json);
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                return LlmResponse.Failure(LlmError.Unknown("No content parts returned in candidate."), rawText: json);
            }

            string rawText = parts[0].TryGetProperty("text", out var textEl) ? (textEl.GetString() ?? string.Empty) : string.Empty;

            // Extract usage metadata
            int promptTokens = 0;
            int candidatesTokens = 0;
            int totalTokens = 0;

            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pt)) promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var ct)) candidatesTokens = ct.GetInt32();
                if (usage.TryGetProperty("totalTokenCount", out var tt)) totalTokens = tt.GetInt32();
                else totalTokens = promptTokens + candidatesTokens;
            }

            var usageStruct = new LlmUsage(promptTokens, candidatesTokens, totalTokens);

            // Parse structured JSON response
            if (!LlmResponseParser.TryParse(rawText, out var brainResponse, out string? parseError))
            {
                return LlmResponse.Failure(LlmError.InvalidJson(parseError), rawText: rawText, usage: usageStruct);
            }

            return LlmResponse.Success(brainResponse, rawText, usageStruct);
        }
        catch (Exception ex)
        {
            return LlmResponse.Failure(LlmError.InvalidJson($"Failed to parse Gemini response wrapper: {ex.Message}"), rawText: json);
        }
    }

    private static LlmError MapHttpError(int statusCode, string responseBody)
    {
        string errorMessage = ExtractGeminiErrorMessage(responseBody, out string? statusString);

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = $"Gemini HTTP request failed with status code {statusCode}.";
        }

        switch (statusCode)
        {
            case 400:
                if (string.Equals(statusString, "API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("API key", StringComparison.OrdinalIgnoreCase))
                {
                    return LlmError.InvalidApiKey(errorMessage, statusCode);
                }
                return LlmError.BadRequest(errorMessage, statusCode);

            case 401 or 403:
                return LlmError.InvalidApiKey(errorMessage, statusCode);

            case 404:
                return new LlmError(LlmErrorKind.NotFound, errorMessage, statusCode);

            case 429:
                return LlmError.RateLimited(errorMessage, statusCode);

            case 500 or 502 or 503 or 504:
                return LlmError.Server(errorMessage, statusCode);

            default:
                return new LlmError(LlmErrorKind.Unknown, errorMessage, statusCode);
        }
    }

    private static string ExtractGeminiErrorMessage(string json, out string? statusString)
    {
        statusString = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                if (errorEl.TryGetProperty("status", out var s))
                {
                    statusString = s.GetString();
                }

                if (errorEl.TryGetProperty("message", out var m))
                {
                    return m.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fall back to returning raw response body
        }

        return json;
    }
}
