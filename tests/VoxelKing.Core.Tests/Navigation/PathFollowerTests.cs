namespace VoxelKing.Core.Tests.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;
using Xunit;

public class PathFollowerTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus) CreateTestWorld()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        return (world, blocks, bus);
    }

    [Fact]
    public void PathFollower_SteersNpcTowardsWaypoints()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "Runner", new Vec3(0.5f, 1.0f, 0.5f));
        using var follower = new PathFollower(npc, world, bus);

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(1, 1, 0),
            new(2, 1, 0)
        };

        follower.FollowPath(path);
        Assert.Equal(PathFollowerStatus.Following, follower.Status);

        // Update 1 tick
        follower.Update(0.1f);

        // Velocity should point along +X towards (1, 1, 0)
        Assert.True(npc.Velocity.X > 0f);
        Assert.Equal(0f, npc.Velocity.Z);
    }

    [Fact]
    public void PathFollower_ReachesDestination_AndMarksArrived()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        for (int x = 0; x <= 2; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "Finisher", new Vec3(0.5f, 1.0f, 0.5f));
        using var follower = new PathFollower(npc, world, bus)
        {
            ArrivalThreshold = 0.5f
        };

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(1, 1, 0)
        };

        follower.FollowPath(path);

        // Place NPC directly at destination
        npc.Position = new Vec3(1.5f, 1.0f, 0.5f);
        follower.Update(0.1f);

        Assert.Equal(PathFollowerStatus.Arrived, follower.Status);
        Assert.Equal(0f, npc.Velocity.X);
        Assert.Equal(0f, npc.Velocity.Z);
    }

    [Fact]
    public void PathFollower_Jumps_WhenApproachingHigherWaypoint()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(1, 1, 0, stoneId); // 1-block step up at X=1

        var npc = new NpcState(new EntityId(1), "Jumper", new Vec3(0.5f, 1.0f, 0.5f))
        {
            IsOnGround = true
        };

        using var follower = new PathFollower(npc, world, bus);

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(1, 2, 0) // Step up to Y=2
        };

        follower.FollowPath(path);
        follower.Update(0.1f);

        // NPC should trigger jump impulse
        Assert.True(npc.Velocity.Y > 0f);
    }

    [Fact]
    public void PathFollower_DetectsStuckCondition_WhenNoProgress()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(1, 0, 0, stoneId);

        var npc = new NpcState(new EntityId(1), "StuckGuy", new Vec3(0.5f, 1.0f, 0.5f));
        using var follower = new PathFollower(npc, world, bus)
        {
            StuckTimeout = 0.5f // 0.5s timeout for test
        };

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(5, 1, 0)
        };

        follower.FollowPath(path);
        Assert.Equal(PathFollowerStatus.Following, follower.Status);

        // Simulate 0.6 seconds where NPC does not change position
        follower.Update(0.3f);
        Assert.Equal(PathFollowerStatus.Following, follower.Status);

        follower.Update(0.3f);
        Assert.Equal(PathFollowerStatus.Stuck, follower.Status);
        Assert.Equal(0f, npc.Velocity.X);
    }

    [Fact]
    public void PathFollower_Invalidates_WhenBlockPlacedInPath()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "BlockedGuy", new Vec3(0.5f, 1.0f, 0.5f));
        using var follower = new PathFollower(npc, world, bus);

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(1, 1, 0),
            new(2, 1, 0),
            new(3, 1, 0)
        };

        follower.FollowPath(path);
        Assert.Equal(PathFollowerStatus.Following, follower.Status);

        // Place a solid wall right in the waypoint at (2, 1, 0)
        world.SetBlock(2, 1, 0, stoneId);

        // Follower should receive event and mark path invalidated
        Assert.Equal(PathFollowerStatus.Invalidated, follower.Status);
    }

    [Fact]
    public void PathFollower_Invalidates_WhenFloorBrokenUnderPath()
    {
        var (world, blocks, bus) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");
        ushort airId = blocks.AirId;

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "PitGuy", new Vec3(0.5f, 1.0f, 0.5f));
        using var follower = new PathFollower(npc, world, bus);

        var path = new List<Vec3i>
        {
            new(0, 1, 0),
            new(1, 1, 0),
            new(2, 1, 0)
        };

        follower.FollowPath(path);
        Assert.Equal(PathFollowerStatus.Following, follower.Status);

        // Break the ground beneath waypoint (2, 1, 0), i.e. at (2, 0, 0)
        world.SetBlock(2, 0, 0, airId);

        Assert.Equal(PathFollowerStatus.Invalidated, follower.Status);
    }
}
