namespace VoxelKing.Core.Tests.Npc;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using Xunit;

public class TickStaggeringTests
{
    [Fact]
    public void Exactly_6_Npcs_At_10Hz_Spread_Exactly_1_Per_Tick_Over_6_Ticks()
    {
        var registry = new EntityRegistry();
        // 60 Hz physics / 10 Hz brain -> interval = 6 ticks
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 6; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // Each of the 6 buckets should have exactly 1 NPC
        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            Assert.Equal(1, manager.GetScheduledNpcCount(NpcLodTier.Normal, phase));
        }

        // Track how many NPCs tick per frame
        var ticksPerStep = new List<List<EntityId>>();
        manager.NpcBrainTicked += npc =>
        {
            ticksPerStep.Last().Add(npc.Id);
        };

        for (int step = 0; step < 6; step++)
        {
            ticksPerStep.Add(new List<EntityId>());
            manager.StepBrainTick();
        }

        // In every single tick, exactly 1 NPC should have ticked
        for (int step = 0; step < 6; step++)
        {
            Assert.Single(ticksPerStep[step]);
        }

        // Over the 6 ticks, all 6 distinct NPCs should have ticked exactly once
        var allTicked = ticksPerStep.SelectMany(x => x).ToList();
        Assert.Equal(6, allTicked.Count);
        Assert.Equal(6, allTicked.Distinct().Count());

        foreach (var npc in npcs)
        {
            Assert.Equal(1, npc.BrainTickCount);
        }
    }

    [Fact]
    public void Exactly_12_Npcs_At_10Hz_Spread_Exactly_2_Per_Tick_Over_6_Ticks()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 12; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // Each of the 6 buckets should have exactly 2 NPCs
        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            Assert.Equal(2, manager.GetScheduledNpcCount(NpcLodTier.Normal, phase));
        }

        var tickedInStep = new List<List<EntityId>>();
        manager.NpcBrainTicked += npc =>
        {
            tickedInStep.Last().Add(npc.Id);
        };

        for (int step = 0; step < 6; step++)
        {
            tickedInStep.Add(new List<EntityId>());
            manager.StepBrainTick();
        }

        // Exactly 2 NPCs per step
        for (int step = 0; step < 6; step++)
        {
            Assert.Equal(2, tickedInStep[step].Count);
        }

        // Over 6 ticks, each of the 12 NPCs ticked exactly once
        foreach (var npc in npcs)
        {
            Assert.Equal(1, npc.BrainTickCount);
        }

        // Run another 6 ticks -> each NPC should have ticked exactly twice total
        for (int step = 0; step < 6; step++)
        {
            tickedInStep.Add(new List<EntityId>());
            manager.StepBrainTick();
        }

        foreach (var npc in npcs)
        {
            Assert.Equal(2, npc.BrainTickCount);
        }
    }

    [Fact]
    public void Arbitrary_10_Npcs_Are_Evenly_Distributed_Across_6_Ticks()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 10; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // 10 NPCs into 6 buckets -> 4 buckets have 2, 2 buckets have 1
        int countWith2 = 0;
        int countWith1 = 0;

        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            int c = manager.GetScheduledNpcCount(NpcLodTier.Normal, phase);
            if (c == 2) countWith2++;
            else if (c == 1) countWith1++;
            else Assert.Fail($"Unexpected bucket count {c}");
        }

        Assert.Equal(4, countWith2);
        Assert.Equal(2, countWith1);

        var countsPerStep = new List<int>();
        manager.NpcBrainTicked += _ =>
        {
            countsPerStep[^1]++;
        };

        for (int step = 0; step < 6; step++)
        {
            countsPerStep.Add(0);
            manager.StepBrainTick();
        }

        // Each step must have either 1 or 2 ticks
        foreach (var count in countsPerStep)
        {
            Assert.True(count is 1 or 2, $"Expected 1 or 2 ticks per step, but got {count}");
        }

        // Total ticks over 6 steps must equal 10
        Assert.Equal(10, countsPerStep.Sum());

        // Every NPC ticked exactly once
        foreach (var npc in npcs)
        {
            Assert.Equal(1, npc.BrainTickCount);
        }
    }

    [Fact]
    public void Large_Scale_100_Npcs_Remain_Balanced_Within_One_Npc_Difference()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 100; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // 100 / 6 = 16 remainder 4 -> 4 buckets have 17, 2 buckets have 16
        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            int c = manager.GetScheduledNpcCount(NpcLodTier.Normal, phase);
            Assert.True(c is 16 or 17, $"Bucket {phase} count was {c}, expected 16 or 17");
        }

        var countsPerStep = new List<int>();
        manager.NpcBrainTicked += _ =>
        {
            countsPerStep[^1]++;
        };

        // Run 18 ticks (3 full 6-tick intervals)
        for (int step = 0; step < 18; step++)
        {
            countsPerStep.Add(0);
            manager.StepBrainTick();
        }

        // Every single step has either 16 or 17 ticks (difference between max and min is <= 1)
        int minStep = countsPerStep.Min();
        int maxStep = countsPerStep.Max();
        Assert.True(maxStep - minStep <= 1);
        Assert.Equal(16, minStep);
        Assert.Equal(17, maxStep);

        // Every NPC ticked exactly 3 times across the 18 steps
        foreach (var npc in npcs)
        {
            Assert.Equal(3, npc.BrainTickCount);
        }
    }

    [Fact]
    public void Despawning_Maintains_Balanced_Buckets()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 12; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // Despawn 3 NPCs
        manager.DespawnNpc(npcs[0].Id);
        manager.DespawnNpc(npcs[1].Id);
        manager.DespawnNpc(npcs[2].Id);

        // Remaining 9 NPCs: min bucket count and max bucket count should differ by at most 1
        var counts = new List<int>();
        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            counts.Add(manager.GetScheduledNpcCount(NpcLodTier.Normal, phase));
        }

        Assert.Equal(9, counts.Sum());
        Assert.True(counts.Max() - counts.Min() <= 1);

        // Spawn 3 new NPCs -> should immediately restore perfect 2-per-bucket balance
        manager.SpawnNpc("New_1", Vec3.Zero);
        manager.SpawnNpc("New_2", Vec3.Zero);
        manager.SpawnNpc("New_3", Vec3.Zero);

        for (int phase = 0; phase < manager.NormalInterval; phase++)
        {
            Assert.Equal(2, manager.GetScheduledNpcCount(NpcLodTier.Normal, phase));
        }
    }

    [Fact]
    public void Distant_Lod_Npcs_Are_Staggered_Across_30_Ticks()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10,
            DistantBrainTickRateHz = 2,
            LodDistanceThreshold = 64f
        };
        var manager = new NpcManager(registry, config);

        // Place player at origin
        var player = new PlayerEntity(registry.GenerateNextId(), "Player", Vec3.Zero);
        registry.Register(player);

        // Spawn 60 NPCs at distance 100m (> 64m) -> Distant LOD (interval = 60 / 2 = 30 ticks)
        var npcs = new List<NpcState>();
        for (int i = 0; i < 60; i++)
        {
            npcs.Add(manager.SpawnNpc($"Distant_{i}", new Vec3(100f, 0f, 0f)));
        }

        Assert.Equal(30, manager.DistantInterval);

        // 60 NPCs / 30 buckets -> exactly 2 per bucket
        for (int phase = 0; phase < manager.DistantInterval; phase++)
        {
            Assert.Equal(2, manager.GetScheduledNpcCount(NpcLodTier.Distant, phase));
        }

        var countsPerStep = new List<int>();
        manager.NpcBrainTicked += _ =>
        {
            countsPerStep[^1]++;
        };

        for (int step = 0; step < 30; step++)
        {
            countsPerStep.Add(0);
            manager.StepBrainTick();
        }

        // Each step has exactly 2 ticks
        for (int step = 0; step < 30; step++)
        {
            Assert.Equal(2, countsPerStep[step]);
        }

        // Every distant NPC ticked once
        foreach (var npc in npcs)
        {
            Assert.Equal(1, npc.BrainTickCount);
        }
    }

    [Fact]
    public void BrainTick_With_DeltaTime_Executes_Discrete_Steps()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig
        {
            PhysicsTickRateHz = 60,
            BrainTickRateHz = 10
        };
        var manager = new NpcManager(registry, config);

        var npcs = new List<NpcState>();
        for (int i = 0; i < 12; i++)
        {
            npcs.Add(manager.SpawnNpc($"Npc_{i}", Vec3.Zero));
        }

        // 0.1 seconds at 60 Hz = exactly 6 physics frames
        // Over 6 frames, all 12 NPCs should have ticked exactly once!
        manager.BrainTick(0.1f);

        Assert.Equal(6, manager.TotalBrainTicks);
        foreach (var npc in npcs)
        {
            Assert.Equal(1, npc.BrainTickCount);
        }
    }
}
