namespace VoxelKing.Core.Tests.Voxel;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class ChunkStreamPlannerTests
{
    [Fact]
    public void Constructor_SetsDefaultConfig()
    {
        var planner = new ChunkStreamPlanner();
        Assert.Equal(4, planner.Config.HorizontalRadius);
        Assert.Equal(1, planner.Config.Hysteresis);
        Assert.Equal(5, planner.Config.UnloadRadius);
        Assert.Equal(0, planner.Config.MinChunkY);
        Assert.Equal(7, planner.Config.MaxChunkY);
    }

    [Fact]
    public void GetChunksInRadius_OrdersClosestFirst()
    {
        var config = new ChunkStreamPlannerConfig(horizontalRadius: 2, hysteresis: 1, minChunkY: 2, maxChunkY: 2);
        var planner = new ChunkStreamPlanner(config);

        var center = new Vec3i(0, 2, 0);
        var chunks = planner.GetChunksInLoadRadius(center);

        Assert.NotEmpty(chunks);
        // The first chunk should be the center chunk itself
        Assert.Equal(center, chunks[0]);

        // Distances must be monotonically non-decreasing
        long prevDist = 0;
        foreach (var chunk in chunks)
        {
            long dist = center.DistanceSquared(chunk);
            Assert.True(dist >= prevDist);
            prevDist = dist;
        }
    }

    [Fact]
    public void GetChunksInRadius_ContainsOnlyWithinRadius()
    {
        int radius = 3;
        var config = new ChunkStreamPlannerConfig(horizontalRadius: radius, hysteresis: 1, minChunkY: 0, maxChunkY: 1);
        var planner = new ChunkStreamPlanner(config);

        var center = new Vec3i(5, 0, 5);
        var chunks = planner.GetChunksInLoadRadius(center);

        foreach (var chunk in chunks)
        {
            int dx = chunk.X - center.X;
            int dz = chunk.Z - center.Z;
            Assert.True((dx * dx) + (dz * dz) <= radius * radius);
            Assert.InRange(chunk.Y, 0, 1);
        }
    }

    [Fact]
    public void GetChunksToUnload_HonorsHysteresis()
    {
        // HorizontalRadius = 3, Hysteresis = 2 => UnloadRadius = 5
        var config = new ChunkStreamPlannerConfig(horizontalRadius: 3, hysteresis: 2, minChunkY: 0, maxChunkY: 0);
        var planner = new ChunkStreamPlanner(config);

        var center = new Vec3i(0, 0, 0);

        var activeChunks = new List<Vec3i>
        {
            new(0, 0, 0),   // dist 0 -> keep
            new(3, 0, 0),   // dist 3 (<= 3) -> keep
            new(4, 0, 0),   // dist 4 (> 3, but <= 5 due to hysteresis) -> KEEP!
            new(5, 0, 0),   // dist 5 (<= 5) -> keep
            new(6, 0, 0),   // dist 6 (> 5) -> UNLOAD!
            new(0, -1, 0),  // Y < min -> UNLOAD!
            new(0, 1, 0),   // Y > max -> UNLOAD!
        };

        var toUnload = planner.GetChunksToUnload(center, activeChunks);

        Assert.Contains(new Vec3i(6, 0, 0), toUnload);
        Assert.Contains(new Vec3i(0, -1, 0), toUnload);
        Assert.Contains(new Vec3i(0, 1, 0), toUnload);

        Assert.DoesNotContain(new Vec3i(0, 0, 0), toUnload);
        Assert.DoesNotContain(new Vec3i(3, 0, 0), toUnload);
        Assert.DoesNotContain(new Vec3i(4, 0, 0), toUnload);
        Assert.DoesNotContain(new Vec3i(5, 0, 0), toUnload);
    }

    [Fact]
    public void IsWithinLoadRadius_And_IsBeyondUnloadRadius_WorkCorrectly()
    {
        var config = new ChunkStreamPlannerConfig(horizontalRadius: 2, hysteresis: 1, minChunkY: 0, maxChunkY: 3);
        var planner = new ChunkStreamPlanner(config);
        var center = new Vec3i(0, 1, 0);

        // At distance 2 (on load boundary)
        var atBoundary = new Vec3i(2, 1, 0);
        Assert.True(planner.IsWithinLoadRadius(atBoundary, center));
        Assert.False(planner.IsBeyondUnloadRadius(atBoundary, center));

        // At distance 3 (in hysteresis buffer)
        var inBuffer = new Vec3i(3, 1, 0);
        Assert.False(planner.IsWithinLoadRadius(inBuffer, center));
        Assert.False(planner.IsBeyondUnloadRadius(inBuffer, center));

        // At distance 4 (beyond unload radius 2+1=3)
        var beyond = new Vec3i(4, 1, 0);
        Assert.False(planner.IsWithinLoadRadius(beyond, center));
        Assert.True(planner.IsBeyondUnloadRadius(beyond, center));
    }

    [Fact]
    public void GenerationRadius_ContainsAllCardinalNeighborsOfLoadRadius()
    {
        var config = new ChunkStreamPlannerConfig(horizontalRadius: 3, hysteresis: 1, minChunkY: 0, maxChunkY: 3);
        var planner = new ChunkStreamPlanner(config);
        var center = new Vec3i(0, 1, 0);

        var loadChunks = planner.GetChunksInLoadRadius(center).ToHashSet();
        var genChunks = planner.GetChunksInGenerationRadius(center).ToHashSet();

        // Every chunk in load radius must have all its horizontal cardinal neighbors present in generation radius
        foreach (var chunk in loadChunks)
        {
            var neighbors = new[]
            {
                chunk + Vec3i.Left,
                chunk + Vec3i.Right,
                chunk + Vec3i.North,
                chunk + Vec3i.South,
            };

            foreach (var neighbor in neighbors)
            {
                Assert.Contains(neighbor, genChunks);
            }
        }
    }

    [Fact]
    public void MovingFocus_UpdatesLoadAndUnloadSetsSeamlessly()
    {
        var config = new ChunkStreamPlannerConfig(horizontalRadius: 3, hysteresis: 1, minChunkY: 0, maxChunkY: 0);
        var planner = new ChunkStreamPlanner(config);

        var startPos = new Vec3i(0, 0, 0);
        var initialLoaded = planner.GetChunksInLoadRadius(startPos).ToHashSet();

        // Simulate flying 10 chunks to the east
        var movedPos = new Vec3i(10, 0, 0);
        var newRequired = planner.GetChunksInLoadRadius(movedPos);
        var toUnload = planner.GetChunksToUnload(movedPos, initialLoaded);

        // Chunks at origin must be marked for unload since dist=10 > 3+1=4
        Assert.Contains(startPos, toUnload);

        // New chunks must contain the new center
        Assert.Contains(movedPos, newRequired);
    }
}
