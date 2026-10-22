namespace VoxelKing.Core.Tests.Npc;

using System;
using System.IO;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcProfileLoaderTests
{
    [Fact]
    public void Load_ArrayRoot_ParsesProfilesSuccessfully()
    {
        string json = """
        [
          {
            "name": "Bob",
            "personality": "farmer",
            "skills": ["farming", "building"],
            "appearanceColor": "#3D7BEB",
            "homeOffset": { "x": 2.5, "y": 0.0, "z": 3.0 },
            "initialBrain": "farm_loop",
            "startingInventory": { "stone": 10, "dirt": 5 }
          },
          {
            "name": "Alice",
            "personality": "woodcutter",
            "skills": ["woodcutting"],
            "appearanceColour": "#2ECC71",
            "homeOffset": [ -3.0, 1.0, 2.0 ],
            "brain": "gather_logs"
          }
        ]
        """;

        var profiles = NpcProfileLoader.Load(json);
        Assert.Equal(2, profiles.Count);

        var bob = profiles[0];
        Assert.Equal("Bob", bob.Name);
        Assert.Equal("farmer", bob.Personality);
        Assert.Equal(new[] { "farming", "building" }, bob.Skills);
        Assert.Equal("#3D7BEB", bob.AppearanceColor);
        Assert.Equal(new Vec3(2.5f, 0.0f, 3.0f), bob.HomeOffset);
        Assert.Equal("farm_loop", bob.InitialBrain);
        Assert.NotNull(bob.StartingInventory);
        Assert.Equal(10, bob.StartingInventory!["stone"]);
        Assert.Equal(5, bob.StartingInventory!["dirt"]);

        var alice = profiles[1];
        Assert.Equal("Alice", alice.Name);
        Assert.Equal("woodcutter", alice.Personality);
        Assert.Equal(new[] { "woodcutting" }, alice.Skills);
        Assert.Equal("#2ECC71", alice.AppearanceColor);
        Assert.Equal(new Vec3(-3.0f, 1.0f, 2.0f), alice.HomeOffset);
        Assert.Equal("gather_logs", alice.InitialBrain);
    }

    [Fact]
    public void Load_ObjectRootWithNpcs_ParsesProfiles()
    {
        string json = """
        {
          "npcs": [
            {
              "name": "Charlie",
              "profile": "builder",
              "color": "#E74C3C",
              "home_offset": { "x": 1.0, "y": 2.0, "z": 3.0 }
            }
          ]
        }
        """;

        var profiles = NpcProfileLoader.Load(json);
        Assert.Single(profiles);
        var charlie = profiles[0];
        Assert.Equal("Charlie", charlie.Name);
        Assert.Equal("builder", charlie.Personality);
        Assert.Equal("#E74C3C", charlie.AppearanceColor);
        Assert.Equal(new Vec3(1.0f, 2.0f, 3.0f), charlie.HomeOffset);
    }

    [Fact]
    public void Load_MissingName_ThrowsArgumentException()
    {
        string json = """
        [
          {
            "personality": "ghost"
          }
        ]
        """;

        Assert.Throws<ArgumentException>(() => NpcProfileLoader.Load(json));
    }

    [Fact]
    public void Load_NullOrEmptyJson_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => NpcProfileLoader.Load(null!));
        Assert.Throws<ArgumentException>(() => NpcProfileLoader.Load("   "));
    }

    [Fact]
    public void LoadDefaultProfiles_ReturnsAtLeastFiveDistinctProfiles()
    {
        var defaults = NpcProfileLoader.LoadDefaultProfiles();
        Assert.True(defaults.Count >= 5, "Expected at least 5 default NPC profiles.");

        // Distinct names
        var names = defaults.Select(p => p.Name).ToHashSet();
        Assert.Equal(defaults.Count, names.Count);

        // Distinct appearance colors
        var colors = defaults.Select(p => p.AppearanceColor).ToHashSet();
        Assert.Equal(defaults.Count, colors.Count);

        // All have personalities and skills
        foreach (var p in defaults)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Personality));
            Assert.NotEmpty(p.Skills);
            Assert.False(string.IsNullOrWhiteSpace(p.AppearanceColor));
        }
    }

    [Fact]
    public void LoadFromFile_ConfigNpcsJson_LoadsAtLeastFiveConfiguredProfiles()
    {
        string? configPath = NpcProfileLoader.FindConfigFile();
        Assert.NotNull(configPath);
        Assert.True(File.Exists(configPath));

        var profiles = NpcProfileLoader.LoadFromFile(configPath);
        Assert.True(profiles.Count >= 5, $"Expected at least 5 profiles in config/npcs.json, found {profiles.Count}");

        // Verify Bob, Alice, Charlie, Diana, Edward are present
        var names = profiles.Select(p => p.Name).ToList();
        Assert.Contains("Bob", names);
        Assert.Contains("Alice", names);
        Assert.Contains("Charlie", names);
        Assert.Contains("Diana", names);
        Assert.Contains("Edward", names);

        // Verify distinct appearance colors
        var colors = profiles.Select(p => p.AppearanceColor).Where(c => !string.IsNullOrEmpty(c)).ToHashSet();
        Assert.True(colors.Count >= 5, "Expected at least 5 distinct appearance colors among profiles.");
    }
}
