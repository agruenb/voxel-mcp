namespace VoxelKing.Core.Tests.Navigation;

using System;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;
using Xunit;

public class AStarPathfinderTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, IWalkability Walkability) CreateTestWorld()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        var walkability = new Walkability(world);
        return (world, blocks, walkability);
    }

    [Fact]
    public void FindPath_StartEqualsGoal_ReturnsSinglePoint()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var result = AStarPathfinder.FindPath(walkability, pos, pos);

        Assert.True(result.Success);
        Assert.Single(result.Waypoints);
        Assert.Equal(pos, result.Waypoints[0]);
        Assert.Equal(0f, result.TotalCost);
    }

    [Fact]
    public void FindPath_StartNotWalkable_Fails()
    {
        var (_, _, walkability) = CreateTestWorld();
        var pos = new Vec3i(0, 1, 0); // Air beneath, not walkable

        var result = AStarPathfinder.FindPath(walkability, pos, new Vec3i(5, 1, 0));

        Assert.False(result.Success);
        Assert.Equal(PathFailureReason.StartNotWalkable, result.FailureReason);
    }

    [Fact]
    public void FindPath_FlatStraightLine_FindsDirectShortestPath()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor from X=0..10 at Y=0, Z=0
        for (int x = -1; x <= 11; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        var start = new Vec3i(0, 1, 0);
        var goal = new Vec3i(10, 1, 0);

        var result = AStarPathfinder.FindPath(walkability, start, goal);

        Assert.True(result.Success);
        Assert.Equal(start, result.Waypoints[0]);
        Assert.Equal(goal, result.Waypoints[^1]);
        Assert.Equal(11, result.Waypoints.Count); // 0 to 10 inclusive is 11 waypoints
        Assert.Equal(10f, result.TotalCost, 2);
    }

    [Fact]
    public void FindPath_NavigatesAroundWall()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // 11x11 floor at Y = 0 (X: -5..5, Z: -5..5)
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // 2-block high wall at X = 0, Z from -3 to 3 (leaving gap at Z=4 and Z=-4)
        for (int z = -3; z <= 3; z++)
        {
            world.SetBlock(0, 1, z, stoneId);
            world.SetBlock(0, 2, z, stoneId);
        }

        var start = new Vec3i(-2, 1, 0);
        var goal = new Vec3i(2, 1, 0);

        var result = AStarPathfinder.FindPath(walkability, start, goal);

        Assert.True(result.Success);
        Assert.Equal(start, result.Waypoints[0]);
        Assert.Equal(goal, result.Waypoints[^1]);

        // Path must not pass through the wall (X=0 with Z in [-3..3])
        foreach (var wp in result.Waypoints)
        {
            if (wp.X == 0)
            {
                Assert.True(Math.Abs(wp.Z) > 3, $"Path penetrated wall at {wp}!");
            }
        }
    }

    [Fact]
    public void FindPath_NavigatesStairsUpAndDown()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Create a 3-step staircase climbing from X=0 to X=3, then flat at X=4, then down to X=7
        // Step 0: floor Y=0, feet Y=1
        // Step 1: floor Y=1, feet Y=2
        // Step 2: floor Y=2, feet Y=3
        // Step 3: floor Y=3, feet Y=4
        // Flat 4: floor Y=3, feet Y=4
        // Step 5: floor Y=2, feet Y=3
        // Step 6: floor Y=1, feet Y=2
        // Step 7: floor Y=0, feet Y=1
        int[] floorHeights = [0, 1, 2, 3, 3, 2, 1, 0];

        for (int x = 0; x < floorHeights.Length; x++)
        {
            int h = floorHeights[x];
            for (int y = 0; y <= h; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    world.SetBlock(x, y, z, stoneId);
                }
            }
        }

        var start = new Vec3i(0, 1, 0);
        var goal = new Vec3i(7, 1, 0);

        var result = AStarPathfinder.FindPath(walkability, start, goal);

        Assert.True(result.Success);
        Assert.Equal(start, result.Waypoints[0]);
        Assert.Equal(goal, result.Waypoints[^1]);

        // Waypoints must climb up to Y=4 and back down to Y=1
        Assert.Contains(result.Waypoints, wp => wp.Y == 4);
    }

    [Fact]
    public void FindPath_DropsDownLedgeSafely()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // High plateau at X <= 2 (floor Y = 3)
        for (int x = 0; x <= 2; x++)
        {
            for (int y = 0; y <= 3; y++)
            {
                world.SetBlock(x, y, 0, stoneId);
            }
        }

        // Low ground at X >= 3 (floor Y = 1) -> 2-block drop from feet Y=4 to feet Y=2
        for (int x = 3; x <= 6; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                world.SetBlock(x, y, 0, stoneId);
            }
        }

        var start = new Vec3i(0, 4, 0);
        var goal = new Vec3i(5, 2, 0);

        var result = AStarPathfinder.FindPath(walkability, start, goal);

        Assert.True(result.Success);
        Assert.Equal(start, result.Waypoints[0]);
        Assert.Equal(goal, result.Waypoints[^1]);
    }

    [Fact]
    public void FindPath_UnreachableGoal_ReturnsPartialPath_WhenAllowed()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor at X from 0 to 5
        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        // Island at X = 20
        world.SetBlock(20, 0, 0, stoneId);

        var start = new Vec3i(0, 1, 0);
        var unreachableGoal = new Vec3i(20, 1, 0);

        // With AllowPartial = true
        var partialResult = AStarPathfinder.FindPath(walkability, start, unreachableGoal, new PathfindingOptions { AllowPartial = true });
        Assert.Equal(PathStatus.Partial, partialResult.Status);
        Assert.True(partialResult.HasPath);
        Assert.Equal(start, partialResult.Waypoints[0]);
        Assert.Equal(new Vec3i(5, 1, 0), partialResult.Waypoints[^1]); // Reached edge of floor

        // With AllowPartial = false
        var failedResult = AStarPathfinder.FindPath(walkability, start, unreachableGoal, new PathfindingOptions { AllowPartial = false });
        Assert.Equal(PathStatus.Failed, failedResult.Status);
        Assert.False(failedResult.HasPath);
    }

    [Fact]
    public void FindPath_BudgetExceeded_ReturnsPartialPath_OrFails()
    {
        var (world, blocks, walkability) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // 30x30 floor at Y = 0
        for (int x = 0; x <= 30; x++)
        {
            for (int z = 0; z <= 30; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        var start = new Vec3i(0, 1, 0);
        var goal = new Vec3i(25, 1, 25);

        // Budget of only 5 node expansions
        var result = AStarPathfinder.FindPath(walkability, start, goal, new PathfindingOptions
        {
            NodeBudget = 5,
            AllowPartial = true
        });

        Assert.Equal(PathStatus.Partial, result.Status);
        Assert.Equal(PathFailureReason.BudgetExceeded, result.FailureReason);
        Assert.True(result.NodesExpanded <= 5);
        Assert.True(result.HasPath);
    }
}
