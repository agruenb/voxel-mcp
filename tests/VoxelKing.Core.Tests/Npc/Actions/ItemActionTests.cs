namespace VoxelKing.Core.Tests.Npc.Actions;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using Xunit;

public class ItemActionTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus, EntityRegistry Registry, PathfindingService PathService) CreateTestEnv()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var registry = new EntityRegistry();
        var pathService = new PathfindingService(world);
        return (world, blocks, bus, registry, pathService);
    }

    [Fact]
    public void PickUpAction_SucceedsWhenNearItem()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var item = new ItemEntity(new EntityId(2), new ItemStack("dirt", 5), new Vec3(1.0f, 1.0f, 0.5f), pickupCooldown: 0f);
        registry.Register(item);

        var action = new PickUpAction(npc, item.Id, registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();

        Assert.True(action.Status.IsRunning);
        action.Tick(0.1f);

        Assert.True(action.Status.IsSucceeded);
        Assert.True(item.IsCollected);
        Assert.Equal(5, npc.Inventory.CountItem("dirt"));
    }

    [Fact]
    public void PickUpAction_FailsInvalidTargetWhenMissingOrNotItem()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var action = new PickUpAction(npc, new EntityId(999), registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void PickUpAction_FailsTargetGoneWhenAlreadyCollected()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var item = new ItemEntity(new EntityId(2), new ItemStack("dirt", 5), new Vec3(1.0f, 1.0f, 0.5f));
        item.MarkCollected();
        registry.Register(item);

        var action = new PickUpAction(npc, item.Id, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TargetGone, action.Status.FailureReason);
    }

    [Fact]
    public void PickUpAction_FailsInventoryFullWhenNoRoom()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        for (int i = 0; i < npc.Inventory.SlotCount; i++)
        {
            npc.Inventory.SetSlot(i, new ItemStack("stone", 64));
        }

        var item = new ItemEntity(new EntityId(2), new ItemStack("dirt", 5), new Vec3(1.0f, 1.0f, 0.5f));
        registry.Register(item);

        var action = new PickUpAction(npc, item.Id, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InventoryFull, action.Status.FailureReason);
    }

    [Fact]
    public void PickUpAction_FailsInterruptedWhenCancelled()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var item = new ItemEntity(new EntityId(2), new ItemStack("dirt", 5), new Vec3(5.0f, 1.0f, 0.5f));
        registry.Register(item);

        var action = new PickUpAction(npc, item.Id, registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Cancel();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
    }

    [Fact]
    public void DropAction_DropsItemsAndCreatesItemEntity()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Dropper", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);
        registry.Register(npc);

        var action = new DropAction(npc, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(6, npc.Inventory.CountItem("stone"));

        // Dropped item should be in registry
        var items = registry.FindAll(npc.Position, 2f);
        Assert.Contains(items, e => e is ItemEntity ie && ie.Stack.ItemId == "stone" && ie.Stack.Count == 4);
    }

    [Fact]
    public void DropAction_FailsInvalidTargetWhenCountZeroOrNegative()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Dropper", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 5, out _);

        var action = new DropAction(npc, "stone", 0, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void DropAction_FailsNoItemWhenNotEnoughHeld()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Dropper", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 2, out _);

        var action = new DropAction(npc, "stone", 5, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.NoItem, action.Status.FailureReason);
    }

    [Fact]
    public void DropAction_SetsTossVelocityAndPickupCooldown()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "Dropper", new Vec3(0f, 1.0f, 0f));
        npc.Inventory.AddItem("dirt", 5, out _);
        npc.Facing = 0f; // facing -Z
        registry.Register(npc);

        var action = new DropAction(npc, "dirt", 2, registry);
        action.Start();

        var item = registry.GetAll<ItemEntity>().Single();
        Assert.Equal("dirt", item.Stack.ItemId);
        Assert.Equal(2, item.Stack.Count);
        Assert.True(item.Velocity.Y > 0f);
        Assert.True(item.PickupCooldown >= 1.0f);
    }

    [Fact]
    public void PickUpAction_SucceedsWhenAutoPickupCollectsItemUponArrival()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var manager = new NpcManager(registry, voxelWorld: world, eventBus: bus);
        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0f, 1.0f, 0f));
        registry.Register(npc);

        var item = manager.SpawnItem(new ItemStack("dirt", 3), new Vec3(1.0f, 1.0f, 0f), pickupCooldown: 0f);

        var action = new PickUpAction(npc, item.Id, registry, manager, pathService, world, bus);
        action.Start();

        // Simulate auto-pickup event as manager physics tick would fire
        bus.Publish(new ItemPickedUp(npc.Id, item.Id, item.Name, 3));
        action.Tick(0.1f);

        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void GiveAction_TransfersItemsToTargetEntity()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        giver.Inventory.AddItem("stone", 10, out _);

        var receiver = new NpcState(new EntityId(2), "Receiver", new Vec3(1.5f, 1.0f, 0.5f));

        registry.Register(giver);
        registry.Register(receiver);

        var action = new GiveAction(giver, receiver.Id, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(6, giver.Inventory.CountItem("stone"));
        Assert.Equal(4, receiver.Inventory.CountItem("stone"));
    }

    [Fact]
    public void GiveAction_FailsInvalidTargetOnSelfOrMissing()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        giver.Inventory.AddItem("stone", 5, out _);
        registry.Register(giver);

        // Self
        var actionSelf = new GiveAction(giver, giver.Id, "stone", 2, registry);
        actionSelf.Start();
        Assert.True(actionSelf.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, actionSelf.Status.FailureReason);

        // Missing
        var actionMissing = new GiveAction(giver, new EntityId(999), "stone", 2, registry);
        actionMissing.Start();
        Assert.True(actionMissing.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, actionMissing.Status.FailureReason);
    }

    [Fact]
    public void GiveAction_FailsTooFarWhenBeyondRange()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        giver.Inventory.AddItem("stone", 10, out _);

        var receiver = new NpcState(new EntityId(2), "Receiver", new Vec3(10.0f, 1.0f, 0.5f));

        registry.Register(giver);
        registry.Register(receiver);

        var action = new GiveAction(giver, receiver.Id, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TooFar, action.Status.FailureReason);
    }

    [Fact]
    public void GiveAction_FailsNoItemWhenGiverLacksItems()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        var receiver = new NpcState(new EntityId(2), "Receiver", new Vec3(1.5f, 1.0f, 0.5f));

        registry.Register(giver);
        registry.Register(receiver);

        var action = new GiveAction(giver, receiver.Id, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.NoItem, action.Status.FailureReason);
    }

    [Fact]
    public void GiveAction_FailsInventoryFullWhenReceiverHasNoRoom()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        giver.Inventory.AddItem("stone", 10, out _);

        var receiver = new NpcState(new EntityId(2), "Receiver", new Vec3(1.5f, 1.0f, 0.5f));
        for (int i = 0; i < receiver.Inventory.SlotCount; i++)
        {
            receiver.Inventory.SetSlot(i, new ItemStack("dirt", 64));
        }

        registry.Register(giver);
        registry.Register(receiver);

        var action = new GiveAction(giver, receiver.Id, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InventoryFull, action.Status.FailureReason);
    }

    [Fact]
    public void PickUpAction_FailsUnreachableWhenNoPathToItem()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stone);
        world.SetBlock(20, 0, 20, stone);

        var npc = new NpcState(new EntityId(1), "Picker", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var item = new ItemEntity(new EntityId(2), new ItemStack("dirt", 5), new Vec3(20.5f, 1.0f, 20.5f));
        registry.Register(item);

        var action = new PickUpAction(npc, item.Id, registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        action.Tick(0.1f);

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Unreachable, action.Status.FailureReason);
    }

    [Fact]
    public void GiveAction_FailsTargetGoneWhenReceiverIsDead()
    {
        var (world, blocks, bus, registry, pathService) = CreateTestEnv();
        var giver = new NpcState(new EntityId(1), "Giver", new Vec3(0.5f, 1.0f, 0.5f));
        giver.Inventory.AddItem("stone", 10, out _);

        var receiver = new NpcState(new EntityId(2), "Receiver", new Vec3(1.5f, 1.0f, 0.5f), health: 0f);

        registry.Register(giver);
        registry.Register(receiver);

        var action = new GiveAction(giver, receiver.Id, "stone", 4, registry);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TargetGone, action.Status.FailureReason);
    }
}

