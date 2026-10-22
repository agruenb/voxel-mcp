namespace VoxelKing.Core.Tests.Npc.Actions;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using Xunit;

public class NavigationActionTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus, PathfindingService PathService) CreateTestEnv()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var pathService = new PathfindingService(world);
        return (world, blocks, bus, pathService);
    }

    [Fact]
    public void GoToAction_SucceedsImmediatelyIfAlreadyAtGoal()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(2.5f, 1.0f, 2.5f));

        var action = new GoToAction(npc, new Vec3i(2, 1, 2), pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void GoToAction_ToleranceArrivalSucceeds()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(2.0f, 1.0f, 2.0f));

        var opts = new GoToOptions { Tolerance = 2.0f };
        var action = new GoToAction(npc, new Vec3i(3, 1, 2), options: opts, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void GoToAction_NavigatesAndSucceeds()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Build a flat walking path from (0,0,0) to (5,0,0)
        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stone);
        }

        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new GoToAction(npc, new Vec3i(3, 1, 0), pathfindingService: pathService, world: world, eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);
        Assert.NotNull(npc.PathFollower);

        // Move NPC to target waypoint
        npc.Position = new Vec3(3.5f, 1.0f, 0.5f);
        action.Tick(0.1f);

        Assert.True(action.Status.IsSucceeded);
        Assert.Null(npc.PathFollower);
    }

    [Fact]
    public void GoToAction_FailsUnreachableWhenNoPathExists()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stone); // Walkable cell at (0, 1, 0)
        world.SetBlock(10, 0, 10, stone); // Isolated walkable cell at (10, 1, 10)

        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));
        var opts = new GoToOptions { AllowPartial = false };
        var action = new GoToAction(npc, new Vec3i(10, 1, 10), options: opts, pathfindingService: pathService, world: world, eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Unreachable, action.Status.FailureReason);
    }

    [Fact]
    public void GoToAction_FailsStuckWhenFollowerStuck()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stone);
        }

        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new GoToAction(npc, new Vec3i(5, 1, 0), pathfindingService: pathService, world: world, eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);

        // Do not update NPC position; simulate being stuck for > stuck timeout (2.0s)
        for (int i = 0; i < 25; i++)
        {
            action.Tick(0.1f);
            if (action.Status.IsFailed) break;
        }

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Stuck, action.Status.FailureReason);
    }

    [Fact]
    public void GoToAction_FailsTimeoutWhenTimeExceeded()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        for (int x = 0; x <= 10; x++)
        {
            world.SetBlock(x, 0, 0, stone);
        }

        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));
        var opts = new GoToOptions { Timeout = 1.0f };
        var action = new GoToAction(npc, new Vec3i(10, 1, 0), options: opts, pathfindingService: pathService, world: world, eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Tick(1.5f);
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Timeout, action.Status.FailureReason);
    }

    [Fact]
    public void GoToAction_FailsInterruptedWhenCancelled()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stone);
        }

        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new GoToAction(npc, new Vec3i(5, 1, 0), pathfindingService: pathService, world: world, eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Cancel();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
        Assert.Null(npc.PathFollower);
    }

    [Fact]
    public void GoNearAction_SucceedsWhenWithinDistance()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new GoNearAction(npc, new Vec3i(2, 1, 0), dist: 2.5f, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void GoNearAction_FailsInvalidTargetWhenNegativeDistance()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "NPC", new Vec3(0.5f, 1.0f, 0.5f));

        var action = new GoNearAction(npc, new Vec3i(2, 1, 0), dist: -1f, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void FollowAction_SucceedsAfterDuration()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();

        var followerNpc = new NpcState(new EntityId(1), "Follower", new Vec3(0.5f, 1.0f, 0.5f));
        var targetNpc = new NpcState(new EntityId(2), "Target", new Vec3(1.0f, 1.0f, 0.5f));

        registry.Register(followerNpc);
        registry.Register(targetNpc);

        var action = new FollowAction(followerNpc, targetNpc.Id, dist: 2.0f, seconds: 1.0f, registry: registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Tick(1.2f);
        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void FollowAction_FailsInvalidTargetOnSelfOrMissing()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();
        var npc = new NpcState(new EntityId(1), "Follower", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        // Cannot follow self
        var actionSelf = new FollowAction(npc, npc.Id, dist: 2f, seconds: 5f, registry: registry);
        actionSelf.Start();
        Assert.True(actionSelf.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, actionSelf.Status.FailureReason);

        // Cannot follow missing entity
        var actionMissing = new FollowAction(npc, new EntityId(999), dist: 2f, seconds: 5f, registry: registry);
        actionMissing.Start();
        Assert.True(actionMissing.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, actionMissing.Status.FailureReason);
    }

    [Fact]
    public void FollowAction_FailsTargetGoneWhenTargetDespawns()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();

        var followerNpc = new NpcState(new EntityId(1), "Follower", new Vec3(0.5f, 1.0f, 0.5f));
        var targetNpc = new NpcState(new EntityId(2), "Target", new Vec3(1.0f, 1.0f, 0.5f));

        registry.Register(followerNpc);
        registry.Register(targetNpc);

        var action = new FollowAction(followerNpc, targetNpc.Id, dist: 2.0f, seconds: 5.0f, registry: registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        Assert.True(action.Status.IsRunning);

        // Target despawns
        registry.Unregister(targetNpc.Id);

        action.Tick(0.1f);
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.TargetGone, action.Status.FailureReason);
    }

    [Fact]
    public void FollowAction_FailsInterruptedWhenCancelled()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();

        var followerNpc = new NpcState(new EntityId(1), "Follower", new Vec3(0.5f, 1.0f, 0.5f));
        var targetNpc = new NpcState(new EntityId(2), "Target", new Vec3(1.0f, 1.0f, 0.5f));

        registry.Register(followerNpc);
        registry.Register(targetNpc);

        var action = new FollowAction(followerNpc, targetNpc.Id, dist: 2.0f, seconds: 0f, registry: registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        Assert.True(action.Status.IsRunning);

        action.Cancel();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
    }

    [Fact]
    public void FollowAction_FailsUnreachableWhenNoPath()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();

        ushort stone = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stone);
        world.SetBlock(20, 0, 20, stone);

        var followerNpc = new NpcState(new EntityId(1), "Follower", new Vec3(0.5f, 1.0f, 0.5f));
        var targetNpc = new NpcState(new EntityId(2), "Target", new Vec3(20.5f, 1.0f, 20.5f));

        registry.Register(followerNpc);
        registry.Register(targetNpc);

        var action = new FollowAction(followerNpc, targetNpc.Id, dist: 1.0f, seconds: 10f, registry: registry, pathfindingService: pathService, world: world, eventBus: bus);
        action.Start();
        action.Tick(0.1f);

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Unreachable, action.Status.FailureReason);
    }
}

