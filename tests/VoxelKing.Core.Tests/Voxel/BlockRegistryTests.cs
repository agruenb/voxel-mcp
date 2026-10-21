namespace VoxelKing.Core.Tests.Voxel;

using System;
using System.Collections.Generic;
using System.IO;
using VoxelKing.Core.Voxel;
using Xunit;

public class BlockRegistryTests
{
    [Fact]
    public void CreateDefault_ContainsExpected12InitialBlocks()
    {
        var registry = BlockRegistry.CreateDefault();

        Assert.Equal(12, registry.Count);
        Assert.Equal(0, registry.AirId);
        Assert.Equal("air", registry.Air.StringId);
        Assert.False(registry.Air.IsSolid);
        Assert.True(registry.Air.IsTransparent);

        // Verify all 12 blocks exist
        string[] expectedBlocks =
        [
            "air", "grass", "dirt", "stone", "sand", "water",
            "oak_log", "oak_leaves", "planks", "cobblestone", "chest", "coal_ore"
        ];

        for (ushort i = 0; i < expectedBlocks.Length; i++)
        {
            string expected = expectedBlocks[i];
            Assert.True(registry.Contains(expected));
            Assert.True(registry.Contains(i));
            Assert.Equal(i, registry.GetId(expected));
            Assert.Equal(expected, registry.GetStringId(i));

            var def = registry.GetDefinition(i);
            Assert.Equal(expected, def.StringId);
            Assert.Equal(i, def.Id);
        }
    }

    [Fact]
    public void CreateDefault_SpecificBlockPropertiesAreAccurate()
    {
        var registry = BlockRegistry.CreateDefault();

        // Water
        var water = registry.GetDefinition("water");
        Assert.False(water.IsSolid);
        Assert.True(water.IsTransparent);
        Assert.Null(water.DropItem);

        // Stone drops cobblestone
        var stone = registry.GetDefinition("stone");
        Assert.True(stone.IsSolid);
        Assert.False(stone.IsTransparent);
        Assert.Equal("cobblestone", stone.DropItem);

        // Leaves are transparent
        var leaves = registry.GetDefinition("oak_leaves");
        Assert.True(leaves.IsSolid);
        Assert.True(leaves.IsTransparent);

        // Chest is container and indexed
        var chest = registry.GetDefinition("chest");
        Assert.True(chest.IsContainer);
        Assert.True(chest.Container);
        Assert.True(chest.IsIndexed);
        Assert.True(chest.Indexed);

        // Oak log and coal ore are indexed
        var log = registry.GetDefinition("oak_log");
        Assert.True(log.IsIndexed);
        var coal = registry.GetDefinition("coal_ore");
        Assert.True(coal.IsIndexed);

        // Dirt and sand are not indexed or containers
        var dirt = registry.GetDefinition("dirt");
        Assert.False(dirt.IsIndexed);
        Assert.False(dirt.IsContainer);
        Assert.Equal("dirt", dirt.DropItem);
    }

    [Fact]
    public void Lookups_CaseInsensitiveStringIds()
    {
        var registry = BlockRegistry.CreateDefault();

        Assert.Equal(registry.GetId("stone"), registry.GetId("STONE"));
        Assert.Equal(registry.GetId("oak_log"), registry.GetId("Oak_Log"));

        Assert.True(registry.TryGetDefinition("GRASS", out var def));
        Assert.NotNull(def);
        Assert.Equal("grass", def.StringId);
    }

    [Fact]
    public void Lookups_UnknownBlocksThrowOrReturnFalse()
    {
        var registry = BlockRegistry.CreateDefault();

        Assert.False(registry.Contains("bedrock"));
        Assert.False(registry.Contains(999));
        Assert.False(registry.TryGetId("bedrock", out _));
        Assert.False(registry.TryGetStringId(999, out _));
        Assert.False(registry.TryGetDefinition("bedrock", out _));
        Assert.False(registry.TryGetDefinition(999, out _));

        Assert.Throws<KeyNotFoundException>(() => registry.GetId("bedrock"));
        Assert.Throws<KeyNotFoundException>(() => registry.GetStringId(999));
        Assert.Throws<KeyNotFoundException>(() => registry.GetDefinition("bedrock"));
        Assert.Throws<KeyNotFoundException>(() => registry.GetDefinition(999));
    }

    [Fact]
    public void Load_ParsesJsonCorrectly()
    {
        string json = """
        {
          "blocks": [
            {
              "id": "air",
              "numericId": 0,
              "solid": false,
              "transparent": true,
              "hardness": 0.0,
              "drop": null,
              "atlasIndices": { "all": 0 }
            },
            {
              "id": "custom_stone",
              "numericId": 1,
              "solid": true,
              "transparent": false,
              "hardness": 2.5,
              "drop": "custom_cobble",
              "isContainer": false,
              "isIndexed": true,
              "atlasIndices": {
                "top": 10,
                "bottom": 11,
                "sides": 12
              }
            }
          ]
        }
        """;

        var registry = BlockRegistry.Load(json);

        Assert.Equal(2, registry.Count);
        Assert.True(registry.Contains("custom_stone"));
        var def = registry.GetDefinition("custom_stone");
        Assert.Equal(1, def.Id);
        Assert.Equal("custom_cobble", def.DropItem);
        Assert.True(def.IsIndexed);
        Assert.Equal(10, def.AtlasIndices.Top);
        Assert.Equal(11, def.AtlasIndices.Bottom);
        Assert.Equal(12, def.AtlasIndices.North);
        Assert.Equal(12, def.AtlasIndices.South);
        Assert.Equal(12, def.AtlasIndices.East);
        Assert.Equal(12, def.AtlasIndices.West);
    }

    [Fact]
    public void Load_ParsesArrayOfAtlasIndices()
    {
        string json = """
        [
          {
            "id": "air",
            "numericId": 0,
            "solid": false,
            "transparent": true
          },
          {
            "id": "test_block",
            "numericId": 1,
            "solid": true,
            "transparent": false,
            "atlasIndices": [1, 2, 3, 4, 5, 6]
          }
        ]
        """;

        var registry = BlockRegistry.Load(json);
        var def = registry.GetDefinition("test_block");
        Assert.Equal(1, def.AtlasIndices.Top);
        Assert.Equal(2, def.AtlasIndices.Bottom);
        Assert.Equal(3, def.AtlasIndices.North);
        Assert.Equal(4, def.AtlasIndices.South);
        Assert.Equal(5, def.AtlasIndices.East);
        Assert.Equal(6, def.AtlasIndices.West);
    }

    [Fact]
    public void LoadFromFile_LoadsConfigFileSuccessfully()
    {
        string? dir = AppContext.BaseDirectory;
        string? configPath = null;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "config", "blocks.json");
            if (File.Exists(candidate))
            {
                configPath = candidate;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(configPath);
        Assert.True(File.Exists(configPath));

        var registry = BlockRegistry.LoadFromFile(configPath);
        Assert.Equal(12, registry.Count);
        Assert.True(registry.Contains("chest"));
        Assert.True(registry.GetDefinition("chest").IsContainer);
    }

    [Fact]
    public void Validation_MissingAirOrNonZeroAirThrows()
    {
        var noAir = new[]
        {
            new BlockDefinition(0, "stone", true, false, 1.5f, null, false, false, new BlockAtlasIndices(0))
        };
        Assert.Throws<ArgumentException>(() => new BlockRegistry(noAir));

        var airNotZero = new[]
        {
            new BlockDefinition(1, "air", false, true, 0f, null, false, false, new BlockAtlasIndices(0))
        };
        Assert.Throws<ArgumentException>(() => new BlockRegistry(airNotZero));
    }

    [Fact]
    public void Validation_DuplicateIdsThrow()
    {
        var dupString = new[]
        {
            new BlockDefinition(0, "air", false, true, 0f, null, false, false, new BlockAtlasIndices(0)),
            new BlockDefinition(1, "stone", true, false, 1.5f, null, false, false, new BlockAtlasIndices(1)),
            new BlockDefinition(2, "STONE", true, false, 1.5f, null, false, false, new BlockAtlasIndices(1))
        };
        Assert.Throws<ArgumentException>(() => new BlockRegistry(dupString));

        var dupNumeric = new[]
        {
            new BlockDefinition(0, "air", false, true, 0f, null, false, false, new BlockAtlasIndices(0)),
            new BlockDefinition(1, "stone", true, false, 1.5f, null, false, false, new BlockAtlasIndices(1)),
            new BlockDefinition(1, "dirt", true, false, 0.5f, null, false, false, new BlockAtlasIndices(2))
        };
        Assert.Throws<ArgumentException>(() => new BlockRegistry(dupNumeric));
    }
}
