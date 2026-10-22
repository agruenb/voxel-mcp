namespace VoxelKing.Core.Tests.Entities;

using VoxelKing.Core.Entities;
using Xunit;

public class InventoryTests
{
    [Fact]
    public void AddItem_StacksCorrectlyAndFillsEmptySlots()
    {
        var inv = new Inventory(slotCount: 4);

        // Add 50 logs (fits in slot 0, max 64)
        bool fullyAdded = inv.AddItem("oak_log", 50, out int remainder);
        Assert.True(fullyAdded);
        Assert.Equal(0, remainder);
        Assert.Equal(50, inv.CountItem("oak_log"));
        Assert.Equal(3, inv.FreeSlots);

        // Add 30 more logs (14 goes into slot 0, remaining 16 goes into slot 1)
        fullyAdded = inv.AddItem("oak_log", 30, out remainder);
        Assert.True(fullyAdded);
        Assert.Equal(0, remainder);
        Assert.Equal(80, inv.CountItem("oak_log"));

        Assert.Equal(64, inv.GetSlot(0)?.Count);
        Assert.Equal(16, inv.GetSlot(1)?.Count);
        Assert.Equal(2, inv.FreeSlots);
    }

    [Fact]
    public void AddItem_WhenInventoryFull_ReturnsRemainder()
    {
        var inv = new Inventory(slotCount: 1);

        inv.AddItem("dirt", 60, out int rem1);
        Assert.Equal(0, rem1);

        bool fullyAdded = inv.AddItem("dirt", 10, out int rem2);
        Assert.False(fullyAdded);
        Assert.Equal(6, rem2); // 4 added (to 64), 6 remaining
        Assert.Equal(64, inv.CountItem("dirt"));
    }

    [Fact]
    public void RemoveItem_DeductsFromMultipleSlots()
    {
        var inv = new Inventory(slotCount: 4);
        inv.AddItem("cobblestone", 80, out _);

        bool removed = inv.RemoveItem("cobblestone", 70);
        Assert.True(removed);
        Assert.Equal(10, inv.CountItem("cobblestone"));

        // Slot 0 should be empty (was 64, removed 64)
        Assert.Null(inv.GetSlot(0));
        // Slot 1 should have 10 left (was 16, removed 6)
        Assert.Equal(10, inv.GetSlot(1)?.Count);
    }

    [Fact]
    public void RemoveItem_WhenInsufficient_DoesNotMutate()
    {
        var inv = new Inventory(slotCount: 2);
        inv.AddItem("stone", 10, out _);

        bool removed = inv.RemoveItem("stone", 15);
        Assert.False(removed);
        Assert.Equal(10, inv.CountItem("stone"));
    }

    [Fact]
    public void Transfer_BetweenInventories_WorksCorrectly()
    {
        var playerInv = new Inventory(slotCount: 9);
        var chestInv = new Inventory(slotCount: 27);

        playerInv.AddItem("oak_log", 20, out _);
        Assert.Equal(20, playerInv.CountItem("oak_log"));
        Assert.Equal(0, chestInv.CountItem("oak_log"));

        // Transfer 15 logs from slot 0 to chest
        int transferred = Inventory.Transfer(playerInv, 0, chestInv, 15);
        Assert.Equal(15, transferred);
        Assert.Equal(5, playerInv.CountItem("oak_log"));
        Assert.Equal(15, chestInv.CountItem("oak_log"));

        // Transfer rest
        transferred = Inventory.Transfer(playerInv, 0, chestInv);
        Assert.Equal(5, transferred);
        Assert.Equal(0, playerInv.CountItem("oak_log"));
        Assert.Equal(20, chestInv.CountItem("oak_log"));
        Assert.Null(playerInv.GetSlot(0));
    }

    [Fact]
    public void TransferSlotToSlot_SwapsOrStacksCorrectly()
    {
        var invA = new Inventory(slotCount: 2);
        var invB = new Inventory(slotCount: 2);

        invA.SetSlot(0, new ItemStack("dirt", 10));
        invB.SetSlot(0, new ItemStack("stone", 5));

        // Swap different items
        bool swapped = Inventory.TransferSlotToSlot(invA, 0, invB, 0);
        Assert.True(swapped);
        Assert.Equal("stone", invA.GetSlot(0)?.ItemId);
        Assert.Equal(5, invA.GetSlot(0)?.Count);
        Assert.Equal("dirt", invB.GetSlot(0)?.ItemId);
        Assert.Equal(10, invB.GetSlot(0)?.Count);

        // Same items stack
        invA.SetSlot(1, new ItemStack("dirt", 5));
        bool stacked = Inventory.TransferSlotToSlot(invA, 1, invB, 0);
        Assert.True(stacked);
        Assert.Null(invA.GetSlot(1));
        Assert.Equal(15, invB.GetSlot(0)?.Count);
    }
}
