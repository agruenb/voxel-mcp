namespace VoxelKing.Core.Common;

/// <summary>
/// Root configuration tree for the game.
/// </summary>
public sealed record GameConfig : IGameConfig
{
    public WorldConfig World { get; set; } = new();
    public NpcConfig Npc { get; set; } = new();
    public BrainLimitsConfig BrainLimits { get; set; } = new();
    public LlmConfig Llm { get; set; } = new();
    public DebugConfig Debug { get; set; } = new();
    public PlayerConfig Player { get; set; } = new();
}

/// <summary>
/// World generation, chunking and spatial perception configuration.
/// </summary>
public sealed record WorldConfig
{
    public int ChunkSize { get; set; } = 32;
    public int LoadedChunkRadius { get; set; } = 6;
    public int Seed { get; set; } = 1337;
    public int PerceptionRadiusBlocks { get; set; } = 24;
    public int PerceptionRadiusEntities { get; set; } = 32;
}

/// <summary>
/// NPC simulation, ticking rates, level of detail, and speech constraints.
/// </summary>
public sealed record NpcConfig
{
    public int PhysicsTickRateHz { get; set; } = 60;
    public int BrainTickRateHz { get; set; } = 10;
    public int DistantBrainTickRateHz { get; set; } = 2;
    public float LodDistanceThreshold { get; set; } = 64.0f;
    public float SayRadius { get; set; } = 16.0f;
    public float SayRateLimitSeconds { get; set; } = 2.0f;
}

/// <summary>
/// Lua brain sandbox runtime limits, safety boundaries, and NPC memory storage quotas.
/// </summary>
public sealed record BrainLimitsConfig
{
    public int MaxInstructionsPerTick { get; set; } = 2000;
    public int MaxConsecutiveForcedYields { get; set; } = 50;
    public int MaxInstructionsPerHandler { get; set; } = 5000;
    public int MaxTopLevelChunkInstructions { get; set; } = 10000;
    public int MaxScriptSizeBytes { get; set; } = 8192;
    public int MaxSayLength { get; set; } = 200;

    /// <summary>
    /// Maximum number of unique keys permitted in an NPC's persistent KV store.
    /// </summary>
    public int MaxMemoryKeys { get; set; } = 64;

    /// <summary>
    /// Maximum total memory footprint in bytes across all keys and values in an NPC's KV store (default: 16 KB).
    /// </summary>
    public int MaxMemoryTotalBytes { get; set; } = 16384;

    /// <summary>
    /// Maximum length in characters of an individual key name in the KV store.
    /// </summary>
    public int MaxMemoryKeyLength { get; set; } = 64;

    /// <summary>
    /// Maximum length in characters of a string value stored in the KV store.
    /// </summary>
    public int MaxMemoryStringValueLength { get; set; } = 4096;

    /// <summary>
    /// Maximum number of named places an NPC can remember simultaneously.
    /// </summary>
    public int MaxMemoryPlaces { get; set; } = 64;

    /// <summary>
    /// Maximum length in characters of a named place identifier.
    /// </summary>
    public int MaxMemoryPlaceNameLength { get; set; } = 64;

    /// <summary>
    /// Maximum number of events retained in the FIFO event log ring buffer for prompt generation.
    /// </summary>
    public int MemoryEventCapacity { get; set; } = 50;
}

/// <summary>
/// LLM provider (Google Gemini) integration settings.
/// </summary>
public sealed record LlmConfig
{
    public string Model { get; set; } = "gemini-3.8-flash";
    public float Temperature { get; set; } = 0.3f;
    public int MaxOutputTokens { get; set; } = 8192;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 2;
    public int MaxRepairRetries { get; set; } = 2;
    public int MaxConcurrentRequests { get; set; } = 2;
    public int RequestsPerMinute { get; set; } = 60;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Debug overlays, visualization, and diagnostics logging configuration.
/// </summary>
public sealed record DebugConfig
{
    public bool ShowOverlay { get; set; } = false;
    public bool DrawPaths { get; set; } = false;
    public bool LogLlmRequests { get; set; } = true;
    public string LogLevel { get; set; } = "Info";
    public string LlmLogDirectory { get; set; } = "user://logs/llm";
}
