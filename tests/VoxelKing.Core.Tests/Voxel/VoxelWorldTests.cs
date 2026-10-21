namespace VoxelKing.Core.Tests.Voxel;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class VoxelWorldTests
{
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();
    private readonly EventBus _bus = new();

    [Fact]
    public void GetBlock_UnloadedChunkReturnsAir()
    {
        var world = new VoxelWorld(_registry, _bus);

        Assert.Equal((ushort)0, world.GetBlock(new Vec3i(100, 200, 300)));
        Assert.Equal((ushort)0, world.GetBlock(new Vec3i(-50, -20, -10)));
        Assert.Equal(0, world.ChunkCount);
    }

    [Fact]
    public void SetBlock_SettingAirOnUnloadedChunkDoesNotCreateChunk()
    {
        var world = new VoxelWorld(_registry, _bus);

        bool changed = world.SetBlock(new Vec3i(10, 20, 30), _registry.AirId);

        Assert.False(changed);
        Assert.Equal(0, world.ChunkCount);
    }

    [Fact]
    public void SetBlock_SettingSolidBlockCreatesChunkAndSetsVoxel()
    {
        var world = new VoxelWorld(_registry, _bus);
        var pos = new Vec3i(5, 6, 7);

        bool changed = world.SetBlock(pos, _registry.GetId("stone"));

        Assert.True(changed);
        Assert.Equal(1, world.ChunkCount);
        Assert.Equal(_registry.GetId("stone"), world.GetBlock(pos));
    }

    [Fact]
    public void SetBlockAndGetBlock_NegativeCoordinatesPersistAcrossChunks()
    {
        var world = new VoxelWorld(_registry, _bus);

        var p1 = new Vec3i(-1, -1, -1);       // Chunk (-1, -1, -1), local (31, 31, 31)
        var p2 = new Vec3i(-32, -32, -32);   // Chunk (-1, -1, -1), local (0, 0, 0)
        var p3 = new Vec3i(-33, 10, -65);     // Chunk (-2, 0, -3), local (31, 10, 31)

        world.SetBlock(p1, _registry.GetId("dirt"));
        world.SetBlock(p2, _registry.GetId("stone"));
        world.SetBlock(p3, _registry.GetId("oak_log"));

        Assert.Equal(_registry.GetId("dirt"), world.GetBlock(p1));
        Assert.Equal(_registry.GetId("stone"), world.GetBlock(p2));
        Assert.Equal(_registry.GetId("oak_log"), world.GetBlock(p3));
    }

    [Fact]
    public void SetBlock_PublishesBlockChangedEvent()
    {
        var world = new VoxelWorld(_registry, _bus);
        var pos = new Vec3i(10, 15, 20);
        var stoneId = _registry.GetId("stone");
        var dirtId = _registry.GetId("dirt");

        var events = new List<BlockChanged>();
        using var sub = _bus.Subscribe<BlockChanged>(e => events.Add(e));

        // Initial change from Air (0) to Stone (3)
        bool changed1 = world.SetBlock(pos, stoneId);
        Assert.True(changed1);
        Assert.Single(events);
        Assert.Equal(pos, events[0].Position);
        Assert.Equal((ushort)0, events[0].OldBlock);
        Assert.Equal(stoneId, events[0].NewBlock);

        // Same block: no-op, no event published
        bool changedSame = world.SetBlock(pos, stoneId);
        Assert.False(changedSame);
        Assert.Single(events);

        // Change from Stone (3) to Dirt (2)
        bool changed2 = world.SetBlock(pos, dirtId);
        Assert.True(changed2);
        Assert.Equal(2, events.Count);
        Assert.Equal(pos, events[1].Position);
        Assert.Equal(stoneId, events[1].OldBlock);
        Assert.Equal(dirtId, events[1].NewBlock);
    }

    [Fact]
    public void SetBlock_InteriorVoxelDoesNotDirtyNeighborChunks()
    {
        var world = new VoxelWorld(_registry, _bus);

        // Create center chunk (0, 0, 0) and orthogonal neighbors
        var center = world.GetOrCreateChunk(Vec3i.Zero);
        var west = world.GetOrCreateChunk(Vec3i.Left); // (-1, 0, 0)
        var east = world.GetOrCreateChunk(Vec3i.Right); // (1, 0, 0)

        // Reset dirty flags
        center.IsDirty = false;
        west.IsDirty = false;
        east.IsDirty = false;

        // Edit interior voxel at (16, 16, 16)
        world.SetBlock(new Vec3i(16, 16, 16), _registry.GetId("stone"));

        Assert.True(center.IsDirty);
        Assert.False(west.IsDirty);
        Assert.False(east.IsDirty);
    }

    [Fact]
    public void SetBlock_BorderVoxelsDirtyOrthogonalNeighborChunks()
    {
        var world = new VoxelWorld(_registry, _bus);

        var center = world.GetOrCreateChunk(Vec3i.Zero);
        var left = world.GetOrCreateChunk(Vec3i.Left);     // -X (-1, 0, 0)
        var right = world.GetOrCreateChunk(Vec3i.Right);   // +X (1, 0, 0)
        var down = world.GetOrCreateChunk(Vec3i.Down);     // -Y (0, -1, 0)
        var up = world.GetOrCreateChunk(Vec3i.Up);         // +Y (0, 1, 0)
        var north = world.GetOrCreateChunk(Vec3i.North);   // -Z (0, 0, -1)
        var south = world.GetOrCreateChunk(Vec3i.South);   // +Z (0, 0, 1)

        void ResetAllDirty()
        {
            center.IsDirty = false;
            left.IsDirty = false;
            right.IsDirty = false;
            down.IsDirty = false;
            up.IsDirty = false;
            north.IsDirty = false;
            south.IsDirty = false;
        }

        // Test West border: local X = 0 (world 0, 16, 16)
        ResetAllDirty();
        world.SetBlock(new Vec3i(0, 16, 16), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.True(left.IsDirty); // -X neighbor dirty
        Assert.False(right.IsDirty);
        Assert.False(down.IsDirty);
        Assert.False(up.IsDirty);
        Assert.False(north.IsDirty);
        Assert.False(south.IsDirty);

        // Test East border: local X = 31 (world 31, 16, 16)
        ResetAllDirty();
        world.SetBlock(new Vec3i(31, 16, 16), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.False(left.IsDirty);
        Assert.True(right.IsDirty); // +X neighbor dirty
        Assert.False(down.IsDirty);

        // Test Bottom border: local Y = 0 (world 16, 0, 16)
        ResetAllDirty();
        world.SetBlock(new Vec3i(16, 0, 16), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.True(down.IsDirty); // -Y neighbor dirty
        Assert.False(up.IsDirty);

        // Test Top border: local Y = 31 (world 16, 31, 16)
        ResetAllDirty();
        world.SetBlock(new Vec3i(16, 31, 16), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.True(up.IsDirty); // +Y neighbor dirty
        Assert.False(down.IsDirty);

        // Test North border: local Z = 0 (world 16, 16, 0)
        ResetAllDirty();
        world.SetBlock(new Vec3i(16, 16, 0), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.True(north.IsDirty); // -Z neighbor dirty
        Assert.False(south.IsDirty);

        // Test South border: local Z = 31 (world 16, 16, 31)
        ResetAllDirty();
        world.SetBlock(new Vec3i(16, 16, 31), _registry.GetId("stone"));
        Assert.True(center.IsDirty);
        Assert.True(south.IsDirty); // +Z neighbor dirty
        Assert.False(north.IsDirty);
    }

    [Fact]
    public void SetBlock_CornerVoxelDirstiesAllThreeOrthogonalNeighbors()
    {
        var world = new VoxelWorld(_registry, _bus);

        var center = world.GetOrCreateChunk(Vec3i.Zero);
        var left = world.GetOrCreateChunk(Vec3i.Left);
        var down = world.GetOrCreateChunk(Vec3i.Down);
        var north = world.GetOrCreateChunk(Vec3i.North);
        var right = world.GetOrCreateChunk(Vec3i.Right);

        center.IsDirty = false;
        left.IsDirty = false;
        down.IsDirty = false;
        north.IsDirty = false;
        right.IsDirty = false;

        // Edit corner at (0, 0, 0): touches Left (-X), Down (-Y), North (-Z)
        world.SetBlock(new Vec3i(0, 0, 0), _registry.GetId("stone"));

        Assert.True(center.IsDirty);
        Assert.True(left.IsDirty);
        Assert.True(down.IsDirty);
        Assert.True(north.IsDirty);
        Assert.False(right.IsDirty);
    }

    [Fact]
    public void ChunkLifecycle_MethodsWorkCorrectly()
    {
        var world = new VoxelWorld(_registry, _bus);
        var coord = new Vec3i(1, 2, 3);

        Assert.False(world.HasChunk(coord));
        Assert.Null(world.GetChunk(coord));

        var chunk = world.GetOrCreateChunk(coord);
        Assert.NotNull(chunk);
        Assert.True(world.HasChunk(coord));
        Assert.Same(chunk, world.GetChunk(coord));
        Assert.Equal(1, world.ChunkCount);

        var removed = world.RemoveChunk(coord);
        Assert.True(removed);
        Assert.False(world.HasChunk(coord));
        Assert.Equal(0, world.ChunkCount);

        var newChunk = new ChunkData(coord);
        world.AddOrUpdateChunk(newChunk);
        Assert.True(world.HasChunk(coord));
        Assert.Same(newChunk, world.GetChunk(coord));
    }
}
