namespace VoxelKing.Game.Entities;

using System;
using System.Linq;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Player;
using VoxelKing.Game.UI;

/// <summary>
/// Headless automated integration test validating item dropping, ground laying, and walking auto-pickup:
/// 1. Player dropping items (Key Q / DropSelectedItem) creating ItemEntity and visual ItemNode.
/// 2. Dropped items landing flat on top of the floor block (Y = floor + 1.0) without hovering.
/// 3. NPCs dropping items (DropAction) creating ItemEntity and visual ItemNode.
/// 4. Player walking over dropped items and automatically picking them up.
/// 5. Despawning of collected items and cleanup of visual ItemNodes.
/// </summary>
public partial class TestItemsRunner : Node3D
{
    private BlockRegistry _registry = null!;
    private VoxelWorld _world = null!;
    private EventBus _eventBus = null!;
    private EntityRegistry _entityRegistry = null!;
    private NpcManager _npcManager = null!;
    private PlayerController _player = null!;
    private PlayerEntity _playerEntity = null!;
    private ItemViewManager _itemViewManager = null!;
    private HotbarUI _hotbarUI = null!;

    private int _physicsFrame = 0;
    private int _testPhase = 0;
    private ItemEntity? _playerDroppedItem;
    private ItemEntity? _npcDroppedItem;

    public override void _Ready()
    {
        _registry = BlockRegistry.CreateDefault();
        _eventBus = new EventBus();
        _world = new VoxelWorld(_registry, _eventBus);
        _entityRegistry = new EntityRegistry();
        _npcManager = new NpcManager(_entityRegistry, voxelWorld: _world, eventBus: _eventBus);

        // Build flat solid floor at Y = 0 from x: -10..10, z: -10..10
        ushort stoneId = _registry.GetId("stone");
        for (int x = -10; x <= 10; x++)
        {
            for (int z = -10; z <= 10; z++)
            {
                _world.SetBlock(x, 0, z, stoneId);
            }
        }

        // Initialize PlayerController
        _player = new PlayerController
        {
            Name = "Player",
            VoxelWorld = _world,
            BlockRegistry = _registry,
            NpcManager = _npcManager,
            Position = new Vector3(0f, 1.0f, 0f)
        };
        AddChild(_player);
        _player.EnsureComponents();

        _playerEntity = new PlayerEntity(
            _entityRegistry.GenerateNextId(),
            "TestPlayer",
            new Vec3(0f, 1.0f, 0f),
            _player.Inventory
        );
        _entityRegistry.Register(_playerEntity);
        _player.PlayerEntity = _playerEntity;

        // Initialize HotbarUI
        _hotbarUI = new HotbarUI { Name = "HotbarUI" };
        AddChild(_hotbarUI);
        _hotbarUI.BindPlayer(_player, _eventBus);
        _player.HotbarUI = _hotbarUI;

        // Initialize ItemViewManager
        _itemViewManager = new ItemViewManager { Name = "ItemViewManager" };
        AddChild(_itemViewManager);
        _itemViewManager.Initialize(_eventBus, _npcManager, _registry);

        GD.Print("[TEST INIT] TestItemsRunner initialized successfully.");
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsFrame++;
        float dt = (float)delta;

        // Synchronize player entity position and tick core physics
        _playerEntity.Position = new Vec3(_player.GlobalPosition.X, _player.GlobalPosition.Y, _player.GlobalPosition.Z);
        _entityRegistry.UpdateSpatial(_playerEntity);

        _npcManager.PhysicsTick(dt);

        switch (_testPhase)
        {
            case 0:
                // Verify initial starting inventory
                AssertTrue(_player.Inventory.CountItem("dirt") == 20, "Player starts with 20 dirt");
                _player.SelectedHotbarSlot = 0; // slot 0 has dirt

                // Phase 1: Player drops 1 dirt
                bool dropped = _player.DropSelectedItem(dropAll: false);
                AssertTrue(dropped, "Player successfully dropped selected item");
                AssertTrue(_player.Inventory.CountItem("dirt") == 19, "Player dirt count decreased to 19");

                // Verify ItemEntity spawned
                AssertTrue(_npcManager.ActiveItems.Count == 1, "NpcManager has 1 active item entity");
                _playerDroppedItem = _npcManager.ActiveItems.First();
                AssertTrue(_playerDroppedItem.Stack.ItemId == "dirt", "Dropped item is dirt");
                AssertTrue(_playerDroppedItem.Stack.Count == 1, "Dropped item stack count is 1");

                // Verify visual ItemNode created in ItemViewManager
                var itemView = _itemViewManager.GetView(_playerDroppedItem.Id);
                AssertTrue(itemView != null, "ItemViewManager created an ItemNode view");
                AssertTrue(itemView!.MeshInstance != null, "ItemNode has a MeshInstance3D");
                AssertTrue(itemView!.MeshInstance!.Mesh != null, "ItemNode has a valid mesh");

                // Move player away so player is not within pickup radius until ready to walk over it
                _player.GlobalPosition = new Vector3(0f, 1.0f, 5.0f);
                _playerEntity.Position = new Vec3(0f, 1.0f, 5.0f);
                _entityRegistry.UpdateSpatial(_playerEntity);

                _testPhase = 1;
                break;

            case 1:
                // Let item physics settle onto the floor
                if (_physicsFrame < 40) return;

                AssertTrue(_playerDroppedItem != null, "Player dropped item exists");
                // The item should land flat on top of the solid stone block at Y = 0 (surface at Y = 1.0f)
                AssertTrue(Mathf.Abs(_playerDroppedItem!.Position.Y - 1.0f) < 0.05f,
                    $"Item landed flat on the floor at Y = 1.0f (actual: {_playerDroppedItem.Position.Y})");

                var nodeView = _itemViewManager.GetView(_playerDroppedItem.Id);
                AssertTrue(nodeView != null, "ItemNode exists for dropped dirt");

                GD.Print($"[PASS] Phase 2: Dropped item landed flat on floor at Y = {_playerDroppedItem.Position.Y:F2}!");
                _testPhase = 2;
                break;

            case 2:
                // Phase 3: NPC drops cobblestone
                var npc = _npcManager.SpawnNpc("TestNpc", new Vec3(5.0f, 1.0f, 0f), inventory: new Inventory(36));
                npc.Inventory.AddItem("cobblestone", 5, out _);
                AssertTrue(npc.Inventory.CountItem("cobblestone") == 5, "NPC has 5 cobblestone");

                var dropAction = new DropAction(npc, "cobblestone", 3, _entityRegistry, _npcManager);
                dropAction.Start();
                AssertTrue(dropAction.Status.IsSucceeded, "NPC drop action succeeded");
                AssertTrue(npc.Inventory.CountItem("cobblestone") == 2, "NPC inventory reduced to 2 cobblestone");

                AssertTrue(_npcManager.ActiveItems.Count == 2, "NpcManager now has 2 active item entities");
                _npcDroppedItem = _npcManager.ActiveItems.FirstOrDefault(i => i.Stack.ItemId == "cobblestone");
                AssertTrue(_npcDroppedItem != null, "NPC dropped cobblestone item exists");

                var npcItemView = _itemViewManager.GetView(_npcDroppedItem!.Id);
                AssertTrue(npcItemView != null, "ItemViewManager created view for NPC dropped item");

                // Despawn NPC so it doesn't auto-pickup the cobblestone before the player arrives
                _npcManager.DespawnNpc(npc.Id);

                GD.Print("[PASS] Phase 3: NPC dropped 3 cobblestone; ItemNode created!");
                _testPhase = 3;
                break;

            case 3:
                // Let NPC dropped item settle onto floor and wait for pickup cooldowns to expire
                if (!_npcDroppedItem!.CanPickup || !_playerDroppedItem!.CanPickup) return;

                AssertTrue(_npcDroppedItem.CanPickup, "NPC dropped item pickup cooldown has expired");
                AssertTrue(_playerDroppedItem.CanPickup, "Player dropped item pickup cooldown has expired");

                // Phase 4: Player walks over dropped dirt to pick it up
                _player.GlobalPosition = new Vector3(_playerDroppedItem.Position.X, 1.0f, _playerDroppedItem.Position.Z);
                _playerEntity.Position = new Vec3(_player.GlobalPosition.X, _player.GlobalPosition.Y, _player.GlobalPosition.Z);
                _entityRegistry.UpdateSpatial(_playerEntity);

                // Run pickup check
                _npcManager.PhysicsTick(dt);

                AssertTrue(_player.Inventory.CountItem("dirt") == 20, "Player walked over dirt and picked it up back to 20");
                AssertTrue(_playerDroppedItem.IsCollected, "Dirt item marked collected");
                AssertTrue(_itemViewManager.GetView(_playerDroppedItem.Id) == null, "Dirt ItemNode was cleaned up upon pickup");

                GD.Print("[PASS] Phase 4: Player walked over dropped dirt and automatically picked it up!");
                _testPhase = 4;
                break;

            case 4:
                // Phase 5: Player walks over dropped cobblestone
                _player.GlobalPosition = new Vector3(_npcDroppedItem!.Position.X, 1.0f, _npcDroppedItem.Position.Z);
                _playerEntity.Position = new Vec3(_player.GlobalPosition.X, _player.GlobalPosition.Y, _player.GlobalPosition.Z);
                _entityRegistry.UpdateSpatial(_playerEntity);

                int cobbleBefore = _player.Inventory.CountItem("cobblestone");
                _npcManager.PhysicsTick(dt);

                AssertTrue(_player.Inventory.CountItem("cobblestone") == cobbleBefore + 3,
                    "Player walked over cobblestone and picked up 3 cobblestone");
                AssertTrue(_npcDroppedItem.IsCollected, "Cobblestone marked collected");
                AssertTrue(_itemViewManager.GetView(_npcDroppedItem.Id) == null, "Cobblestone ItemNode cleaned up");

                GD.Print("[PASS] Phase 5: Player walked over NPC dropped cobblestone and picked it up!");
                _testPhase = 5;
                break;

            case 5:
                GD.Print("[PASS] ALL ITEM DROPPING, FLOOR LAYING, AND AUTO-PICKUP TESTS PASSED SUCCESSFULLY!");
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
}
