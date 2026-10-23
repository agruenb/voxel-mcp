namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Collections.Generic;
using System.IO;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Core.Voxel;
using Xunit;

public class DayNightBrainTests
{
    private sealed class InstantActionFactory : IActionFactory
    {
        public IAction? CreateAction(NpcState npc, ActionRequest request)
        {
            return new InstantSucceedAction(request.ActionName);
        }
    }

    private sealed class InstantSucceedAction : ActionBase
    {
        public override string Name { get; }

        public InstantSucceedAction(string name) : base(30f)
        {
            Name = name;
        }

        protected override void OnStart()
        {
            Succeed();
        }

        protected override void OnTick(float dt)
        {
            Succeed();
        }
    }

    [Fact]
    public void GoHomeAtNight_TransitionsBetweenDayAndNightRoutines()
    {
        var blockRegistry = BlockRegistry.CreateDefault();
        var voxelWorld = new VoxelWorld(blockRegistry);
        var entityRegistry = new EntityRegistry();
        var worldTime = new WorldTime(initialHour: 12.0f, dayLengthSeconds: 600.0f);
        var perceptionService = new PerceptionService(voxelWorld, entityRegistry, worldTime: worldTime);

        var homePos = new Vec3(10f, 64f, 10f);
        var npc = new NpcState(
            new EntityId(1),
            "NocturnalWorker",
            homePos,
            home: homePos,
            memory: new NpcMemory());

        entityRegistry.Register(npc);
        npc.Perception = perceptionService.For(npc);

        var saidLines = new List<string>();
        var eventBus = new EventBus();
        eventBus.Subscribe<EntitySpoke>(spoke =>
        {
            if (spoke.SpeakerId == npc.Id)
            {
                saidLines.Add(spoke.Text);
            }
        });

        var host = new BrainHost(
            npc,
            entityRegistry: entityRegistry,
            eventBus: eventBus,
            actionFactory: new InstantActionFactory());
        npc.BrainHost = host;

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "../../../../../brains/examples/go_home_at_night.lua");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.Combine(AppContext.BaseDirectory, "brains/examples/go_home_at_night.lua");
        }
        if (!File.Exists(scriptPath))
        {
            // Fallback search upwards
            string dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "brains/examples/go_home_at_night.lua");
                if (File.Exists(candidate))
                {
                    scriptPath = candidate;
                    break;
                }
                dir = Path.GetDirectoryName(dir)!;
            }
        }

        Assert.True(File.Exists(scriptPath), $"Script not found at: {scriptPath}");
        string script = File.ReadAllText(scriptPath);

        bool loaded = host.LoadScript(script);
        Assert.True(loaded);
        Assert.Equal(BrainStatus.Running, host.Status);

        // 1. Tick during Day (12.0h)
        // NPC should say the intro line and start daytime wandering
        host.Tick(0.1f);
        Assert.Contains(saidLines, line => line.Contains("Understood. I will stay outside by day"));

        // Simulate a few daytime ticks
        for (int i = 0; i < 5; i++)
        {
            host.Tick(1.0f);
        }

        // Move NPC slightly away from home so dist > 2
        npc.Position = new Vec3(20f, 64f, 20f);

        // 2. Advance time into Night (20.0h)
        worldTime.SetTime(20.0f);
        Assert.False(worldTime.IsDay);

        // Next tick: NPC should perceive night, say "Night has fallen", and head home
        // Drain any remaining wait timer on host
        for (int i = 0; i < 10; i++)
        {
            host.Tick(1.0f);
            if (saidLines.Exists(line => line.Contains("Night has fallen. Returning home now.")))
            {
                break;
            }
        }

        Assert.Contains(saidLines, line => line.Contains("Night has fallen. Returning home now."));

        // Simulate returning home
        npc.Position = homePos;

        // 3. While night continues, NPC rests inside and waits
        for (int i = 0; i < 5; i++)
        {
            host.Tick(1.0f);
        }

        // "Sun is up" should NOT have been said yet
        Assert.DoesNotContain(saidLines, line => line.Contains("Sun is up! Time for daytime activities."));

        // 4. Advance time into Day (8.0h)
        worldTime.SetTime(8.0f);
        Assert.True(worldTime.IsDay);

        // NPC loop checks world.is_day() and greets the sunrise
        for (int i = 0; i < 10; i++)
        {
            host.Tick(1.0f);
            if (saidLines.Exists(line => line.Contains("Sun is up! Time for daytime activities.")))
            {
                break;
            }
        }

        Assert.Contains(saidLines, line => line.Contains("Sun is up! Time for daytime activities."));
    }
}
