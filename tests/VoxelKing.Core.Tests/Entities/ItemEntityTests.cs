namespace VoxelKing.Core.Tests.Entities;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Voxel;
using Xunit;

public class ItemEntityTests
{
    [Fact]
    public void ItemEntity_Initializes_With_Correct_Properties()
    {
        var id = new EntityId(1);
        var stack = new ItemStack("oak_log", 10);
        var pos = new Vec3(5f, 10f, 5f);
        var vel = new Vec3(0f, 2f, 0f);

        var item = new ItemEntity(id, stack, pos, vel, pickupCooldown: 0.5f);

        Assert.Equal(id, item.Id);
        Assert.Equal(EntityKind.Item, item.Kind);
        Assert.Equal("oak_log", item.Name);
        Assert.Equal(pos, item.Position);
        Assert.Equal(vel, item.Velocity);
        Assert.Equal(10, item.Stack.Count);
        Assert.Equal(0.5f, item.PickupCooldown);
        Assert.False(item.CanPickup); // Due to pickup cooldown
        Assert.True(item.IsActive);
        Assert.False(item.IsCollected);
    }

    [Fact]
    public void Cooldown_Prevents_Pickup_Until_Expired()
    {
        var id = new EntityId(1);
        var item = new ItemEntity(id, new ItemStack("dirt", 5), Vec3.Zero, pickupCooldown: 1.0f);
        var inventory = new Inventory(10);

        Assert.False(item.CanPickup);
        bool pickedUp = item.TryPickup(inventory, out int count);
        Assert.False(pickedUp);
        Assert.Equal(0, count);

        // Tick physics by 0.5s -> cooldown remains 0.5s
        item.TickPhysics(0.5f);
        Assert.False(item.CanPickup);

        // Tick physics by 0.6s -> cooldown expires
        item.TickPhysics(0.6f);
        Assert.True(item.CanPickup);

        pickedUp = item.TryPickup(inventory, out count);
        Assert.True(pickedUp);
        Assert.Equal(5, count);
        Assert.True(item.IsCollected);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void Partial_Pickup_Updates_Stack_Count_And_Keeps_Item_Active()
    {
        var id = new EntityId(1);
        var item = new ItemEntity(id, new ItemStack("stone", 10), Vec3.Zero, pickupCooldown: 0f);

        // Inventory with only 1 slot that already has 60 stone (maxStack 64 -> 4 space left)
        var inventory = new Inventory(1);
        inventory.SetSlot(0, new ItemStack("stone", 60));

        bool pickedUp = item.TryPickup(inventory, out int count);

        Assert.True(pickedUp);
        Assert.Equal(4, count);
        Assert.Equal(6, item.Stack.Count); // 10 - 4 = 6 remaining
        Assert.False(item.IsCollected);
        Assert.True(item.IsActive);
        Assert.True(inventory.IsFull);
    }

    [Fact]
    public void TickPhysics_Applies_Gravity()
    {
        var item = new ItemEntity(new EntityId(1), new ItemStack("dirt", 1), new Vec3(0f, 10f, 0f), Vec3.Zero, 0f);

        item.TickPhysics(0.1f, gravity: 20f);

        // Velocity.Y should be -2 m/s
        Assert.True(item.Velocity.Y < 0f);
        // Position.Y should be < 10
        Assert.True(item.Position.Y < 10f);
    }

    [Fact]
    public void TickPhysics_Lands_On_Solid_Block_When_World_Provided()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        // Place solid stone at (0, 0, 0)
        world.SetBlock(0, 0, 0, blocks.GetId("stone"));

        // Dropped item at Y = 1.2 moving downward
        var item = new ItemEntity(new EntityId(1), new ItemStack("dirt", 1), new Vec3(0.5f, 1.2f, 0.5f), new Vec3(0f, -5f, 0f), 0f);

        item.TickPhysics(0.1f, world);

        // Should land on top of the block at Y = 1.0f
        Assert.True(item.Position.Y >= 1.0f);
        Assert.Equal(0f, item.Velocity.Y);
    }
}
