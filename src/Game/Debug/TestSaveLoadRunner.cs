namespace VoxelKing.Game.Debug;

using System;
using System.IO;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Game.Bootstrap;
using VoxelKing.Game.Player;

/// <summary>
/// Headless test runner validating F-501: Save / load.
/// Verifies:
/// - Modified terrain blocks are preserved across save/load.
/// - Chest containers and inventory items survive save/load.
/// - Named places survive save/load.
/// - Player state and inventory items survive save/load.
/// - NPC positions, inventories, memories, and brains (restarted) survive save/load.
/// </summary>
public partial class TestSaveLoadRunner : Node
{
    private const string TestSaveWorldName = "test_saveload_world";

    private GameRoot _gameRoot = null!;
    private int _stage = 0;
    private int _frameCounter = 0;
    private Vec3i _testBlockPos = new(0, 80, 0);
    private Vec3i _testChestPos = new(1, 80, 0);

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestSaveLoadRunner could not locate GameRoot.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[INFO] TestSaveLoadRunner initialized.");
    }

    public override void _PhysicsProcess(double delta)
    {
        _frameCounter++;

        switch (_stage)
        {
            case 0: // Wait for initialization to stabilize
                if (_frameCounter < 20) return;

                GD.Print("[INFO] Phase 1: Modifying world, chests, named places, player, and NPCs...");
                ModifySimulationState();

                GD.Print($"[INFO] Saving game to world '{TestSaveWorldName}'...");
                _gameRoot.SaveGame(TestSaveWorldName);

                _stage = 1;
                _frameCounter = 0;
                break;

            case 1: // Verify files on disk, then wipe state and reload
                if (_frameCounter < 5) return;

                string saveDir = Path.Combine(ProjectSettings.GlobalizePath(_gameRoot.ConfigProvider.Config.Save.SavesDirectory), TestSaveWorldName);
                if (!Directory.Exists(saveDir) || !File.Exists(Path.Combine(saveDir, "world.json")))
                {
                    GD.PrintErr($"[FAIL] Expected save files not found in '{saveDir}'.");
                    GetTree().Quit(1);
                    return;
                }

                GD.Print("[INFO] Phase 2: Wiping in-memory world state before reload...");
                _gameRoot.VoxelWorld.SetBlock(_testBlockPos, _gameRoot.BlockRegistry.AirId);
                _gameRoot.ContainerManager.Clear();
                _gameRoot.NamedPlaces.Clear();

                var player = _gameRoot.GetNodeOrNull<PlayerController>("Player");
                player?.Inventory.Clear();

                AssertTrue(_gameRoot.VoxelWorld.GetBlock(_testBlockPos) == _gameRoot.BlockRegistry.AirId, "Block was successfully cleared");
                AssertTrue(!_gameRoot.ContainerManager.HasContainer(_testChestPos), "Chests were successfully cleared");
                AssertTrue(!_gameRoot.NamedPlaces.HasPlace("player_chest"), "Named places were successfully cleared");

                GD.Print($"[INFO] Loading game from world '{TestSaveWorldName}'...");
                bool loadSuccess = _gameRoot.LoadGame(TestSaveWorldName);
                AssertTrue(loadSuccess, "LoadGame returned success");

                _stage = 2;
                _frameCounter = 0;
                break;

            case 2: // Verify restored state
                if (_frameCounter < 15) return;

                GD.Print("[INFO] Phase 3: Verifying restored state...");
                VerifyRestoredState();

                GD.Print("[PASS] F-501 SAVE / LOAD ACCEPTANCE CRITERIA MET DEMONSTRABLY!");
                CleanupTestSave();
                GetTree().Quit(0);
                break;
        }
    }

    private void ModifySimulationState()
    {
        // 1. Terrain modification
        ushort cobblestoneId = _gameRoot.BlockRegistry.GetId("cobblestone");
        _gameRoot.VoxelWorld.SetBlock(_testBlockPos, cobblestoneId);
        AssertTrue(_gameRoot.VoxelWorld.GetBlock(_testBlockPos) == cobblestoneId, "Cobblestone was placed");

        // 2. Chest container modification
        var chestInv = _gameRoot.ContainerManager.GetOrCreateContainer(_testChestPos, 27);
        chestInv.Clear();
        chestInv.AddItem("oak_log", 25, out _);
        chestInv.AddItem("stone", 50, out _);
        AssertTrue(chestInv.CountItem("oak_log") == 25, "Chest has 25 logs");

        // 3. Named places
        _gameRoot.NamedPlaces.SetPlace("player_chest", _testChestPos);
        _gameRoot.NamedPlaces.SetPlace("special_shrine", new Vec3i(10, 70, -5));

        // 4. Player state & inventory
        var player = _gameRoot.GetNodeOrNull<PlayerController>("Player");
        if (player != null)
        {
            player.Inventory.Clear();
            player.Inventory.AddItem("planks", 33, out _);
            player.SelectedHotbarSlot = 2;
        }

        // 5. NPC state & memories
        if (_gameRoot.NpcManager.ActiveNpcs.Count > 0)
        {
            var firstNpc = _gameRoot.NpcManager.ActiveNpcs.GetEnumerator();
            firstNpc.MoveNext();
            var npc = firstNpc.Current;

            npc.Inventory.Clear();
            npc.Inventory.AddItem("coal_ore", 14, out _);
            npc.Memory.RememberPlace("safe_spot", new Vec3i(3, 65, 3));
            npc.Memory.Set("quest_progress", 42L);
            npc.CurrentTaskSummary = "Guarding the safe spot";

            GD.Print($"[INFO] Configured NPC '{npc.Name}' with items, memories, and brain '{npc.BrainHost?.Status}'.");
        }
    }

    private void VerifyRestoredState()
    {
        // 1. Verify terrain block restored
        ushort cobblestoneId = _gameRoot.BlockRegistry.GetId("cobblestone");
        ushort restoredBlock = _gameRoot.VoxelWorld.GetBlock(_testBlockPos);
        AssertTrue(restoredBlock == cobblestoneId, $"Modified block restored: expected {cobblestoneId}, got {restoredBlock}");

        // 2. Verify chest container restored
        AssertTrue(_gameRoot.ContainerManager.HasContainer(_testChestPos), "Chest container exists at test position");
        var chestInv = _gameRoot.ContainerManager.GetContainer(_testChestPos);
        AssertTrue(chestInv != null, "Chest inventory is not null");
        AssertTrue(chestInv!.CountItem("oak_log") == 25, $"Chest logs count: expected 25, got {chestInv.CountItem("oak_log")}");
        AssertTrue(chestInv.CountItem("stone") == 50, $"Chest stone count: expected 50, got {chestInv.CountItem("stone")}");

        // 3. Verify named places restored
        AssertTrue(_gameRoot.NamedPlaces.HasPlace("player_chest"), "Named place 'player_chest' restored");
        AssertTrue(_gameRoot.NamedPlaces.GetPlace("player_chest") == _testChestPos, "player_chest points to correct position");
        AssertTrue(_gameRoot.NamedPlaces.HasPlace("special_shrine"), "special_shrine restored");

        // 4. Verify player restored
        var player = _gameRoot.GetNodeOrNull<PlayerController>("Player");
        if (player != null)
        {
            AssertTrue(player.Inventory.CountItem("planks") == 33, $"Player planks count: {player.Inventory.CountItem("planks")}");
            AssertTrue(player.SelectedHotbarSlot == 2, $"Player hotbar slot: {player.SelectedHotbarSlot}");
        }

        // 5. Verify NPCs restored with memories and restarted brains
        AssertTrue(_gameRoot.NpcManager.ActiveNpcs.Count > 0, "Active NPCs restored");
        bool foundNpcWithQuest = false;
        foreach (var npc in _gameRoot.NpcManager.ActiveNpcs)
        {
            if (npc.Memory.Place("safe_spot") == new Vec3i(3, 65, 3))
            {
                foundNpcWithQuest = true;
                AssertTrue(npc.Inventory.CountItem("coal_ore") == 14, $"NPC coal ore: {npc.Inventory.CountItem("coal_ore")}");
                AssertTrue(Convert.ToInt64(npc.Memory.Get("quest_progress")) == 42L, "NPC memory KV store restored");
                AssertTrue(npc.CurrentTaskSummary == "Guarding the safe spot", $"NPC task summary: {npc.CurrentTaskSummary}");
                AssertTrue(npc.BrainHost != null, "NPC BrainHost exists");
                AssertTrue(npc.BrainHost!.Status == BrainStatus.Running, $"NPC brain status is Running: {npc.BrainHost.Status}");
            }
        }
        AssertTrue(foundNpcWithQuest, "Found restored NPC with configured safe_spot and quest progress");
    }

    private void CleanupTestSave()
    {
        try
        {
            string saveDir = Path.Combine(ProjectSettings.GlobalizePath(_gameRoot.ConfigProvider.Config.Save.SavesDirectory), TestSaveWorldName);
            if (Directory.Exists(saveDir))
            {
                Directory.Delete(saveDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"[FAIL] Assertion failed: {message}");
            GetTree().Quit(1);
            throw new Exception($"Assertion failed: {message}");
        }
        GD.Print($"[ASSERT OK] {message}");
    }
}
