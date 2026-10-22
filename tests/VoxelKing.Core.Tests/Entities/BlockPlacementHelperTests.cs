namespace VoxelKing.Core.Tests.Entities;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using Xunit;

public class BlockPlacementHelperTests
{
    [Fact]
    public void IntersectsEntity_WhenBlockInsidePlayer_ReturnsTrue()
    {
        // Player standing at (0.5, 1.0, 0.5), height 1.8 (occupies Y: 1.0..2.8)
        var feet = new Vec3(0.5f, 1.0f, 0.5f);

        // Block at (0, 1, 0) occupies [0..1, 1..2, 0..1]
        Assert.True(BlockPlacementHelper.IntersectsEntity(new Vec3i(0, 1, 0), feet));

        // Block at (0, 2, 0) occupies [0..1, 2..3, 0..1]
        Assert.True(BlockPlacementHelper.IntersectsEntity(new Vec3i(0, 2, 0), feet));
    }

    [Fact]
    public void IntersectsEntity_WhenBlockOutsidePlayer_ReturnsFalse()
    {
        var feet = new Vec3(0.5f, 1.0f, 0.5f);

        // Block at (2, 1, 0) is 2 units away horizontally
        Assert.False(BlockPlacementHelper.IntersectsEntity(new Vec3i(2, 1, 0), feet));

        // Block at (0, 0, 0) is beneath feet
        Assert.False(BlockPlacementHelper.IntersectsEntity(new Vec3i(0, 0, 0), feet));

        // Block at (0, 3, 0) is above head
        Assert.False(BlockPlacementHelper.IntersectsEntity(new Vec3i(0, 3, 0), feet));
    }
}
