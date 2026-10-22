namespace VoxelKing.Core.Tests.Npc.Actions;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using Xunit;

public class WorldActionTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus, ContainerManager Containers) CreateTestEnv()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var containers = new ContainerManager();
        return (world, blocks, bus, containers);
    }

    [Fact]
    public void MineAction_BreaksBlockAndAddsDropToInventory()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(1, 1, 0);
        world.SetBlock(pos, stone);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        int initialCobblestone = npc.Inventory.CountItem("cobblestone");
        Assert.Equal(0, npc.Inventory.CountItem("stone"));

        var action = new MineAction(npc, pos, world, blocks, containers, duration: 0f);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(blocks.AirId, world.GetBlock(pos));
        Assert.Equal(initialCobblestone + 1, npc.Inventory.CountItem("cobblestone"));
    }

    [Fact]
    public void MineAction_BreaksContainer_TransfersItemsToNpc()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort chestId = blocks.GetId("chest");
        var pos = new Vec3i(1, 1, 0);
        world.SetBlock(pos, chestId);

        var chestInv = containers.GetOrCreateContainer(pos);
        chestInv.AddItem("oak_log", 5, out _);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new MineAction(npc, pos, world, blocks, containers, duration: 0f);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(blocks.AirId, world.GetBlock(pos));
        Assert.False(containers.HasContainer(pos));
        Assert.Equal(5, npc.Inventory.CountItem("oak_log"));
        Assert.Equal(1, npc.Inventory.CountItem("chest")); // Block drop
    }

    [Fact]
    public void MineAction_FailsTooFarWhenBeyondRange()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(10, 1, 0);
        world.SetBlock(pos, stone);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new MineAction(npc, pos, world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TooFar, action.Status.FailureReason);
    }

    [Fact]
    public void MineAction_FailsInvalidTargetWhenTargetIsAir()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        var pos = new Vec3i(1, 1, 0); // Air by default

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new MineAction(npc, pos, world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void MineAction_FailsInventoryFullWhenCannotStoreDrop()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(1, 1, 0);
        world.SetBlock(pos, stone);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        // Fill inventory completely with different items
        for (int i = 0; i < npc.Inventory.SlotCount; i++)
        {
            npc.Inventory.SetSlot(i, new ItemStack("dirt", 64));
        }

        var action = new MineAction(npc, pos, world, blocks, containers, duration: 0f);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InventoryFull, action.Status.FailureReason);
    }

    [Fact]
    public void MineAction_FailsTargetGoneWhenBlockChangesDuringMining()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(1, 1, 0);
        world.SetBlock(pos, stone);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new MineAction(npc, pos, world, blocks, containers, duration: 2.0f);
        action.Start();
        Assert.True(action.Status.IsRunning);

        // Someone else breaks the block
        world.SetBlock(pos, blocks.AirId);

        action.Tick(0.5f);
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TargetGone, action.Status.FailureReason);
    }

    [Fact]
    public void MineAction_FailsInterruptedWhenCancelled()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(1, 1, 0);
        world.SetBlock(pos, stone);

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new MineAction(npc, pos, world, blocks, containers, duration: 5.0f);
        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Cancel();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_PlacesBlockAndDeductsFromInventory()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(1, 0, 0, stone); // Base block to be adjacent to

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 3, out _);

        var placePos = new Vec3i(1, 1, 0);
        var action = new PlaceAction(npc, placePos, "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(blocks.GetId("dirt"), world.GetBlock(placePos));
        Assert.Equal(2, npc.Inventory.CountItem("dirt"));
    }

    [Fact]
    public void PlaceAction_CreatesContainerWhenPlacingChest()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(1, 0, 0, stone);

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("chest", 1, out _);

        var placePos = new Vec3i(1, 1, 0);
        var action = new PlaceAction(npc, placePos, "chest", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.True(containers.HasContainer(placePos));
    }

    [Fact]
    public void PlaceAction_FailsNoItemWhenNotHeld()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        world.SetBlock(1, 0, 0, blocks.GetId("stone"));

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new PlaceAction(npc, new Vec3i(1, 1, 0), "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.NoItem, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_FailsTooFarWhenBeyondReach()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        world.SetBlock(10, 0, 0, blocks.GetId("stone"));

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 1, out _);

        var action = new PlaceAction(npc, new Vec3i(10, 1, 0), "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TooFar, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_FailsOccupiedWhenCellAlreadySolid()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(1, 1, 0, stone); // Already solid
        world.SetBlock(1, 0, 0, stone);

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 1, out _);

        var action = new PlaceAction(npc, new Vec3i(1, 1, 0), "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Occupied, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_FailsOccupiedWhenIntersectingNpc()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stone);

        // NPC standing at (0.5, 1.0, 0.5); feet occupy (0, 1, 0)
        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 1, out _);

        var action = new PlaceAction(npc, new Vec3i(0, 1, 0), "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Occupied, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_FailsInvalidTargetWhenNotAdjacentToSolid()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        // Place in mid-air with no neighbors
        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 1, out _);

        var action = new PlaceAction(npc, new Vec3i(2, 2, 0), "dirt", world, blocks, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void PlaceAction_FailsInterruptedWhenCancelled()
    {
        var (world, blocks, bus, containers) = CreateTestEnv();
        world.SetBlock(1, 0, 0, blocks.GetId("stone"));

        var npc = new NpcState(new EntityId(1), "Builder", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("dirt", 1, out _);

        var action = new PlaceAction(npc, new Vec3i(1, 1, 0), "dirt", world, blocks, containers);
        action.Cancel();

        Assert.True(action.Status.IsFailed || action.Status.IsNotStarted);
    }
}
