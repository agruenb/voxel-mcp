namespace VoxelKing.Core.Tests.Voxel;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class VoxelRaycastTests
{
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();

    [Fact]
    public void Cast_DownwardsAtFloor_HitsTopFace()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);

        ushort stone = _registry.GetId("stone");
        world.SetBlock(0, 0, 0, stone);

        // Ray starts at (0.5, 2.5, 0.5) pointing straight down
        var origin = new Vec3(0.5f, 2.5f, 0.5f);
        var dir = new Vec3(0f, -1f, 0f);

        var result = VoxelRaycast.Cast(origin, dir, 5.0f, world, _registry);

        Assert.True(result.IsHit);
        Assert.Equal(new Vec3i(0, 0, 0), result.BlockPos);
        Assert.Equal(new Vec3i(0, 1, 0), result.AdjacentPos);
        Assert.Equal(BlockFace.Top, result.Face);
        Assert.Equal(stone, result.BlockId);
        Assert.True(MathF.Abs(result.Distance - 1.5f) < 0.01f);
    }

    [Fact]
    public void Cast_HorizontallyAtWall_HitsWestFace()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);

        ushort stone = _registry.GetId("stone");
        world.SetBlock(5, 0, 0, stone);

        // Ray starts at (1.5, 0.5, 0.5) pointing towards +X
        var origin = new Vec3(1.5f, 0.5f, 0.5f);
        var dir = new Vec3(1f, 0f, 0f);

        var result = VoxelRaycast.Cast(origin, dir, 10.0f, world, _registry);

        Assert.True(result.IsHit);
        Assert.Equal(new Vec3i(5, 0, 0), result.BlockPos);
        Assert.Equal(new Vec3i(4, 0, 0), result.AdjacentPos);
        Assert.Equal(BlockFace.West, result.Face);
        Assert.Equal(stone, result.BlockId);
        Assert.True(MathF.Abs(result.Distance - 3.5f) < 0.01f);
    }

    [Fact]
    public void Cast_ExceedingMaxDistance_ReturnsMiss()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);

        ushort stone = _registry.GetId("stone");
        world.SetBlock(10, 0, 0, stone);

        var origin = new Vec3(0.5f, 0.5f, 0.5f);
        var dir = new Vec3(1f, 0f, 0f);

        // Target is 9.5 units away, maxDistance is only 5.0
        var result = VoxelRaycast.Cast(origin, dir, 5.0f, world, _registry);

        Assert.False(result.IsHit);
    }

    [Fact]
    public void Cast_ThroughAir_FindsDistantBlock()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);

        ushort log = _registry.GetId("oak_log");
        world.SetBlock(2, 2, 2, log);

        var origin = new Vec3(0.5f, 0.5f, 0.5f);
        var dir = (new Vec3(2.5f, 2.5f, 2.5f) - origin).Normalized();

        var result = VoxelRaycast.Cast(origin, dir, 10.0f, world, _registry);

        Assert.True(result.IsHit);
        Assert.Equal(new Vec3i(2, 2, 2), result.BlockPos);
        Assert.Equal(log, result.BlockId);
    }

    [Fact]
    public void Cast_OriginInsideSolid_HitsImmediately()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);

        ushort stone = _registry.GetId("stone");
        world.SetBlock(0, 0, 0, stone);

        var origin = new Vec3(0.5f, 0.5f, 0.5f);
        var dir = new Vec3(1f, 0f, 0f);

        var result = VoxelRaycast.Cast(origin, dir, 5.0f, world, _registry);

        Assert.True(result.IsHit);
        Assert.Equal(new Vec3i(0, 0, 0), result.BlockPos);
        Assert.Equal(0f, result.Distance);
    }
}
