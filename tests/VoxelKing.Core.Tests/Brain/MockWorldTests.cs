namespace VoxelKing.Core.Tests.Brain;

using System;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using Xunit;

public class MockWorldTests
{
    [Fact]
    public void GetBlock_WithinRadius_ReturnsPlausibleOrExplicitBlock()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        // Explicit block registered in defaults
        var oakPos = new Vec3i(3, 64, 0);
        Assert.Equal("oak_log", world.GetBlock(oakPos));

        // Default surface height
        Assert.Equal("grass", world.GetBlock(new Vec3i(0, 64, 0)));
        Assert.Equal("dirt", world.GetBlock(new Vec3i(0, 62, 0)));
        Assert.Equal("stone", world.GetBlock(new Vec3i(0, 50, 0)));
        Assert.Equal("air", world.GetBlock(new Vec3i(0, 70, 0)));
    }

    [Fact]
    public void GetBlock_OutsideRadius_ReturnsNull()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));
        var farPos = new Vec3i(100, 64, 100);

        Assert.Null(world.GetBlock(farPos));
    }

    [Fact]
    public void FindBlock_FindsExistingBlock_OrReturnsNullForNonexistent()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        var foundOak = world.FindBlock("oak_log", radius: 10);
        Assert.NotNull(foundOak);
        Assert.Equal("oak_log", world.GetBlock(foundOak.Value));

        var notFound = world.FindBlock("nonexistent_block_type_xyz", radius: 10);
        Assert.Null(notFound);
    }

    [Fact]
    public void FindBlocks_RespectsMaxAndRadius()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        var logs = world.FindBlocks("oak_log", radius: 24, max: 2);
        Assert.NotEmpty(logs);
        Assert.True(logs.Count <= 2);

        var empty = world.FindBlocks("oak_log", radius: 0);
        Assert.Empty(empty);
    }

    [Fact]
    public void FindEntity_FindsRegisteredEntities_OrReturnsNullForNonexistent()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        var playerId = world.FindEntity(EntityKind.Player);
        Assert.NotNull(playerId);
        Assert.Equal(new EntityId(100), playerId.Value);

        var itemId = world.FindEntity("item");
        Assert.NotNull(itemId);
        Assert.Equal(new EntityId(200), itemId.Value);

        var unknownKind = world.FindEntity("unknown_kind");
        Assert.Null(unknownKind);
    }

    [Fact]
    public void GetEntity_FindsPerceivedEntity_OrReturnsNullWhenMissing()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        var player = world.GetEntity(new EntityId(100));
        Assert.NotNull(player);
        Assert.Equal("Player", player.Name);
        Assert.Equal(EntityKind.Player, player.Kind);

        var missing = world.GetEntity(new EntityId(9999));
        Assert.Null(missing);
    }

    [Fact]
    public void WalkabilityAndSurface_ReturnExpectedPlausibleValues()
    {
        var world = new MockWorld(new Vec3(0, 64, 0));

        Assert.True(world.IsWalkable(new Vec3i(0, 64, 0)));
        Assert.False(world.IsWalkable(new Vec3i(0, 10, 0)));

        var surface = world.GetSurface(5, 5);
        Assert.NotNull(surface);
        Assert.Equal(new Vec3i(5, 64, 5), surface.Value);

        var farSurface = world.GetSurface(100, 100);
        Assert.Null(farSurface);
    }

    [Fact]
    public void TimeAndDay_ReturnConfiguredDefaults()
    {
        var world = new MockWorld();

        Assert.Equal(12.0f, world.GetTime());
        Assert.True(world.IsDay());

        world.Time = 22.0f;
        world.IsDayTime = false;

        Assert.Equal(22.0f, world.GetTime());
        Assert.False(world.IsDay());
    }
}
