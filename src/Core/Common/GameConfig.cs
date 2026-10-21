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
/// Lua brain sandbox runtime limits and safety boundaries.
/// </summary>
public sealed record BrainLimitsConfig
{
    public int MaxInstructionsPerTick { get; set; } = 2000;
    public int MaxConsecutiveForcedYields { get; set; } = 50;
    public int MaxInstructionsPerHandler { get; set; } = 5000;
    public int MaxTopLevelChunkInstructions { get; set; } = 10000;
    public int MaxScriptSizeBytes { get; set; } = 8192;
    public int MaxSayLength { get; set; } = 200;
    public int MaxMemoryKeys { get; set; } = 64;
    public int MaxMemoryTotalBytes { get; set; } = 16384;
}

/// <summary>
/// LLM provider (Google Gemini) integration settings.
/// </summary>
public sealed record LlmConfig
{
    public string Model { get; set; } = "gemini-2.5-flash";
    public float Temperature { get; set; } = 0.3f;
    public int MaxOutputTokens { get; set; } = 2048;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 2;
    public int MaxConcurrentRequests { get; set; } = 2;
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
}
