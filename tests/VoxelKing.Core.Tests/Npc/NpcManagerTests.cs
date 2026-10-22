namespace VoxelKing.Core.Tests.Npc;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using Xunit;

public class NpcManagerTests
{
    [Fact]
    public void SpawnNpc_Registers_In_EntityRegistry_And_ActiveNpcs()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var npc = manager.SpawnNpc("Miner", new Vec3(10f, 64f, 10f), "Mines ore");

        Assert.Single(manager.ActiveNpcs);
        Assert.True(registry.TryGet(npc.Id, out var registered));
        Assert.Same(npc, registered);
        Assert.Equal("Miner", npc.Name);
    }

    [Fact]
    public void DespawnNpc_Cleans_Up_From_Registry_And_ActiveNpcs()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var npc = manager.SpawnNpc("Farmer", new Vec3(0f, 64f, 0f));
        Assert.Single(manager.ActiveNpcs);

        bool removed = manager.DespawnNpc(npc.Id);

        Assert.True(removed);
        Assert.Empty(manager.ActiveNpcs);
        Assert.False(registry.TryGet(npc.Id, out _));
    }

    [Fact]
    public void PhysicsTick_Moves_Npc_With_Velocity()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);
        var npc = manager.SpawnNpc("Runner", new Vec3(0f, 0f, 0f));
        npc.Velocity = new Vec3(5f, 0f, 0f);

        manager.PhysicsTick(0.5f);

        Assert.True(npc.Position.X > 2.0f);
        // Spatial hash should reflect the new position
        var found = registry.FindNearest(new Vec3(2.5f, 0f, 0f), 1f);
        Assert.NotNull(found);
        Assert.Equal(npc.Id, found.Id);
    }

    [Fact]
    public void PhysicsTick_Auto_Picks_Up_Nearby_Items_Into_Npc_Inventory()
    {
        var eventBus = new EventBus();
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry, eventBus: eventBus);

        var npc = manager.SpawnNpc("Collector", new Vec3(0f, 0f, 0f));
        var item = manager.SpawnItem(new ItemStack("oak_log", 4), new Vec3(0.5f, 0f, 0.5f), pickupCooldown: 0f);

        var pickedEvents = new List<ItemPickedUp>();
        using var sub = eventBus.Subscribe<ItemPickedUp>(e => pickedEvents.Add(e));

        manager.PhysicsTick(0.1f);

        // Item should be picked up and despawned
        Assert.Equal(4, npc.Inventory.CountItem("oak_log"));
        Assert.True(item.IsCollected);
        Assert.Empty(manager.ActiveItems);
        Assert.Single(pickedEvents);
        Assert.Equal(npc.Id, pickedEvents[0].PickerId);
    }

    [Fact]
    public void DistanceBased_Lod_Calculates_Tiers_Correctly()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            LodDistanceThreshold = 64.0f
        };
        var manager = new NpcManager(registry, config)
        {
            FreezeDistance = 150.0f
        };

        // Register a player at origin (0, 0, 0)
        var player = new PlayerEntity(registry.GenerateNextId(), "Player", Vec3.Zero);
        registry.Register(player);

        // NPC 1: Close to player (dist 20m <= 64m) -> Normal LOD
        var npcNear = manager.SpawnNpc("NearNpc", new Vec3(20f, 0f, 0f));

        // NPC 2: Distant from player (dist 100m > 64m and <= 150m) -> Distant LOD
        var npcFar = manager.SpawnNpc("DistantNpc", new Vec3(100f, 0f, 0f));

        // NPC 3: Beyond freeze threshold (dist 200m > 150m) -> Frozen LOD
        var npcFrozen = manager.SpawnNpc("FrozenNpc", new Vec3(200f, 0f, 0f));

        // Trigger brain step to refresh LOD calculations
        manager.StepBrainTick();

        var scheduleNear = manager.GetNpcSchedule(npcNear.Id);
        var scheduleFar = manager.GetNpcSchedule(npcFar.Id);
        var scheduleFrozen = manager.GetNpcSchedule(npcFrozen.Id);

        Assert.Equal(NpcLodTier.Normal, scheduleNear.Tier);
        Assert.Equal(NpcLodTier.Distant, scheduleFar.Tier);
        Assert.Equal(NpcLodTier.Frozen, scheduleFrozen.Tier);

        // Move the frozen NPC close to the player
        npcFrozen.Position = new Vec3(10f, 0f, 0f);
        registry.UpdateSpatial(npcFrozen);

        manager.StepBrainTick();

        var updatedSchedule = manager.GetNpcSchedule(npcFrozen.Id);
        Assert.Equal(NpcLodTier.Normal, updatedSchedule.Tier);
    }

    [Fact]
    public void PhysicsTick_With_VoxelWorld_Resolves_Kinematic_Gravity_And_Collisions()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        ushort stoneId = blocks.GetId("stone");

        // Floor at Y = 0 (top surface at Y = 1.0f)
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // 2-block high wall at X = 2 (Y = 1 and 2)
        for (int z = -2; z <= 2; z++)
        {
            world.SetBlock(2, 1, z, stoneId);
            world.SetBlock(2, 2, z, stoneId);
        }

        var registry = new EntityRegistry();
        var manager = new NpcManager(registry, voxelWorld: world);

        // Spawn NPC floating at Y = 4.0f
        var npc = manager.SpawnNpc("KinematicBot", new Vec3(0f, 4.0f, 0f));
        Assert.False(npc.IsOnGround);

        // Run 60 physics ticks (1.0s) -> NPC falls 3m and lands on the floor
        for (int i = 0; i < 60; i++)
        {
            manager.PhysicsTick(1f / 60f);
        }

        Assert.True(npc.IsOnGround);
        Assert.Equal(1.0f, npc.Position.Y, 2);

        // Give NPC horizontal velocity towards the wall
        npc.Velocity = new Vec3(4.0f, 0f, 0f);

        for (int i = 0; i < 30; i++)
        {
            manager.PhysicsTick(1f / 60f);
        }

        // Must be stopped by wall at X = 2.0 (X <= 1.701 with half-width 0.3)
        Assert.True(npc.Position.X <= 1.701f);
        Assert.True(npc.IsOnWall);
        Assert.True(npc.IsOnGround);
    }

    [Fact]
    public void PhysicsTick_TicksActiveAction_AndCompletes()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry);

        var npc = manager.SpawnNpc("ActionBot", new Vec3(0f, 1f, 0f));
        var action = new WaitAction(npc, 0.2f);
        action.Start();
        npc.CurrentAction = action;

        Assert.True(action.Status.IsRunning);

        // Advance 15 ticks @ 60Hz = 0.25s > 0.2s duration
        for (int i = 0; i < 15; i++)
        {
            manager.PhysicsTick(1f / 60f);
        }

        Assert.True(action.Status.IsSucceeded);
    }
}
