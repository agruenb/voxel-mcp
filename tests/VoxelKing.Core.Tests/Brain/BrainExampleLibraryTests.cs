namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter;
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

public class BrainExampleLibraryTests
{
    private static readonly string[] ExpectedValidExamples =
    [
        "gather_logs",
        "follow_player",
        "guard_area",
        "build_wall",
        "farm_loop",
        "go_home_at_night",
        "deliver_items"
    ];

    private static readonly string[] ExpectedInvalidExamples =
    [
        "syntax_error",
        "unknown_api",
        "busy_loop",
        "blocking_in_handler"
    ];

    [Fact]
    public void FindExamplesDirectory_LocatesValidDirectory()
    {
        string? dir = BrainExampleLibrary.FindExamplesDirectory();
        Assert.NotNull(dir);
        Assert.True(Directory.Exists(dir));
        Assert.True(File.Exists(Path.Combine(dir, "gather_logs.lua")));
    }

    [Fact]
    public void LoadAll_DiscoversAllRequiredExamples()
    {
        var examples = BrainExampleLibrary.LoadAll();
        Assert.NotNull(examples);
        Assert.NotEmpty(examples);

        var byName = examples.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        // Check 7 valid examples
        foreach (var name in ExpectedValidExamples)
        {
            Assert.True(byName.ContainsKey(name), $"Expected valid example '{name}' was not found.");
            var ex = byName[name];
            Assert.True(ex.IsValid, $"Example '{name}' should be marked IsValid = true");
            Assert.False(string.IsNullOrWhiteSpace(ex.Instruction), $"Example '{name}' should have an instruction header.");
            Assert.False(string.IsNullOrWhiteSpace(ex.Reply), $"Example '{name}' should have a reply header.");
            Assert.True(Encoding.UTF8.GetByteCount(ex.Script) <= 8192, $"Example '{name}' exceeds 8 KB limit.");
        }

        // Check 4 invalid examples
        foreach (var name in ExpectedInvalidExamples)
        {
            Assert.True(byName.ContainsKey(name), $"Expected invalid example '{name}' was not found.");
            var ex = byName[name];
            Assert.False(ex.IsValid, $"Example '{name}' should be marked IsValid = false");
            Assert.False(string.IsNullOrWhiteSpace(ex.ErrorCategory), $"Example '{name}' should specify an ErrorCategory.");
        }
    }

    [Theory]
    [InlineData("gather_logs")]
    [InlineData("follow_player")]
    [InlineData("guard_area")]
    [InlineData("build_wall")]
    [InlineData("farm_loop")]
    [InlineData("go_home_at_night")]
    [InlineData("deliver_items")]
    public void ValidExamples_CompileAndLoadInBrainHost(string exampleName)
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => string.Equals(e.Name, exampleName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(example);

        var (npc, host, _, _) = CreateTestSim();
        bool loaded = host.LoadScript(example.Script);
        Assert.True(loaded, $"Failed to load script for {exampleName}: {host.LastError}");
        Assert.Equal(BrainStatus.Running, host.Status);
    }

    [Fact]
    public void InvalidExample_SyntaxError_FailsToCompile()
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => e.Name == "syntax_error");
        Assert.NotNull(example);

        var (npc, host, _, _) = CreateTestSim();
        bool loaded = host.LoadScript(example.Script);
        Assert.False(loaded);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    [Fact]
    public void InvalidExample_UnknownApi_FaultsWhenExecuted()
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => e.Name == "unknown_api");
        Assert.NotNull(example);

        var (npc, host, _, _) = CreateTestSim();
        bool loaded = host.LoadScript(example.Script);
        Assert.True(loaded);

        // First tick starts main(), which attempts to call unknown API
        host.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    [Fact]
    public void InvalidExample_BusyLoop_FaultsDueToInstructionOrYieldLimit()
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => e.Name == "busy_loop");
        Assert.NotNull(example);

        var (npc, host, _, _) = CreateTestSim();
        bool loaded = host.LoadScript(example.Script);
        Assert.True(loaded);

        // Tick repeatedly without actions to trigger busy loop limit
        for (int i = 0; i < 60; i++)
        {
            if (host.Status == BrainStatus.Faulted) break;
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
        Assert.Contains("busy loop", host.LastError);
    }

    [Fact]
    public void InvalidExample_BlockingInHandler_FaultsWhenEventDispatched()
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => e.Name == "blocking_in_handler");
        Assert.NotNull(example);

        var (npc, host, eventBus, _) = CreateTestSim();
        bool loaded = host.LoadScript(example.Script);
        Assert.True(loaded);

        // Advance initial tick to start main
        host.Tick(0.1f);
        Assert.Equal(BrainStatus.Running, host.Status);

        // Dispatch speech event to trigger on_heard
        eventBus.Publish(new EntitySpoke(new EntityId(999), "Player", new Vec3(10f, 64f, 10f), "hello there", 16f));

        // Ticking processes the queued event and invokes on_heard, which attempts to call npc.go_to
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
        Assert.Contains("cannot be called from an event handler", host.LastError);
    }

    [Theory]
    [InlineData("gather_logs")]
    [InlineData("follow_player")]
    [InlineData("guard_area")]
    [InlineData("build_wall")]
    [InlineData("farm_loop")]
    [InlineData("go_home_at_night")]
    [InlineData("deliver_items")]
    public void ValidExamples_SimulateMultipleTicks_WithoutErrors(string exampleName)
    {
        var examples = BrainExampleLibrary.LoadAll();
        var example = examples.FirstOrDefault(e => string.Equals(e.Name, exampleName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(example);

        var actionFactory = new AutoCompletingActionFactory();
        var (npc, host, eventBus, registry) = CreateTestSim(actionFactory);

        // Put some starting materials in NPC inventory to support build_wall / deliver_items
        npc.Inventory?.AddItem("stone", 20, out _);
        npc.Inventory?.AddItem("oak_log", 10, out _);
        npc.Inventory?.AddItem("wheat", 5, out _);

        // Record a player chest in memory
        npc.Memory.RememberPlace("player_chest", new Vec3i(10, 64, 12));

        bool loaded = host.LoadScript(example.Script, instructorId: new EntityId(100));
        Assert.True(loaded);

        // Simulate 100 brain ticks (equivalent to 10 seconds of active brain execution)
        for (int i = 0; i < 100; i++)
        {
            if (host.Status == BrainStatus.Completed)
            {
                break; // Task finished cleanly via npc.done()
            }

            // Periodically fire events
            if (i == 10)
            {
                eventBus.Publish(new EntitySpoke(new EntityId(100), "Player", npc.Position, "keep up the good work", 16f));
            }

            host.Tick(0.1f);
            actionFactory.Tick(0.1f);

            Assert.NotEqual(BrainStatus.Faulted, host.Status);
            Assert.Null(host.LastError);
        }
    }

    private static (NpcState npc, BrainHost host, EventBus eventBus, EntityRegistry registry) CreateTestSim(
        IActionFactory? actionFactory = null)
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        var blockRegistry = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blockRegistry, eventBus);
        var perceptionService = new PerceptionService(world, registry);

        var player = new PlayerEntity(new EntityId(100), "Player", new Vec3(10f, 64f, 15f));
        registry.Register(player);

        var memory = new NpcMemory(new BrainLimitsConfig());
        var inventory = new Inventory(16);
        var npc = new NpcState(
            new EntityId(1),
            "TestNPC",
            new Vec3(10f, 64f, 10f),
            home: new Vec3(10f, 64f, 10f),
            inventory: inventory,
            memory: memory);

        registry.Register(npc);
        npc.Perception = perceptionService.For(npc);

        var host = new BrainHost(
            npc,
            limitsConfig: new BrainLimitsConfig(),
            entityRegistry: registry,
            eventBus: eventBus,
            actionFactory: actionFactory ?? new AutoCompletingActionFactory());

        npc.BrainHost = host;
        return (npc, host, eventBus, registry);
    }

    private sealed class AutoCompletingActionFactory : IActionFactory
    {
        private readonly List<AutoAction> _activeActions = new();

        public IAction? CreateAction(NpcState npc, ActionRequest request)
        {
            var action = new AutoAction(request.ActionName);
            _activeActions.Add(action);
            return action;
        }

        public void Tick(float dt)
        {
            for (int i = _activeActions.Count - 1; i >= 0; i--)
            {
                var action = _activeActions[i];
                if (action.Status == ActionStatus.Running)
                {
                    action.Step();
                }
                else
                {
                    _activeActions.RemoveAt(i);
                }
            }
        }
    }

    private sealed class AutoAction : ActionBase
    {
        public override string Name { get; }
        private int _ticksRemaining = 1;

        public AutoAction(string name) : base(30f)
        {
            Name = name;
        }

        protected override void OnStart()
        {
        }

        protected override void OnTick(float dt)
        {
        }

        public void Step()
        {
            _ticksRemaining--;
            if (_ticksRemaining <= 0)
            {
                Succeed();
            }
        }
    }
}
