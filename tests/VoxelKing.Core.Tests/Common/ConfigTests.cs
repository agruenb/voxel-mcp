namespace VoxelKing.Core.Tests.Common;

using System.Text.Json;
using VoxelKing.Core.Common;
using Xunit;

public class ConfigTests
{
    [Fact]
    public void Load_WithDefaults_ReturnsExpectedDefaultValues()
    {
        var config = GameConfigLoader.Load();

        Assert.NotNull(config);
        Assert.NotNull(config.World);
        Assert.NotNull(config.Npc);
        Assert.NotNull(config.BrainLimits);
        Assert.NotNull(config.Llm);
        Assert.NotNull(config.Debug);

        Assert.Equal(32, config.World.ChunkSize);
        Assert.Equal(6, config.World.LoadedChunkRadius);
        Assert.Equal(1337, config.World.Seed);
        Assert.Equal(24, config.World.PerceptionRadiusBlocks);
        Assert.Equal(32, config.World.PerceptionRadiusEntities);

        Assert.Equal(60, config.Npc.PhysicsTickRateHz);
        Assert.Equal(10, config.Npc.BrainTickRateHz);
        Assert.Equal(2, config.Npc.DistantBrainTickRateHz);
        Assert.Equal(64.0f, config.Npc.LodDistanceThreshold);
        Assert.Equal(16.0f, config.Npc.SayRadius);
        Assert.Equal(2.0f, config.Npc.SayRateLimitSeconds);

        Assert.Equal(2000, config.BrainLimits.MaxInstructionsPerTick);
        Assert.Equal(50, config.BrainLimits.MaxConsecutiveForcedYields);
        Assert.Equal(5000, config.BrainLimits.MaxInstructionsPerHandler);
        Assert.Equal(10000, config.BrainLimits.MaxTopLevelChunkInstructions);
        Assert.Equal(8192, config.BrainLimits.MaxScriptSizeBytes);
        Assert.Equal(200, config.BrainLimits.MaxSayLength);
        Assert.Equal(64, config.BrainLimits.MaxMemoryKeys);
        Assert.Equal(16384, config.BrainLimits.MaxMemoryTotalBytes);

        Assert.Equal("gemini-3.8-flash", config.Llm.Model);
        Assert.Equal(8192, config.Llm.MaxOutputTokens);
        Assert.Equal(30, config.Llm.TimeoutSeconds);
        Assert.Equal(2, config.Llm.MaxRetries);
        Assert.Equal(2, config.Llm.MaxConcurrentRequests);
        Assert.Equal(string.Empty, config.Llm.ApiKey);

        Assert.False(config.Debug.ShowOverlay);
        Assert.False(config.Debug.DrawPaths);
        Assert.True(config.Debug.LogLlmRequests);
        Assert.Equal("Info", config.Debug.LogLevel);

        Assert.NotNull(config.Player);
        Assert.Equal(5.0f, config.Player.WalkSpeed);
        Assert.Equal(8.5f, config.Player.SprintSpeed);
        Assert.Equal(7.5f, config.Player.JumpVelocity);
        Assert.Equal(20.0f, config.Player.Gravity);
        Assert.Equal(15.0f, config.Player.FlySpeed);
        Assert.Equal(2.0f, config.Player.SprintFlyMultiplier);
        Assert.Equal(0.003f, config.Player.MouseSensitivity);
        Assert.Equal(1.1f, config.Player.MaxStepHeight);
        Assert.Equal(1.6f, config.Player.EyeHeight);
        Assert.Equal(0.35f, config.Player.BodyRadius);
        Assert.Equal(1.8f, config.Player.BodyHeight);
        Assert.True(config.Player.EnableStepAssist);
        Assert.True(config.Player.EnableStepSmoothing);
    }

    [Fact]
    public void Load_WithBaseJson_OverridesDefaults()
    {
        const string baseJson = """
        {
          "world": {
            "seed": 99999,
            "loadedChunkRadius": 10
          },
          "llm": {
            "model": "gemini-1.5-pro",
            "maxOutputTokens": 4096
          }
        }
        """;

        var config = GameConfigLoader.Load(baseJson: baseJson);

        Assert.Equal(99999, config.World.Seed);
        Assert.Equal(10, config.World.LoadedChunkRadius);
        Assert.Equal(32, config.World.ChunkSize); // unmentioned property keeps default

        Assert.Equal("gemini-1.5-pro", config.Llm.Model);
        Assert.Equal(4096, config.Llm.MaxOutputTokens);
        Assert.Equal(30, config.Llm.TimeoutSeconds); // unmentioned property keeps default
    }

    [Fact]
    public void Load_WithUserJson_OverridesBaseAndDefaults()
    {
        const string baseJson = """
        {
          "world": {
            "seed": 100,
            "loadedChunkRadius": 4
          },
          "llm": {
            "model": "gemini-3.8-flash",
            "apiKey": "base-key"
          }
        }
        """;

        const string userJson = """
        {
          "world": {
            "loadedChunkRadius": 8
          },
          "llm": {
            "apiKey": "user-secret-key"
          },
          "debug": {
            "showOverlay": true
          }
        }
        """;

        var config = GameConfigLoader.Load(baseJson: baseJson, userJson: userJson);

        // Overridden by user
        Assert.Equal(8, config.World.LoadedChunkRadius);
        Assert.Equal("user-secret-key", config.Llm.ApiKey);
        Assert.True(config.Debug.ShowOverlay);

        // Preserved from base
        Assert.Equal(100, config.World.Seed);
        Assert.Equal("gemini-3.8-flash", config.Llm.Model);

        // Preserved from default
        Assert.Equal(32, config.World.ChunkSize);
        Assert.False(config.Debug.DrawPaths);
    }

    [Fact]
    public void Load_WithEnvApiKey_OverridesBaseAndUserConfigs()
    {
        const string baseJson = """{ "llm": { "apiKey": "base-key" } }""";
        const string userJson = """{ "llm": { "apiKey": "user-key" } }""";
        const string envApiKey = "env-override-key-12345";

        var config = GameConfigLoader.Load(baseJson, userJson, envApiKey);

        Assert.Equal("env-override-key-12345", config.Llm.ApiKey);
    }

    [Fact]
    public void Load_WithEmptyEnvApiKey_DoesNotOverrideConfigKey()
    {
        const string userJson = """{ "llm": { "apiKey": "user-key" } }""";

        var config1 = GameConfigLoader.Load(null, userJson, string.Empty);
        var config2 = GameConfigLoader.Load(null, userJson, "   ");

        Assert.Equal("user-key", config1.Llm.ApiKey);
        Assert.Equal("user-key", config2.Llm.ApiKey);
    }

    [Fact]
    public void Load_MissingUserJson_SucceedsWithBaseOrDefaults()
    {
        const string baseJson = """{ "world": { "seed": 42 } }""";

        var configWithNull = GameConfigLoader.Load(baseJson, null);
        var configWithEmpty = GameConfigLoader.Load(baseJson, "");
        var configWithWhitespace = GameConfigLoader.Load(baseJson, "   \n\t  ");

        Assert.Equal(42, configWithNull.World.Seed);
        Assert.Equal(42, configWithEmpty.World.Seed);
        Assert.Equal(42, configWithWhitespace.World.Seed);
    }

    [Fact]
    public void Load_WithCommentsAndTrailingCommas_ParsesSuccessfully()
    {
        const string jsonWithComments = """
        {
          // World settings
          "world": {
            "seed": 777, // custom seed
            "loadedChunkRadius": 5,
          },
          /* LLM section */
          "llm": {
            "model": "gemini-3.8-flash",
          },
        }
        """;

        var config = GameConfigLoader.Load(baseJson: jsonWithComments);

        Assert.Equal(777, config.World.Seed);
        Assert.Equal(5, config.World.LoadedChunkRadius);
    }

    [Fact]
    public void Load_WithCaseInsensitiveAndSnakeCaseKeys_MergesCorrectly()
    {
        const string json = """
        {
          "World": {
            "chunk_size": 16,
            "Perception_Radius_Blocks": 40
          },
          "brain_limits": {
            "max_instructions_per_tick": 4000
          }
        }
        """;

        var config = GameConfigLoader.Load(baseJson: json);

        Assert.Equal(16, config.World.ChunkSize);
        Assert.Equal(40, config.World.PerceptionRadiusBlocks);
        Assert.Equal(4000, config.BrainLimits.MaxInstructionsPerTick);
    }

    [Fact]
    public void LoadFromFiles_WithValidFiles_MergesSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "VoxelKing_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string basePath = Path.Combine(tempDir, "game_config.json");
            string userPath = Path.Combine(tempDir, "user_config.json");

            File.WriteAllText(basePath, """{ "world": { "seed": 500 } }""");
            File.WriteAllText(userPath, """{ "world": { "loadedChunkRadius": 12 }, "llm": { "apiKey": "secret" } }""");

            var config = GameConfigLoader.LoadFromFiles(basePath, userPath);

            Assert.Equal(500, config.World.Seed);
            Assert.Equal(12, config.World.LoadedChunkRadius);
            Assert.Equal("secret", config.Llm.ApiKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void LoadFromFiles_WithMissingUserFile_Succeeds()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "VoxelKing_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string basePath = Path.Combine(tempDir, "game_config.json");
            string missingUserPath = Path.Combine(tempDir, "non_existent_config.json");

            File.WriteAllText(basePath, """{ "world": { "seed": 500 } }""");

            var config = GameConfigLoader.LoadFromFiles(basePath, missingUserPath);

            Assert.Equal(500, config.World.Seed);
            Assert.Equal(string.Empty, config.Llm.ApiKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ConfigProvider_ConstructorInjection_ProvidesConfigAndNotifiesOnUpdate()
    {
        var initial = new GameConfig();
        var provider = new ConfigProvider(initial);

        // Verify interface implementation
        IGameConfigProvider interfaceProvider = provider;
        Assert.Same(initial, interfaceProvider.Config);

        GameConfig? receivedConfig = null;
        provider.ConfigChanged += cfg => receivedConfig = cfg;

        var updated = initial with { World = initial.World with { Seed = 999 } };
        provider.Update(updated);

        Assert.Same(updated, provider.Config);
        Assert.Same(updated, receivedConfig);
    }

    [Fact]
    public void ToJson_RoundTrip_MatchesConfig()
    {
        var original = new GameConfig
        {
            World = new WorldConfig { Seed = 42, LoadedChunkRadius = 10 },
            Llm = new LlmConfig { Model = "gemini-test", MaxOutputTokens = 4096 }
        };

        string json = GameConfigLoader.ToJson(original);
        var restored = GameConfigLoader.Load(baseJson: json);

        Assert.Equal(original.World.Seed, restored.World.Seed);
        Assert.Equal(original.World.LoadedChunkRadius, restored.World.LoadedChunkRadius);
        Assert.Equal(original.Llm.Model, restored.Llm.Model);
        Assert.Equal(original.Llm.MaxOutputTokens, restored.Llm.MaxOutputTokens);
    }

    [Fact]
    public void Load_WithPlayerJson_OverridesPlayerDefaults()
    {
        const string json = """
        {
          "player": {
            "walkSpeed": 6.5,
            "sprintSpeed": 11.0,
            "jumpVelocity": 8.0,
            "maxStepHeight": 1.25,
            "enableStepAssist": false
          }
        }
        """;

        var config = GameConfigLoader.Load(baseJson: json);

        Assert.Equal(6.5f, config.Player.WalkSpeed);
        Assert.Equal(11.0f, config.Player.SprintSpeed);
        Assert.Equal(8.0f, config.Player.JumpVelocity);
        Assert.Equal(1.25f, config.Player.MaxStepHeight);
        Assert.False(config.Player.EnableStepAssist);

        // Verify unmentioned properties retain defaults
        Assert.Equal(20.0f, config.Player.Gravity);
        Assert.Equal(15.0f, config.Player.FlySpeed);
        Assert.True(config.Player.EnableStepSmoothing);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsJsonException()
    {
        const string badJson = "{ invalid json content }";

        Assert.ThrowsAny<JsonException>(() => GameConfigLoader.Load(baseJson: badJson));
    }
}
