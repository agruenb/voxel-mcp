namespace VoxelKing.Core.Tests.Persistence;

using System;
using System.IO;
using System.Linq;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Persistence;
using VoxelKing.Core.Voxel;
using Xunit;

public sealed class SaveManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();
    private readonly EventBus _eventBus = new();

    public SaveManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VoxelKing_SaveTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    [Fact]
    public void WorldSaveData_RoundTrip_PreservesAllFields()
    {
        var data = new WorldSaveData
        {
            Version = 1,
            WorldName = "test_world",
            Seed = 9999,
            WorldTime = 123.45,
            SavedAtUtc = DateTime.UtcNow,
            NamedPlaces = { ["player_chest"] = new Vec3i(10, 64, -5), ["town_square"] = new Vec3i(0, 65, 0) },
            Containers =
            {
                new ContainerSaveDto
                {
                    Position = new Vec3i(10, 64, -5),
                    SlotCount = 27,
                    Slots =
                    {
                        new InventorySlotDto { Slot = 0, ItemId = "stone", Count = 64, MaxStack = 64 },
                        new InventorySlotDto { Slot = 5, ItemId = "oak_log", Count = 12, MaxStack = 64 }
                    }
                }
            }
        };

        string json = data.ToJson();
        var loaded = WorldSaveData.FromJson(json);

        Assert.Equal(data.Version, loaded.Version);
        Assert.Equal(data.WorldName, loaded.WorldName);
        Assert.Equal(data.Seed, loaded.Seed);
        Assert.Equal(data.WorldTime, loaded.WorldTime);
        Assert.Equal(2, loaded.NamedPlaces.Count);
        Assert.Equal(new Vec3i(10, 64, -5), loaded.NamedPlaces["player_chest"]);
        Assert.Single(loaded.Containers);
        Assert.Equal(2, loaded.Containers[0].Slots.Count);
        Assert.Equal("stone", loaded.Containers[0].Slots[0].ItemId);
        Assert.Equal(64, loaded.Containers[0].Slots[0].Count);
    }

    [Fact]
    public void PlayerSaveData_RoundTrip_PreservesAllFields()
    {
        var player = new PlayerSaveData
        {
            Version = 1,
            Position = new Vec3(12.5f, 66f, -3.2f),
            Yaw = 1.2f,
            Pitch = -0.3f,
            IsFlying = true,
            SelectedHotbarSlot = 3,
            Inventory =
            {
                new InventorySlotDto { Slot = 3, ItemId = "cobblestone", Count = 40, MaxStack = 64 }
            }
        };

        string json = player.ToJson();
        var loaded = PlayerSaveData.FromJson(json);

        Assert.Equal(player.Position, loaded.Position);
        Assert.Equal(player.Yaw, loaded.Yaw);
        Assert.Equal(player.Pitch, loaded.Pitch);
        Assert.True(loaded.IsFlying);
        Assert.Equal(3, loaded.SelectedHotbarSlot);
        Assert.Single(loaded.Inventory);
        Assert.Equal("cobblestone", loaded.Inventory[0].ItemId);
    }

    [Fact]
    public void SaveManager_OnlySavesModifiedChunks()
    {
        var saveManager = new SaveManager();
        var world = new VoxelWorld(_registry, _eventBus);

        // Chunk A: untouched (IsModified = false)
        var chunkA = world.GetOrCreateChunk(new Vec3i(0, 0, 0));
        chunkA.IsModified = false;

        // Chunk B: modified via SetBlock
        world.SetBlock(new Vec3i(40, 10, 40), _registry.GetId("stone"));

        var worldData = new WorldSaveData { WorldName = "chunk_test", Seed = 1234 };

        saveManager.SaveWorld(_tempDir, worldData, world.ModifiedChunks, Array.Empty<NpcState>(), null);

        string regionsDir = Path.Combine(_tempDir, "regions");
        var regionFiles = Directory.GetFiles(regionsDir, "*.bin");

        // Only Chunk B should be saved
        Assert.Single(regionFiles);
        Assert.Contains("chunk_1_0_1.bin", regionFiles[0]);

        var loadedChunks = saveManager.LoadModifiedChunks(_tempDir, _registry.IsIndexed);
        Assert.Single(loadedChunks);
        Assert.Equal(new Vec3i(1, 0, 1), loadedChunks[0].ChunkCoord);
        Assert.True(loadedChunks[0].IsModified);
    }

    [Fact]
    public void FullWorldSimulation_SaveAndLoadRoundtrip_RestoresAllStateAndRestartsBrains()
    {
        var saveManager = new SaveManager();

        // 1. Setup source world and managers
        var world = new VoxelWorld(_registry, _eventBus);
        var containerManager = new ContainerManager();
        var namedPlaces = new NamedPlaces();
        var entityRegistry = new EntityRegistry();
        var npcConfig = new NpcConfig();
        var brainLimits = new BrainLimitsConfig();
        var npcManager = new NpcManager(
            entityRegistry,
            npcConfig,
            world,
            _eventBus,
            logger: null,
            brainLimitsConfig: brainLimits)
        {
            ContainerManager = containerManager,
            BlockRegistry = _registry
        };

        // 2. Modify terrain
        var blockPos = new Vec3i(15, 64, 15);
        world.SetBlock(blockPos, _registry.GetId("stone"));
        Assert.Equal(_registry.GetId("stone"), world.GetBlock(blockPos));

        // 3. Setup chest with items
        var chestPos = new Vec3i(16, 64, 15);
        var chestInv = containerManager.GetOrCreateContainer(chestPos, 27);
        chestInv.AddItem("oak_log", 20, out _);
        chestInv.AddItem("planks", 40, out _);

        // 4. Setup named places
        namedPlaces.SetPlace("player_chest", chestPos);

        // 5. Spawn and configure NPC with memory and Lua brain
        var npcPos = new Vec3(10f, 65f, 10f);
        var profile = new NpcProfile
        {
            Name = "Lumberjack",
            Personality = "hardworking",
            Skills = new[] { "chopping", "woodcraft" },
            AppearanceColor = "#3399FF"
        };
        var npc = npcManager.SpawnNpc(profile, npcPos);
        npc.Inventory.Clear();
        npc.Inventory.AddItem("stone", 15, out _);

        // Add memories
        npc.Memory.RememberPlace("my_tree", new Vec3i(25, 65, 30));
        npc.Memory.Set("trees_chopped", 7L);
        npc.Memory.RecordInstruction("Player", "Chop down trees");
        npc.CurrentTaskSummary = "Chopping trees in forest";
        npc.TaskStartTime = 42.0;

        // Assign Lua brain script
        string luaScript = @"
-- instruction: Chop down trees
local chopped = memory.get('trees_chopped') or 0
function main()
    log('Restarted lumberjack with ' .. tostring(chopped) .. ' trees chopped.')
    while true do
        npc.wait(1.0)
    end
end
";
        Assert.NotNull(npc.BrainHost);
        bool scriptLoaded = npc.BrainHost.LoadScript(luaScript);
        Assert.True(scriptLoaded, $"Failed to load initial brain: {npc.BrainHost.LastError}");
        Assert.Equal(BrainStatus.Running, npc.BrainHost.Status);

        // 6. Setup player state
        var playerSave = new PlayerSaveData
        {
            Version = 1,
            Position = new Vec3(5f, 65f, 5f),
            Yaw = 0.5f,
            Pitch = 0.1f,
            IsFlying = false,
            SelectedHotbarSlot = 1,
            Inventory =
            {
                new InventorySlotDto { Slot = 0, ItemId = "dirt", Count = 32, MaxStack = 64 },
                new InventorySlotDto { Slot = 1, ItemId = "cobblestone", Count = 64, MaxStack = 64 }
            }
        };

        // 7. Save world
        var worldData = new WorldSaveData
        {
            Version = 1,
            WorldName = "integration_world",
            Seed = 1337,
            WorldTime = 100.0,
            SavedAtUtc = DateTime.UtcNow,
            NamedPlaces = SaveManager.CreateNamedPlacesDto(namedPlaces),
            Containers = SaveManager.CreateContainerDtos(containerManager)
        };

        saveManager.SaveWorld(_tempDir, worldData, world.ModifiedChunks, npcManager.ActiveNpcs, playerSave);

        Assert.True(saveManager.HasSave(_tempDir));

        // 8. Create fresh target world and restore everything
        var restoredWorld = new VoxelWorld(_registry, _eventBus);
        var restoredContainerManager = new ContainerManager();
        var restoredNamedPlaces = new NamedPlaces();
        var restoredEntityRegistry = new EntityRegistry();
        var restoredNpcManager = new NpcManager(
            restoredEntityRegistry,
            npcConfig,
            restoredWorld,
            _eventBus,
            logger: null,
            brainLimitsConfig: brainLimits)
        {
            ContainerManager = restoredContainerManager,
            BlockRegistry = _registry
        };

        // Load metadata
        var loadedMeta = saveManager.LoadWorldMetadata(_tempDir);
        Assert.NotNull(loadedMeta);
        Assert.Equal(1337, loadedMeta.Seed);
        Assert.Equal(100.0, loadedMeta.WorldTime);
        SaveManager.RestoreNamedPlaces(restoredNamedPlaces, loadedMeta.NamedPlaces);
        SaveManager.RestoreContainers(restoredContainerManager, loadedMeta.Containers);

        // Load modified chunks
        var loadedModifiedChunks = saveManager.LoadModifiedChunks(_tempDir, _registry.IsIndexed);
        foreach (var chunk in loadedModifiedChunks)
        {
            restoredWorld.AddOrUpdateChunk(chunk);
        }

        // Load NPCs
        var loadedNpcs = saveManager.LoadNpcs(_tempDir);
        Assert.NotNull(loadedNpcs);
        Assert.Single(loadedNpcs.Npcs);
        foreach (var npcDto in loadedNpcs.Npcs)
        {
            var restoredNpc = SaveManager.CreateNpcState(npcDto, brainLimits);
            restoredNpcManager.RestoreNpc(restoredNpc, npcDto.BrainSource);
        }

        // Load Player
        var loadedPlayer = saveManager.LoadPlayer(_tempDir);
        Assert.NotNull(loadedPlayer);

        // 9. Verify restored world state
        Assert.Equal(_registry.GetId("stone"), restoredWorld.GetBlock(blockPos));
        Assert.True(restoredNamedPlaces.HasPlace("player_chest"));
        Assert.Equal(chestPos, restoredNamedPlaces.GetPlace("player_chest"));

        Assert.True(restoredContainerManager.HasContainer(chestPos));
        var restoredChest = restoredContainerManager.GetContainer(chestPos);
        Assert.NotNull(restoredChest);
        Assert.Equal(20, restoredChest.CountItem("oak_log"));
        Assert.Equal(40, restoredChest.CountItem("planks"));

        // 10. Verify restored NPC state
        var restoredNpcInstance = restoredNpcManager.GetNpc(npc.Id);
        Assert.NotNull(restoredNpcInstance);
        Assert.Equal("Lumberjack", restoredNpcInstance.Name);
        Assert.Equal("hardworking", restoredNpcInstance.Personality);
        Assert.Equal(new Vec3(10f, 65f, 10f), restoredNpcInstance.Position);
        Assert.Equal(15, restoredNpcInstance.Inventory.CountItem("stone"));

        // Verify memory survived
        Assert.Equal(new Vec3i(25, 65, 30), restoredNpcInstance.Memory.Place("my_tree"));
        Assert.Equal(7L, Convert.ToInt64(restoredNpcInstance.Memory.Get("trees_chopped")));
        Assert.Equal("Chopping trees in forest", restoredNpcInstance.CurrentTaskSummary);
        Assert.Equal(42.0, restoredNpcInstance.TaskStartTime);

        // Verify brain restarted and is running!
        Assert.NotNull(restoredNpcInstance.BrainHost);
        Assert.Equal(BrainStatus.Running, restoredNpcInstance.BrainHost.Status);
        Assert.Equal(luaScript.Trim(), restoredNpcInstance.BrainHost.CurrentScript?.Trim());

        // 11. Verify player state
        Assert.Equal(new Vec3(5f, 65f, 5f), loadedPlayer.Position);
        Assert.Equal(0.5f, loadedPlayer.Yaw);
        Assert.Equal(0.1f, loadedPlayer.Pitch);
        Assert.False(loadedPlayer.IsFlying);
        Assert.Equal(1, loadedPlayer.SelectedHotbarSlot);
        Assert.Equal(2, loadedPlayer.Inventory.Count);
    }
}
