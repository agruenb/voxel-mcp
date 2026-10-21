namespace VoxelKing.Core.Common;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Loads and merges game configuration from default JSON, user JSON, and environment variables.
/// </summary>
public static class GameConfigLoader
{
    public const string EnvironmentApiKeyVariable = "GEMINI_API_KEY";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads and merges configuration from JSON strings and optional environment key override.
    /// </summary>
    /// <param name="baseJson">Base configuration JSON (committed defaults), or null to use code defaults.</param>
    /// <param name="userJson">User configuration JSON (local overrides), or null if none.</param>
    /// <param name="envApiKey">Explicit API key override. If null, reads <c>GEMINI_API_KEY</c> environment variable.</param>
    /// <returns>A merged <see cref="GameConfig"/> instance.</returns>
    public static GameConfig Load(
        string? baseJson = null,
        string? userJson = null,
        string? envApiKey = null)
    {
        // 1. Initialize root node with defaults
        var defaultConfig = new GameConfig();
        var rootNode = JsonSerializer.SerializeToNode(defaultConfig, JsonOptions) as JsonObject
            ?? new JsonObject();

        // 2. Merge base JSON if provided
        if (!string.IsNullOrWhiteSpace(baseJson))
        {
            var baseNode = JsonNode.Parse(baseJson, null, DocumentOptions) as JsonObject
                ?? throw new JsonException("Base configuration JSON must represent a JSON object.");
            MergeObjects(rootNode, baseNode);
        }

        // 3. Merge user JSON if provided
        if (!string.IsNullOrWhiteSpace(userJson))
        {
            var userNode = JsonNode.Parse(userJson, null, DocumentOptions) as JsonObject
                ?? throw new JsonException("User configuration JSON must represent a JSON object.");
            MergeObjects(rootNode, userNode);
        }

        // 4. Deserialize merged JsonObject to GameConfig
        var config = rootNode.Deserialize<GameConfig>(JsonOptions) ?? new GameConfig();

        // 5. Environment variable override for GEMINI_API_KEY
        var effectiveApiKey = envApiKey ?? Environment.GetEnvironmentVariable(EnvironmentApiKeyVariable);
        if (!string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            config.Llm.ApiKey = effectiveApiKey;
        }

        return config;
    }

    /// <summary>
    /// Loads configuration from file paths. Missing user configuration file is handled gracefully.
    /// </summary>
    /// <param name="baseConfigPath">Path to base configuration file.</param>
    /// <param name="userConfigPath">Optional path to user configuration file (e.g. user://config.json).</param>
    /// <param name="envApiKey">Optional API key override.</param>
    /// <returns>A merged <see cref="GameConfig"/> instance.</returns>
    public static GameConfig LoadFromFiles(
        string baseConfigPath,
        string? userConfigPath = null,
        string? envApiKey = null)
    {
        string? baseJson = File.Exists(baseConfigPath) ? File.ReadAllText(baseConfigPath) : null;
        string? userJson = (userConfigPath != null && File.Exists(userConfigPath))
            ? File.ReadAllText(userConfigPath)
            : null;

        return Load(baseJson, userJson, envApiKey);
    }

    /// <summary>
    /// Serializes a <see cref="GameConfig"/> instance to a JSON string.
    /// </summary>
    public static string ToJson(GameConfig config, bool writeIndented = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        var options = writeIndented
            ? JsonOptions
            : new JsonSerializerOptions(JsonOptions) { WriteIndented = false };
        return JsonSerializer.Serialize(config, options);
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var (key, sourceValue) in source)
        {
            string matchingKey = FindMatchingKey(target, key);

            if (target.TryGetPropertyValue(matchingKey, out var targetValue) &&
                targetValue is JsonObject targetObj &&
                sourceValue is JsonObject sourceObj)
            {
                MergeObjects(targetObj, sourceObj);
            }
            else
            {
                target[matchingKey] = sourceValue?.DeepClone();
            }
        }
    }

    private static string FindMatchingKey(JsonObject target, string key)
    {
        // 1. Direct or case-insensitive match
        foreach (var existingKey in target.Select(p => p.Key))
        {
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return existingKey;
            }
        }

        // 2. Snake_case to camelCase match (ignore underscores)
        var strippedKey = key.Replace("_", string.Empty);
        foreach (var existingKey in target.Select(p => p.Key))
        {
            var strippedExisting = existingKey.Replace("_", string.Empty);
            if (string.Equals(strippedExisting, strippedKey, StringComparison.OrdinalIgnoreCase))
            {
                return existingKey;
            }
        }

        return key;
    }
}
