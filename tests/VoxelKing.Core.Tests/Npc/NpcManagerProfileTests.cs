namespace VoxelKing.Core.Tests.Npc;

using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;
using Xunit;

public class NpcManagerProfileTests
{
    [Fact]
    public void SpawnNpc_FromProfile_AssignsAllPropertiesCorrectly()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var profile = new NpcProfile(
            name: "Diana",
            personality: "cautious and vigilant village guard",
            skills: new[] { "guarding", "patrol" },
            appearanceColor: "#9B59B6",
            homeOffset: new Vec3(2f, 0f, 4f),
            startingInventory: new Dictionary<string, int> { { "stone", 15 } }
        );

        var spawnOrigin = new Vec3(10f, 60f, 20f);
        var npc = manager.SpawnNpc(profile, spawnOrigin);

        Assert.NotNull(npc);
        Assert.Equal("Diana", npc.Name);
        Assert.Equal("cautious and vigilant village guard", npc.Personality);
        Assert.Equal("cautious and vigilant village guard", npc.Profile);
        Assert.Equal(new[] { "guarding", "patrol" }, npc.Skills);
        Assert.Equal("#9B59B6", npc.AppearanceColor);
        Assert.Equal(new Vec3(12f, 60f, 24f), npc.Position);
        Assert.Equal(new Vec3(12f, 60f, 24f), npc.Home);

        // Check starting inventory
        Assert.NotNull(npc.Inventory);
        Assert.Equal(15, npc.Inventory.CountItem("stone"));

        // Entity is registered in registry and manager
        Assert.Single(manager.ActiveNpcs);
        Assert.True(registry.TryGet(npc.Id, out var registered));
        Assert.Same(npc, registered);
    }

    [Fact]
    public void SpawnNpcsFromProfiles_SpawnsMultipleNpcsIndependently()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var defaults = NpcProfileLoader.LoadDefaultProfiles();
        Assert.True(defaults.Count >= 5);

        var spawnOrigin = new Vec3(0f, 64f, 0f);
        var npcs = manager.SpawnNpcsFromProfiles(defaults, spawnOrigin);

        Assert.Equal(defaults.Count, npcs.Count);
        Assert.Equal(defaults.Count, manager.ActiveNpcs.Count);

        // Distinct names and positions
        var names = npcs.Select(n => n.Name).ToHashSet();
        Assert.Equal(defaults.Count, names.Count);

        var positions = npcs.Select(n => n.Position).ToHashSet();
        Assert.Equal(defaults.Count, positions.Count);

        // All registered in EntityRegistry
        foreach (var npc in npcs)
        {
            Assert.True(registry.TryGet(npc.Id, out _));
        }
    }

    [Fact]
    public void Profile_Feeds_PromptContext_And_PromptBuilder()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var profile = new NpcProfile(
            name: "Bob",
            personality: "grumpy farmer",
            skills: new[] { "farming", "harvesting" },
            appearanceColor: "#3D7BEB"
        );

        var npc = manager.SpawnNpc(profile, Vec3.Zero);

        var context = NpcPromptContext.FromNpc(npc, "tend the wheat");
        Assert.Equal("Bob", context.Name);
        Assert.Equal("grumpy farmer", context.Personality);
        Assert.Equal(new[] { "farming", "harvesting" }, context.Skills);

        var builder = new PromptBuilder();
        var prompt = builder.BuildPrompt(npc, "tend the wheat");
        Assert.Contains("name=Bob", prompt.UserContent);
        Assert.Contains("personality=\"grumpy farmer\"", prompt.UserContent);
        Assert.Contains("skills=[farming, harvesting]", prompt.UserContent);
    }

    [Fact]
    public void SpawnNpc_FromProfile_LoadsInitialBrain_WhenBrainHostAvailable()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var pathService = new PathfindingService(world);
        var registry = new EntityRegistry();

        var manager = new NpcManager(
            registry,
            voxelWorld: world,
            eventBus: bus,
            pathfindingService: pathService)
        {
            EnableArbiters = true
        };

        var profile = new NpcProfile(
            name: "Alice",
            personality: "woodcutter",
            skills: new[] { "woodcutting" },
            initialBrain: "gather_logs"
        );

        var npc = manager.SpawnNpc(profile, Vec3.Zero);

        Assert.NotNull(npc.BrainHost);
        // Initial brain script should be loaded into BrainHost
        Assert.Equal(BrainStatus.Running, npc.BrainHost.Status);
        Assert.NotNull(npc.CurrentTaskSummary);
        Assert.True(npc.BrainHost.HasActiveTask);
    }
}
