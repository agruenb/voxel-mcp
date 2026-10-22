namespace VoxelKing.Core.Tests.Npc;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Arbiter;
using VoxelKing.Core.Voxel;
using Xunit;

public class ArbiterTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus, PathfindingService PathService) CreateTestEnv()
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var pathService = new PathfindingService(world);
        return (world, blocks, bus, pathService);
    }

    #region Arbiter Priority and Coordination Tests

    [Fact]
    public void Arbiter_RunsIdle_WhenTaskHasNoActiveTask()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer();
        var task = new MockTaskLayer { HasActiveTask = false };
        var idle = new MockIdleLayer();

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.Tick(0.1f);

        Assert.Equal(BehaviourLayerKind.Idle, arbiter.ActiveLayer);
        Assert.Equal(1, idle.TickCount);
        Assert.Equal(0, task.TickCount);
    }

    [Fact]
    public void Arbiter_RunsTask_WhenTaskHasActiveTask()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer();
        var task = new MockTaskLayer { HasActiveTask = true };
        var idle = new MockIdleLayer();

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.Tick(0.1f);

        Assert.Equal(BehaviourLayerKind.Task, arbiter.ActiveLayer);
        Assert.Equal(1, task.TickCount);
        Assert.Equal(0, idle.TickCount);
    }

    [Fact]
    public void Arbiter_PreemptsTask_WhenReflexTriggers()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer { IsTriggered = true, Reason = "drop_hazard" };
        var task = new MockTaskLayer { HasActiveTask = true };
        var idle = new MockIdleLayer();

        var action = new WaitAction(npc, 5.0f);
        action.Start();
        npc.CurrentAction = action;

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.Tick(0.1f);

        Assert.Equal(BehaviourLayerKind.Reflex, arbiter.ActiveLayer);
        Assert.True(task.WasInterrupted);
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
        Assert.Equal(0, task.TickCount);
        Assert.Equal(0, idle.TickCount);
    }

    [Fact]
    public void Arbiter_ResumesTaskOrIdle_WhenReflexClears()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer { IsTriggered = true, Reason = "water" };
        var task = new MockTaskLayer { HasActiveTask = false };
        var idle = new MockIdleLayer();

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.Tick(0.1f);
        Assert.Equal(BehaviourLayerKind.Reflex, arbiter.ActiveLayer);

        // Reflex condition resolves
        reflex.IsTriggered = false;
        arbiter.Tick(0.1f);

        Assert.Equal(BehaviourLayerKind.Idle, arbiter.ActiveLayer);
        Assert.Equal(1, idle.TickCount);
    }

    [Fact]
    public void Arbiter_PhysicsTick_EvaluatesReflexHazard_AndPreempts()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer { IsTriggered = true, Reason = "drop_hazard" };
        var task = new MockTaskLayer { HasActiveTask = true };
        var idle = new MockIdleLayer();

        var action = new WaitAction(npc, 5.0f);
        action.Start();
        npc.CurrentAction = action;

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.PhysicsTick(1f / 60f);

        Assert.Equal(BehaviourLayerKind.Reflex, arbiter.ActiveLayer);
        Assert.True(task.WasInterrupted);
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
    }

    [Fact]
    public void Arbiter_Reset_ClearsActiveLayerAndChildLayers()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0f, 1f, 0f));
        var reflex = new MockReflexLayer();
        var task = new MockTaskLayer { HasActiveTask = true };
        var idle = new MockIdleLayer();

        var arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        arbiter.Tick(0.1f);
        Assert.Equal(BehaviourLayerKind.Task, arbiter.ActiveLayer);

        arbiter.Reset();
        Assert.Equal(BehaviourLayerKind.None, arbiter.ActiveLayer);
        Assert.True(reflex.WasReset);
        Assert.True(idle.WasReset);
    }

    #endregion

    #region ReflexLayer Tests

    [Fact]
    public void Reflex_Suffocation_RelievesNpcInsideSolidBlock()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Build a ground floor at Y=0
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                world.SetBlock(x, 0, z, stone);
            }
        }

        // Place a solid stone block at (0, 1, 0) and (0, 2, 0) embedding the NPC
        world.SetBlock(0, 1, 0, stone);
        world.SetBlock(0, 2, 0, stone);

        var npc = new NpcState(new EntityId(1), "SuffocatingNpc", new Vec3(0.5f, 1.0f, 0.5f));
        var reflex = new ReflexLayer(npc, world);

        bool triggered = reflex.CheckReflex(0.1f);

        Assert.True(triggered);
        Assert.True(reflex.IsActive);
        Assert.Equal("suffocation", reflex.CurrentReflexReason);

        // Verify NPC stepped out of the suffocating block to a non-solid location
        var newFeet = npc.Position.FloorToInt();
        Assert.NotEqual(new Vec3i(0, 1, 0), newFeet);
        Assert.False(Walkability.IsSolid(world, newFeet));
    }

    [Fact]
    public void Reflex_InWater_AppliesUpwardImpulseAndSteersToDryLand()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort water = blocks.GetId("water");
        ushort stone = blocks.GetId("stone");

        // Ground pool: stone at Y=0, water at (0,1,0), dry ground at (1,1,0)
        world.SetBlock(0, 0, 0, stone);
        world.SetBlock(1, 0, 0, stone);
        world.SetBlock(0, 1, 0, water);
        // (1,1,0) is air above stone -> walkable dry land

        var npc = new NpcState(new EntityId(1), "Swimmer", new Vec3(0.5f, 1.0f, 0.5f));
        var reflex = new ReflexLayer(npc, world);

        bool triggered = reflex.CheckReflex(0.1f);

        Assert.True(triggered);
        Assert.True(reflex.IsActive);
        Assert.Equal("water", reflex.CurrentReflexReason);
        Assert.True(npc.Velocity.Y >= 5.0f);
        Assert.True(npc.Velocity.X > 0f); // Steers towards dry ground at +X
    }

    [Fact]
    public void Reflex_AvoidsWaterAhead_WhenMovingTowardsWater()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort water = blocks.GetId("water");
        ushort stone = blocks.GetId("stone");

        // Stone at (0,0,0), water at (1,1,0)
        world.SetBlock(0, 0, 0, stone);
        world.SetBlock(1, 1, 0, water);

        var npc = new NpcState(new EntityId(1), "Walker", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Velocity = new Vec3(3.0f, 0f, 0f); // Moving towards +X water
        var reflex = new ReflexLayer(npc, world);

        bool triggered = reflex.CheckReflex(0.1f);

        Assert.True(triggered);
        Assert.True(reflex.IsActive);
        Assert.Equal("water_hazard", reflex.CurrentReflexReason);
        Assert.Equal(0f, npc.Velocity.X); // Forward velocity stopped
    }

    [Fact]
    public void Reflex_AvoidsDropGreaterThan3_WhenApproachingCliff()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Stone at (0, 10, 0). Next cell at +X is (1, 10, 0) with a sheer cliff: ground is at Y=0 (drop of 10 > 3)
        world.SetBlock(0, 10, 0, stone);
        world.SetBlock(1, 0, 0, stone); // 10 blocks down

        var npc = new NpcState(new EntityId(1), "CliffWalker", new Vec3(0.5f, 11.0f, 0.5f));
        npc.Velocity = new Vec3(3.0f, 0f, 0f); // Moving towards cliff at +X
        var reflex = new ReflexLayer(npc, world);

        bool triggered = reflex.CheckReflex(0.1f);

        Assert.True(triggered);
        Assert.True(reflex.IsActive);
        Assert.Equal("drop_hazard", reflex.CurrentReflexReason);
        Assert.Equal(0f, npc.Velocity.X); // Forward velocity halted
    }

    [Fact]
    public void Reflex_PermitsSafeDropLessThanOrEqualTo3()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Stone at (0, 10, 0). Next cell at +X has ground at Y=8 (drop of 2 blocks, walkable)
        world.SetBlock(0, 10, 0, stone);
        world.SetBlock(1, 8, 0, stone);

        var npc = new NpcState(new EntityId(1), "StairWalker", new Vec3(0.5f, 11.0f, 0.5f));
        npc.Velocity = new Vec3(3.0f, 0f, 0f);
        var reflex = new ReflexLayer(npc, world);

        bool triggered = reflex.CheckReflex(0.1f);

        Assert.False(triggered);
        Assert.False(reflex.IsActive);
        Assert.Equal(3.0f, npc.Velocity.X); // Moving allowed
    }

    [Fact]
    public void Reflex_Unstuck_TriggersWhenNoProgressMadeOverTimeout()
    {
        var (world, blocks, _, _) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stone);

        var npc = new NpcState(new EntityId(1), "StuckBot", new Vec3(0.5f, 1.0f, 0.5f));
        npc.IsOnGround = true;
        npc.IsOnWall = true; // Blocked against a wall
        var reflex = new ReflexLayer(npc, world)
        {
            StuckTimeout = 1.0f
        };

        // Advance 0.5s -> not yet stuck
        bool r1 = reflex.CheckReflex(0.5f);
        Assert.False(r1);

        // Advance another 0.6s -> total 1.1s > StuckTimeout 1.0s
        bool r2 = reflex.CheckReflex(0.6f);
        Assert.True(r2);
        Assert.Equal("stuck", reflex.CurrentReflexReason);
        Assert.True(npc.Velocity.Y > 0f); // Jump was initiated
    }

    #endregion

    #region IdleLayer Tests

    [Fact]
    public void Idle_WandersWithin8BlocksOfHome()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Build flat platform around (0, 0, 0) with radius 12
        for (int x = -12; x <= 12; x++)
        {
            for (int z = -12; z <= 12; z++)
            {
                world.SetBlock(x, 0, z, stone);
            }
        }

        var npc = new NpcState(new EntityId(1), "Wanderer", new Vec3(0.5f, 1.0f, 0.5f))
        {
            Home = new Vec3(0.5f, 1.0f, 0.5f)
        };

        var idle = new IdleLayer(npc, world, pathService, eventBus: bus)
        {
            MinWaitTime = 0.0f,
            MaxWaitTime = 0.0f // Trigger wander immediately
        };
        idle.Reset();

        idle.Tick(0.1f);

        Assert.Equal(IdleState.Wandering, idle.State);
        Assert.NotNull(idle.CurrentWanderGoal);
        Assert.NotNull(npc.CurrentAction);

        var goal = idle.CurrentWanderGoal.Value;
        float distFromHome = MathF.Sqrt(
            MathF.Pow(goal.X + 0.5f - npc.Home.X, 2) +
            MathF.Pow(goal.Z + 0.5f - npc.Home.Z, 2));

        Assert.True(distFromHome <= 8.0f, $"Wander destination {goal} is {distFromHome:F2} blocks from home (must be <= 8)");
    }

    [Fact]
    public void Idle_LooksAtNearbyPlayer()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var registry = new EntityRegistry();

        var npc = new NpcState(new EntityId(1), "Watcher", new Vec3(0.5f, 1.0f, 0.5f));
        registry.Register(npc);

        var player = new PlayerEntity(new EntityId(99), "Player", new Vec3(3.5f, 1.0f, 0.5f), new Inventory(36));
        registry.Register(player);

        var idle = new IdleLayer(npc, world, pathService, registry, bus)
        {
            MinWaitTime = 5.0f,
            MaxWaitTime = 5.0f,
            PlayerNoticeRadius = 6.0f
        };

        idle.Tick(0.1f);

        Assert.Equal(IdleState.LookingAtPlayer, idle.State);
        var facing = npc.GetFacingDirection();
        Assert.True(facing.X > 0.8f, $"Facing direction X should point towards player at +X, got {facing.X}");
    }

    [Fact]
    public void Idle_RespectsExternalRunningAction()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        var npc = new NpcState(new EntityId(1), "BusyBot", new Vec3(0.5f, 1.0f, 0.5f));

        var externalAction = new WaitAction(npc, 10.0f);
        externalAction.Start();
        npc.CurrentAction = externalAction;

        var idle = new IdleLayer(npc, world, pathService, eventBus: bus)
        {
            MinWaitTime = 0.0f,
            MaxWaitTime = 0.0f
        };

        idle.Tick(0.1f);

        // Idle must not overwrite or cancel external action
        Assert.Same(externalAction, npc.CurrentAction);
        Assert.True(externalAction.Status.IsRunning);
    }

    #endregion

    #region WalkAndMineAction Tests

    [Fact]
    public void WalkAndMine_MinesImmediately_WhenWithinReachDistance()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");
        world.SetBlock(1, 1, 0, stone); // Block within 2 blocks reach

        var npc = new NpcState(new EntityId(1), "Miner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new WalkAndMineAction(
            npc,
            new Vec3i(1, 1, 0),
            world,
            blocks,
            pathfindingService: pathService,
            eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);

        // Advance mining time (stone takes ~1.5s)
        for (int i = 0; i < 30; i++)
        {
            action.Tick(0.1f);
            if (action.Status.IsCompleted) break;
        }

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(0, world.GetBlock(1, 1, 0)); // Block was broken
    }

    [Fact]
    public void WalkAndMine_NavigatesNear_ThenMinesBlock()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Build flat floor from 0 to 6
        for (int x = 0; x <= 6; x++)
        {
            world.SetBlock(x, 0, 0, stone);
        }

        // Target block at (6, 1, 0) — 6 blocks away, outside initial reach (reach <= 4.0)
        world.SetBlock(6, 1, 0, stone);

        var npc = new NpcState(new EntityId(1), "LongDistanceMiner", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new WalkAndMineAction(
            npc,
            new Vec3i(6, 1, 0),
            world,
            blocks,
            pathfindingService: pathService,
            eventBus: bus);

        action.Start();
        Assert.True(action.Status.IsRunning);

        // Tick movement and mining over simulation steps
        for (int i = 0; i < 60; i++)
        {
            if (npc.Velocity.LengthSquared() > 1e-4f)
            {
                npc.Position += npc.Velocity * 0.1f;
            }

            action.Tick(0.1f);
            if (action.Status.IsCompleted) break;
        }

        Assert.True(action.Status.IsSucceeded);
        Assert.Equal(0, world.GetBlock(6, 1, 0)); // Stone mined
    }

    #endregion

    #region Full Manager Idle Simulation Test

    [Fact]
    public void NpcManager_MultipleNpcs_IdleWanderNaturally_WithoutCrashingOrGettingStuck()
    {
        var (world, blocks, bus, pathService) = CreateTestEnv();
        ushort stone = blocks.GetId("stone");

        // Create a 25x25 platform around origin
        for (int x = -12; x <= 12; x++)
        {
            for (int z = -12; z <= 12; z++)
            {
                world.SetBlock(x, 0, z, stone);
            }
        }

        var registry = new EntityRegistry();
        var manager = new NpcManager(
            registry,
            voxelWorld: world,
            eventBus: bus,
            pathfindingService: pathService)
        {
            EnableArbiters = true
        };

        // Spawn 3 NPCs around origin
        var npc1 = manager.SpawnNpc("Alice", new Vec3(0.5f, 1.0f, 0.5f));
        var npc2 = manager.SpawnNpc("Bob", new Vec3(2.5f, 1.0f, 2.5f));
        var npc3 = manager.SpawnNpc("Charlie", new Vec3(-2.5f, 1.0f, -2.5f));

        Assert.NotNull(npc1.Arbiter);
        Assert.NotNull(npc2.Arbiter);
        Assert.NotNull(npc3.Arbiter);

        // Run simulation for 100 ticks (10 simulated seconds)
        for (int i = 0; i < 100; i++)
        {
            manager.Tick(0.1f);
        }

        // Verify NPCs are alive, active, and staying within 8 blocks of their home
        foreach (var npc in manager.ActiveNpcs)
        {
            Assert.True(npc.IsActive);
            Assert.False(npc.IsDead);
            float distHome = MathF.Sqrt(
                MathF.Pow(npc.Position.X - npc.Home.X, 2) +
                MathF.Pow(npc.Position.Z - npc.Home.Z, 2));

            Assert.True(distHome <= 9.0f, $"NPC {npc.Name} drifted {distHome:F2} blocks from home (expected <= 9)");
        }
    }

    #endregion

    #region Mock Classes

    private sealed class MockReflexLayer : IReflexLayer
    {
        public bool IsActive => IsTriggered;
        public string? CurrentReflexReason => Reason;
        public bool IsTriggered { get; set; }
        public string? Reason { get; set; }
        public bool WasReset { get; private set; }

        public bool CheckReflex(float dt) => IsTriggered;

        public void Reset()
        {
            WasReset = true;
            IsTriggered = false;
            Reason = null;
        }
    }

    private sealed class MockTaskLayer : ITaskLayer
    {
        public bool HasActiveTask { get; set; }
        public int TickCount { get; private set; }
        public bool WasInterrupted { get; private set; }

        public void Tick(float dt) => TickCount++;

        public void Interrupt() => WasInterrupted = true;
    }

    private sealed class MockIdleLayer : IIdleLayer
    {
        public IdleState State { get; set; } = IdleState.Waiting;
        public Vec3i? CurrentWanderGoal { get; set; }
        public float WanderRadius { get; set; } = 8.0f;
        public float MinWaitTime { get; set; } = 2.0f;
        public float MaxWaitTime { get; set; } = 4.0f;
        public float PlayerNoticeRadius { get; set; } = 6.0f;
        public int TickCount { get; private set; }
        public bool WasReset { get; private set; }

        public void Tick(float dt) => TickCount++;

        public void Reset()
        {
            WasReset = true;
            State = IdleState.Waiting;
            CurrentWanderGoal = null;
        }
    }

    #endregion
}
