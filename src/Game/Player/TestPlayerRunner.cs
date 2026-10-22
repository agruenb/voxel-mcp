namespace VoxelKing.Game.Player;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.UI;
using VoxelKing.Game.World;

/// <summary>
/// Headless automated integration test validating the first-person player controller:
/// surface spawning, WASD movement, smooth 1-block step climbing, 2-block wall blocking,
/// jumping, fly mode toggle, mouse capture toggling, DDA voxel raycast targeting,
/// block breaking with hardness, item drop into inventory, block placement,
/// container opening, chest item storage, and "player_chest" place marking.
/// </summary>
public partial class TestPlayerRunner : Node3D
{
    private ChunkNode _chunkNode = null!;
    private PlayerController _player = null!;
    private ChunkMesher _mesher = null!;
    private BlockRegistry _registry = null!;
    private ChunkMaterials _materials = null!;
    private ChunkData _chunkData = null!;
    private VoxelWorld _world = null!;
    private HotbarUI _hotbarUI = null!;
    private ChestUI _chestUI = null!;

    private int _physicsFrame = 0;
    private int _testPhase = 0;
    private float _initialJumpY = 0f;
    private int _cobblestoneBeforeMine = 0;
    private Vec3i _minedBlockPos;
    private Vec3i _placedBlockPos;
    private Vec3i _placedChestPos;

    public override void _Ready()
    {
        _registry = BlockRegistry.CreateDefault();
        _mesher = new ChunkMesher(_registry);
        _materials = new ChunkMaterials();

        var eventBus = new EventBus();
        _world = new VoxelWorld(_registry, eventBus);

        // Build test terrain in chunk (0, 0, 0):
        // 1. Base floor at y=0 from x: 0..25, z: 0..10
        _chunkData = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        for (int x = 0; x <= 25; x++)
        {
            for (int z = 0; z <= 10; z++)
            {
                _chunkData.SetBlock(x, 0, z, stoneId);
            }
        }

        // 2. 1-block elevated step at x: 6..25, y: 1, z: 0..10 (height 1.0m step)
        for (int x = 6; x <= 25; x++)
        {
            for (int z = 0; z <= 10; z++)
            {
                _chunkData.SetBlock(x, 1, z, stoneId);
            }
        }

        // 3. 2-block high wall at x: 16, y: 2..3, z: 0..10 (height 2.0m wall from step surface)
        for (int y = 2; y <= 3; y++)
        {
            for (int z = 0; z <= 10; z++)
            {
                _chunkData.SetBlock(16, y, z, stoneId);
            }
        }

        _world.AddOrUpdateChunk(_chunkData);

        // Mesh and add chunk
        _chunkNode = new ChunkNode(Vec3i.Zero);
        AddChild(_chunkNode);
        RebuildMesh();

        // Create and wire player controller
        _player = new PlayerController { Name = "TestPlayer" };
        _player.FreezeUntilGrounded = false;
        _player.Position = new Vector3(2.0f, 1.05f, 5.0f); // just above base floor at y=0 (surface top = 1.0)
        _player.VoxelWorld = _world;
        _player.BlockRegistry = _registry;
        _player.ContainerManager = new ContainerManager();
        _player.NamedPlaces = new NamedPlaces();

        _hotbarUI = new HotbarUI { Name = "HotbarUI" };
        AddChild(_hotbarUI);
        _hotbarUI.BindPlayer(_player);
        _player.HotbarUI = _hotbarUI;

        _chestUI = new ChestUI { Name = "ChestUI" };
        AddChild(_chestUI);
        _chestUI.Setup(_player, _player.NamedPlaces, _hotbarUI);
        _player.ChestUI = _chestUI;

        AddChild(_player);

        GD.Print("[TestPlayer] Starting PlayerController physics and interaction tests...");
    }

    private void RebuildMesh()
    {
        var snapshot = new ChunkMeshSnapshot(_chunkData);
        var meshData = _mesher.Mesh(snapshot);
        _chunkNode.ApplyMesh(meshData, _materials.Opaque, _materials.Transparent);
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsFrame++;

        switch (_testPhase)
        {
            case 0:
                // Phase 0: Settle on base floor (y=0 top is at Y=1.0)
                if (_player.IsOnFloor())
                {
                    AssertTrue(_player.Position.Y >= 0.95f && _player.Position.Y <= 1.15f,
                        $"Player should be grounded on base floor near Y=1.0, but is at {_player.Position.Y:F2}");
                    GD.Print($"[PASS] Phase 0: Grounded on base floor at Y={_player.Position.Y:F2}.");

                    _testPhase = 1;
                    _physicsFrame = 0;

                    // Orient player facing +X direction (Rotation.Y = -PI/2)
                    _player.Rotation = new Vector3(0, -Mathf.Pi * 0.5f, 0);
                    // Walk forward in facing direction (+X)
                    _player.SimulatedMoveInput = new Vector2(0, 1.0f);
                }
                else if (_physicsFrame > 60)
                {
                    Fail($"Player failed to land on floor within 60 frames. Pos: {_player.Position}");
                }
                break;

            case 1:
                // Phase 1: Walk forward towards the 1-block step at X=6
                if (_player.Position.X >= 5.5f)
                {
                    GD.Print($"[PASS] Phase 1: Approached step at X={_player.Position.X:F2}. Entering step climb test.");
                    _testPhase = 2;
                    _physicsFrame = 0;
                }
                else if (_physicsFrame > 120)
                {
                    Fail($"Player failed to reach step at X=5.5 within 120 frames. Pos: {_player.Position}");
                }
                break;

            case 2:
                // Phase 2: Climb the 1-block step onto Y=2.0 (elevated floor top is at Y=2.0)
                if (_player.Position.X > 6.5f && _player.Position.Y >= 1.9f)
                {
                    AssertTrue(_player.IsOnFloor(), "Player should be on floor on top of step");
                    GD.Print($"[PASS] Phase 2: Successfully climbed 1-block step! Pos=({_player.Position.X:F2}, {_player.Position.Y:F2}).");
                    _testPhase = 3;
                    _physicsFrame = 0;
                }
                else if (_physicsFrame > 120)
                {
                    Fail($"Player failed to climb 1-block step within 120 frames. Pos: {_player.Position}");
                }
                break;

            case 3:
                // Phase 3: Walk towards 2-block wall at X=16 and verify barrier collision (must NOT step up)
                if (_player.IsOnWall())
                {
                    AssertTrue(_player.Position.X < 16.0f, $"Player must be stopped before X=16, at {_player.Position.X:F2}");
                    AssertTrue(_player.Position.Y < 2.5f, $"Player must not step onto 2-block high wall, Y={_player.Position.Y:F2}");
                    GD.Print($"[PASS] Phase 3: 2-block high wall blocked player correctly at X={_player.Position.X:F2}, Y={_player.Position.Y:F2}.");

                    _testPhase = 4;
                    _physicsFrame = 0;
                    _player.SimulatedMoveInput = Vector2.Zero;
                }
                else if (_physicsFrame > 180)
                {
                    Fail($"Player failed to collide with 2-block wall within 180 frames. Pos: {_player.Position}");
                }
                break;

            case 4:
                // Phase 4: Test jump
                if (_physicsFrame == 1)
                {
                    _initialJumpY = _player.Position.Y;
                    _player.SimulatedJump = true;
                }
                else if (_physicsFrame >= 5)
                {
                    _player.SimulatedJump = false;
                    AssertTrue(_player.Position.Y > _initialJumpY + 0.3f,
                        $"Player should have jumped above {_initialJumpY + 0.3f}, current Y={_player.Position.Y:F2}");
                    GD.Print($"[PASS] Phase 4: Jump verified! Jumped from {_initialJumpY:F2} to {_player.Position.Y:F2}.");

                    _testPhase = 5;
                    _physicsFrame = 0;
                }
                break;

            case 5:
                // Phase 5: Test fly toggle
                if (_physicsFrame == 1)
                {
                    _player.ToggleFly();
                    AssertTrue(_player.IsFlying, "Player should be flying after ToggleFly()");
                    _initialJumpY = _player.Position.Y;
                    _player.SimulatedJump = true;
                }
                else if (_physicsFrame >= 15)
                {
                    _player.SimulatedJump = false;
                    AssertTrue(_player.Position.Y > _initialJumpY + 1.0f,
                        $"Player should have flown upward, Y={_player.Position.Y:F2} vs start {_initialJumpY:F2}");
                    _player.ToggleFly();
                    AssertTrue(!_player.IsFlying, "Player should exit fly mode after second ToggleFly()");
                    GD.Print($"[PASS] Phase 5: Fly mode toggle verified! Flew from {_initialJumpY:F2} to {_player.Position.Y:F2}.");

                    _testPhase = 6;
                    _physicsFrame = 0;
                }
                break;

            case 6:
                // Phase 6: Mouse capture toggle
                _player.CaptureMouse();
                AssertTrue(_player.IsMouseCaptured, "IsMouseCaptured should be true");
                _player.ToggleMouseCapture();
                AssertTrue(!_player.IsMouseCaptured, "IsMouseCaptured should be false after toggle");
                _player.ToggleMouseCapture();
                AssertTrue(_player.IsMouseCaptured, "IsMouseCaptured should be true after toggle");
                GD.Print("[PASS] Phase 6: Mouse capture toggling verified.");

                // Settle player on ground at X=14.0, Y=2.0, Z=5.0 facing wall at X=16
                _player.Position = new Vector3(14.0f, 2.05f, 5.0f);
                _player.Velocity = Vector3.Zero;

                _testPhase = 7;
                _physicsFrame = 0;
                break;

            case 7:
                // Phase 7: Raycast targeting verification
                // Player stands at X=14.0 facing +X at wall at X=16.
                _player.Rotation = new Vector3(0, -Mathf.Pi * 0.5f, 0); // Face +X
                _player.Head.Rotation = Vector3.Zero; // Look straight ahead

                // Call _Process on player to update raycast
                _player._Process(0.016);

                AssertTrue(_player.CurrentRaycast.IsHit, "Player raycast should hit the wall in front of them");
                AssertTrue(_player.CurrentRaycast.BlockPos.X == 16,
                    $"Raycast should hit wall at X=16, hit {_player.CurrentRaycast.BlockPos}");
                AssertTrue(_player.HighlightNode.Visible, "Highlight node should be visible when targeting block");

                GD.Print($"[PASS] Phase 7: Raycast targeted block at {_player.CurrentRaycast.BlockPos}.");
                _testPhase = 8;
                _physicsFrame = 0;
                _minedBlockPos = _player.CurrentRaycast.BlockPos;
                _cobblestoneBeforeMine = _player.Inventory.CountItem("cobblestone");
                _player.TriggerMiningAction(true);
                break;

            case 8:
                // Phase 8: Block breaking with hardness
                // Stone hardness is 1.5s (90 ticks). Mining action is held.
                _player._Process(0.0166);

                if (_world.GetBlock(_minedBlockPos) == _registry.AirId)
                {
                    _player.TriggerMiningAction(false);
                    int cobblestoneAfter = _player.Inventory.CountItem("cobblestone");
                    AssertTrue(cobblestoneAfter > _cobblestoneBeforeMine,
                        "Mining stone should drop cobblestone into player inventory");
                    GD.Print($"[PASS] Phase 8: Successfully broke stone block with hardness! Cobblestone in inventory: {cobblestoneAfter}.");

                    _testPhase = 9;
                    _physicsFrame = 0;
                }
                else if (_physicsFrame > 150)
                {
                    Fail($"Mining block at {_minedBlockPos} timed out after 150 frames. Progress: {_player.BreakProgress:F2}");
                }
                break;

            case 9:
                // Phase 9: Block placement (RMB place)
                // Look down slightly towards the floor at feet in front
                _player.Head.Rotation = new Vector3(-Mathf.DegToRad(30f), 0, 0);
                _player._Process(0.0166);

                AssertTrue(_player.CurrentRaycast.IsHit, "Should hit floor in front");
                _placedBlockPos = _player.CurrentRaycast.AdjacentPos;

                int dirtBefore = _player.Inventory.CountItem("dirt");
                AssertTrue(dirtBefore > 0, "Player should have dirt in inventory");

                _player.SelectedHotbarSlot = 0; // Dirt slot
                bool placed = _player.TryPlaceOrInteract();
                AssertTrue(placed, $"Failed to place dirt block at {_placedBlockPos}");

                ushort placedBlockId = _world.GetBlock(_placedBlockPos);
                AssertTrue(placedBlockId == _registry.GetId("dirt"),
                    $"VoxelWorld should contain dirt at {_placedBlockPos}, found {placedBlockId}");

                int dirtAfter = _player.Inventory.CountItem("dirt");
                AssertTrue(dirtAfter == dirtBefore - 1,
                    $"Dirt count should decrement by 1 (was {dirtBefore}, now {dirtAfter})");

                GD.Print($"[PASS] Phase 9: Successfully placed dirt block at {_placedBlockPos}.");
                _testPhase = 10;
                _physicsFrame = 0;
                break;

            case 10:
                // Phase 10: Chest placement, opening, item storage, and marking player_chest
                _player.SelectedHotbarSlot = 4; // Chest slot
                _player._Process(0.0166);

                _placedChestPos = _player.CurrentRaycast.AdjacentPos;
                bool chestPlaced = _player.TryPlaceOrInteract();
                AssertTrue(chestPlaced, $"Failed to place chest at {_placedChestPos}");
                AssertTrue(_world.GetBlock(_placedChestPos) == _registry.GetId("chest"), "Block should be chest");
                AssertTrue(_player.ContainerManager != null && _player.ContainerManager.HasContainer(_placedChestPos),
                    "ContainerManager should track the placed chest container");

                // Target the placed chest
                _player._Process(0.0166);
                AssertTrue(_player.CurrentRaycast.IsHit && _player.CurrentRaycast.BlockPos == _placedChestPos,
                    $"Raycast should now target chest at {_placedChestPos}");

                // Interact with chest -> Opens ChestUI
                bool chestOpened = _player.TryPlaceOrInteract();
                AssertTrue(chestOpened, "Interacting with chest should succeed");
                AssertTrue(_player.ChestUI != null && _player.ChestUI.IsOpen, "ChestUI should be open");

                // Verify chest container storage
                var chestInv = _player.ContainerManager!.GetOrCreateContainer(_placedChestPos);
                int transferred = Inventory.Transfer(_player.Inventory, 0, chestInv, 5);
                AssertTrue(transferred == 5, $"Should transfer 5 dirt into chest, transferred {transferred}");
                AssertTrue(chestInv.CountItem("dirt") == 5, "Chest container should now store 5 dirt");

                // Mark chest as player_chest
                _player.NamedPlaces!.SetPlace("player_chest", _placedChestPos);
                AssertTrue(_player.NamedPlaces!.GetPlace("player_chest") == _placedChestPos,
                    "NamedPlaces should store player_chest location");

                // Close chest UI
                _player.ChestUI!.CloseChest();
                AssertTrue(!_player.ChestUI.IsOpen, "ChestUI should be closed");

                GD.Print($"[PASS] Phase 10: Chest placed, opened, stored 5 items, and marked as player_chest at {_placedChestPos}!");
                _testPhase = 11;
                break;

            case 11:
                GD.Print("[PASS] ALL PLAYER CONTROLLER & INTERACTION INTEGRATION TESTS PASSED SUCCESSFULLY!");
                GetTree().Quit(0);
                break;
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"Assertion Failed: {message}");
            GetTree().Quit(1);
            throw new InvalidOperationException(message);
        }
    }

    private void Fail(string message)
    {
        GD.PrintErr($"[FAIL] {message}");
        GetTree().Quit(1);
    }
}
