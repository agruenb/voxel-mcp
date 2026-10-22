namespace VoxelKing.Core.Tests.Npc.Actions;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using Xunit;

public class ContainerActionTests
{
    [Fact]
    public void DepositAction_DepositsItemsIntoContainer()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);

        var action = new DepositAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(6, npc.Inventory.CountItem("stone"));
        Assert.Equal(4, chestInv.CountItem("stone"));
    }

    [Fact]
    public void DepositAction_DepositsAllItemsWhenCountNegativeOne()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);

        var action = new DepositAction(npc, chestPos, "stone", -1, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(0, npc.Inventory.CountItem("stone"));
        Assert.Equal(10, chestInv.CountItem("stone"));
    }

    [Fact]
    public void DepositAction_FailsTooFarWhenBeyondRange()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(10, 1, 0);
        containers.GetOrCreateContainer(chestPos);

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);

        var action = new DepositAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TooFar, action.Status.FailureReason);
    }

    [Fact]
    public void DepositAction_FailsInvalidTargetWhenNoContainer()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0); // No container created

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);

        var action = new DepositAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void DepositAction_FailsNoItemWhenNpcLacksItems()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        containers.GetOrCreateContainer(chestPos);

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 2, out _);

        var action = new DepositAction(npc, chestPos, "stone", 5, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.NoItem, action.Status.FailureReason);
    }

    [Fact]
    public void DepositAction_FailsInventoryFullWhenContainerFull()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);

        // Fill container with other items
        for (int i = 0; i < chestInv.SlotCount; i++)
        {
            chestInv.SetSlot(i, new ItemStack("dirt", 64));
        }

        var npc = new NpcState(new EntityId(1), "Depositor", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Inventory.AddItem("stone", 10, out _);

        var action = new DepositAction(npc, chestPos, "stone", 5, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InventoryFull, action.Status.FailureReason);
    }

    [Fact]
    public void WithdrawAction_WithdrawsItemsFromContainer()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);
        chestInv.AddItem("stone", 10, out _);

        var npc = new NpcState(new EntityId(1), "Withdrawer", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new WithdrawAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(6, chestInv.CountItem("stone"));
        Assert.Equal(4, npc.Inventory.CountItem("stone"));
    }

    [Fact]
    public void WithdrawAction_WithdrawsAllWhenCountNegativeOne()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);
        chestInv.AddItem("stone", 10, out _);

        var npc = new NpcState(new EntityId(1), "Withdrawer", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new WithdrawAction(npc, chestPos, "stone", -1, containers);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(0, chestInv.CountItem("stone"));
        Assert.Equal(10, npc.Inventory.CountItem("stone"));
    }

    [Fact]
    public void WithdrawAction_FailsTooFarWhenBeyondRange()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(10, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);
        chestInv.AddItem("stone", 10, out _);

        var npc = new NpcState(new EntityId(1), "Withdrawer", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new WithdrawAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TooFar, action.Status.FailureReason);
    }

    [Fact]
    public void WithdrawAction_FailsNoItemWhenContainerEmpty()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        containers.GetOrCreateContainer(chestPos);

        var npc = new NpcState(new EntityId(1), "Withdrawer", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new WithdrawAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.NoItem, action.Status.FailureReason);
    }

    [Fact]
    public void WithdrawAction_FailsInventoryFullWhenNpcFull()
    {
        var containers = new ContainerManager();
        var chestPos = new Vec3i(1, 1, 0);
        var chestInv = containers.GetOrCreateContainer(chestPos);
        chestInv.AddItem("stone", 10, out _);

        var npc = new NpcState(new EntityId(1), "Withdrawer", new Vec3(0.5f, 1.0f, 0.5f));
        for (int i = 0; i < npc.Inventory.SlotCount; i++)
        {
            npc.Inventory.SetSlot(i, new ItemStack("dirt", 64));
        }

        var action = new WithdrawAction(npc, chestPos, "stone", 4, containers);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InventoryFull, action.Status.FailureReason);
    }
}
