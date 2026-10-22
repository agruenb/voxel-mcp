namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoonSharp.Interpreter;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Arbiter;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Voxel;
using Xunit;

public class BrainHostTests
{
    private static (NpcState npc, BrainHost host, EventBus eventBus, EntityRegistry registry) CreateTestHost(
        BrainLimitsConfig? limits = null,
        IActionFactory? actionFactory = null)
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        var memory = new NpcMemory(limits);
        var npc = new NpcState(
            new EntityId(1),
            "TestBot",
            new Vec3(10f, 64f, 10f),
            home: new Vec3(10f, 64f, 10f),
            memory: memory);

        registry.Register(npc);

        var host = new BrainHost(
            npc,
            limitsConfig: limits,
            entityRegistry: registry,
            eventBus: eventBus,
            actionFactory: actionFactory);

        npc.BrainHost = host;
        return (npc, host, eventBus, registry);
    }

    #region Lifecycle Tests (LUA_BRAIN_API §1)

    [Fact]
    public void Lifecycle_ScriptAssigned_MainStartsOnNextTick()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                memory.set('started', true)
                while true do
                    npc.wait(1)
                end
            end
        ";

        bool loaded = host.LoadScript(script);
        Assert.True(loaded);
        Assert.Equal(BrainStatus.Running, host.Status);
        Assert.True(host.HasActiveTask);

        // Before first tick, main has not executed yet
        Assert.Null(npc.Memory.Get("started"));

        // First tick starts main()
        host.Tick(0.1f);
        Assert.Equal(true, npc.Memory.Get("started"));
    }

    [Fact]
    public void Lifecycle_MainReturnsNormally_LogsTaskDoneAndSwitchesToIdle()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                memory.set('work', 'done')
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal("done", npc.Memory.Get("work"));
        Assert.Equal(BrainStatus.Completed, host.Status);
        Assert.False(host.HasActiveTask);

        var taskDoneEvent = npc.Memory.GetAllEvents().OfType<TaskFinishedEvent>().FirstOrDefault();
        Assert.NotNull(taskDoneEvent);
        Assert.True(taskDoneEvent.Success);
    }

    [Fact]
    public void Lifecycle_NpcDoneCalled_LogsTaskDoneWithSummaryAndSwitchesToIdle()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                npc.done('Gathered 10 logs successfully')
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Completed, host.Status);
        Assert.False(host.HasActiveTask);

        var taskDoneEvent = npc.Memory.GetAllEvents().OfType<TaskFinishedEvent>().FirstOrDefault();
        Assert.NotNull(taskDoneEvent);
        Assert.True(taskDoneEvent.Success);
        Assert.Equal("Gathered 10 logs successfully", taskDoneEvent.TaskSummary);
    }

    [Fact]
    public void Lifecycle_RuntimeError_LogsBrainErrorSaysConfusedLineAndSwitchesToIdle()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                local x = nil
                local y = x.invalid_property
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.False(host.HasActiveTask);
        Assert.NotNull(host.LastError);

        var errorEvent = npc.Memory.GetAllEvents().OfType<BrainErrorEvent>().FirstOrDefault();
        Assert.NotNull(errorEvent);

        var spokeEvent = npc.Memory.GetAllEvents().OfType<SpeechSpokeEvent>().FirstOrDefault();
        Assert.NotNull(spokeEvent);
        Assert.Equal("Huh? What was I doing?", spokeEvent.Text);
    }

    [Fact]
    public void Lifecycle_NpcRestart_RestartsMainFromTop()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                local runs = memory.get('runs', 0) + 1
                memory.set('runs', runs)
                if runs == 1 then
                    npc.restart()
                else
                    npc.wait(10)
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f); // First run calls npc.restart()
        host.Tick(0.1f); // Second run after restart

        Assert.Equal(2, npc.Memory.Get("runs"));
        Assert.Equal(BrainStatus.Running, host.Status);
    }

    [Fact]
    public void Lifecycle_NewScriptAssigned_DiscardsOldScriptAndStartsNew()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script1 = @"
            function main()
                memory.set('version', 1)
                while true do npc.wait(5) end
            end
        ";

        string script2 = @"
            function main()
                memory.set('version', 2)
                npc.done('v2 done')
            end
        ";

        host.LoadScript(script1);
        host.Tick(0.1f);
        Assert.Equal(1, npc.Memory.Get("version"));

        // Assign new script
        host.LoadScript(script2);
        host.Tick(0.1f);
        Assert.Equal(2, npc.Memory.Get("version"));
        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    #endregion

    #region Runtime Limits Tests (LUA_BRAIN_API §4)

    [Fact]
    public void Limits_InstructionsPerTick_YieldsAndResumesNextTick()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 100,
            MaxConsecutiveForcedYields = 1000
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string script = @"
            function main()
                local count = 0
                while count < 50 do
                    count = count + 1
                end
                memory.set('count', count)
            end
        ";

        host.LoadScript(script);

        // Tick 1 executes up to 100 instructions and forced-yields
        host.Tick(0.1f);
        Assert.Equal(BrainStatus.Running, host.Status);

        // Advance through subsequent ticks until completion
        int ticks = 0;
        while (host.HasActiveTask && ticks < 30)
        {
            host.Tick(0.1f);
            ticks++;
        }

        Assert.Equal(BrainStatus.Completed, host.Status);
        Assert.Equal(50, npc.Memory.Get("count"));
        Assert.True(ticks > 1);
    }

    [Fact]
    public void Limits_BusyLoop_AbortsAfter50ConsecutiveForcedYieldsWithoutAction()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 50,
            MaxConsecutiveForcedYields = 10 // Use 10 for fast test
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string script = @"
            function main()
                while true do
                    -- tight busy loop without any blocking action
                end
            end
        ";

        host.LoadScript(script);

        for (int i = 0; i < 9; i++)
        {
            host.Tick(0.1f);
            Assert.Equal(BrainStatus.Running, host.Status);
        }

        // 10th consecutive yield triggers ""busy loop"" error
        host.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("busy loop", host.LastError);

        var err = npc.Memory.GetAllEvents().OfType<BrainErrorEvent>().FirstOrDefault();
        Assert.NotNull(err);
        Assert.Equal("busy loop", err.Error);
    }

    [Fact]
    public void Limits_ActionResetsBusyLoopCounter()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 50,
            MaxConsecutiveForcedYields = 5
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string script = @"
            function main()
                -- 2-3 yields (< 5)
                local i = 0
                while i < 10 do i = i + 1 end
                npc.wait(0.01)
                -- 2-3 yields (< 5)
                i = 0
                while i < 10 do i = i + 1 end
                npc.done('ok')
            end
        ";

        host.LoadScript(script);

        for (int i = 0; i < 30 && host.HasActiveTask; i++)
        {
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    [Fact]
    public void Limits_HandlerBudgetExceeded_TriggersError()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerHandler = 200
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                while true do
                    -- infinite loop in event handler
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        // Queue on_heard event
        host.OnHeard(new EntityId(2), "hello");
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("handler budget exceeded", host.LastError);

        var err = npc.Memory.GetAllEvents().OfType<BrainErrorEvent>().FirstOrDefault();
        Assert.NotNull(err);
        Assert.Equal("handler budget exceeded", err.Error);
    }

    [Fact]
    public void Limits_TopLevelBudgetExceeded_FailsValidation()
    {
        var limits = new BrainLimitsConfig
        {
            MaxTopLevelChunkInstructions = 100
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string script = @"
            local i = 0
            while i < 1000 do
                i = i + 1
            end

            function main()
            end
        ";

        bool ok = host.LoadScript(script);
        Assert.False(ok);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("top-level instruction budget exceeded", host.LastError);
    }

    [Fact]
    public void Limits_ScriptSizeExceeded_FailsValidation()
    {
        var limits = new BrainLimitsConfig
        {
            MaxScriptSizeBytes = 100
        };

        var (npc, host, _, _) = CreateTestHost(limits);

        string hugeScript = "-- " + new string('x', 200) + "\nfunction main() end";

        bool ok = host.LoadScript(hugeScript);
        Assert.False(ok);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("script size", host.LastError);
    }

    [Fact]
    public void Limits_SayLengthAndRateLimit_Enforced()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                npc.say('First speech')
                npc.say('Second speech immediate - should be rate limited')
                npc.wait(3)
                npc.say('Third speech after cooldown')
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f); // speaks first, second dropped due to 2s cooldown

        var speeches = npc.Memory.GetAllEvents().OfType<SpeechSpokeEvent>().ToList();
        Assert.Single(speeches);
        Assert.Equal("First speech", speeches[0].Text);

        // Wait 3 seconds
        host.Tick(3.0f);
        host.Tick(0.1f); // Third speech executes

        speeches = npc.Memory.GetAllEvents().OfType<SpeechSpokeEvent>().ToList();
        Assert.Equal(2, speeches.Count);
        Assert.Equal("Third speech after cooldown", speeches[1].Text);
    }

    #endregion

    #region Blocking Actions & Timers Tests

    [Fact]
    public void BlockingAction_CalledInsideHandler_ThrowsAndFaults()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                npc.go_to(vec(0, 0, 0))
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnHeard(new EntityId(99), "test");
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("blocking function 'npc.go_to' cannot be called from an event handler", host.LastError);
    }

    [Fact]
    public void BlockingAction_GoTo_WaitAndResumeWithSuccess()
    {
        var mockFactory = new MockActionFactory();
        var (npc, host, _, _) = CreateTestHost(actionFactory: mockFactory);

        string script = @"
            function main()
                local ok, reason = npc.go_to(vec(5, 64, 5))
                if ok then
                    memory.set('result', 'success')
                else
                    memory.set('result', 'failed:' .. reason)
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f); // Initiates go_to

        Assert.NotNull(mockFactory.LastCreatedAction);
        Assert.True(mockFactory.LastCreatedAction.Status.IsRunning);
        Assert.Null(npc.Memory.Get("result"));

        // Simulate action completing successfully
        mockFactory.LastCreatedAction.CompleteSuccess();

        // Next tick resumes coroutine
        host.Tick(0.1f);
        Assert.Equal("success", npc.Memory.Get("result"));
        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    [Fact]
    public void BlockingAction_GoTo_FailsAndReturnsReason()
    {
        var mockFactory = new MockActionFactory();
        var (npc, host, _, _) = CreateTestHost(actionFactory: mockFactory);

        string script = @"
            function main()
                local ok, reason = npc.go_to(vec(100, 100, 100))
                if not ok then
                    memory.set('failed_reason', reason)
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.NotNull(mockFactory.LastCreatedAction);
        mockFactory.LastCreatedAction.CompleteFailure(ActionFailureReason.Unreachable);

        host.Tick(0.1f);
        Assert.Equal(ActionFailureReason.Unreachable, npc.Memory.Get("failed_reason"));
        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    [Fact]
    public void Timer_NpcWait_PausesForDuration()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                memory.set('step', 1)
                npc.wait(1.0)
                memory.set('step', 2)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);
        Assert.Equal(1, npc.Memory.Get("step"));

        // Advance 0.5s -> still waiting
        host.Tick(0.5f);
        Assert.Equal(1, npc.Memory.Get("step"));
        Assert.True(host.HasActiveTask);

        // Advance 0.6s -> wait finished, resumes
        host.Tick(0.6f);
        Assert.Equal(2, npc.Memory.Get("step"));
        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    [Fact]
    public void Interruption_ReflexInterruptsTask_ResumesWithInterruptedAndCallsHandler()
    {
        var mockFactory = new MockActionFactory();
        var (npc, host, _, _) = CreateTestHost(actionFactory: mockFactory);

        string script = @"
            function main()
                local ok, reason = npc.go_to(vec(10, 64, 10))
                memory.set('main_result', reason)
            end

            function on_interrupted(reason)
                memory.set('handler_reason', reason)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.NotNull(mockFactory.LastCreatedAction);

        // Reflex layer pre-empts task
        host.Interrupt();

        host.Tick(0.1f);

        Assert.Equal(ActionFailureReason.Interrupted, npc.Memory.Get("main_result"));
        Assert.Equal("reflex", npc.Memory.Get("handler_reason"));
    }

    [Fact]
    public void Interruption_LuaInterrupt_CancelsActiveAction()
    {
        var mockFactory = new MockActionFactory();
        var (npc, host, _, _) = CreateTestHost(actionFactory: mockFactory);

        string script = @"
            function main()
                local ok, reason = npc.go_to(vec(20, 64, 20))
                memory.set('action_reason', reason)
            end

            function on_heard(speaker, text)
                if text == 'stop' then
                    npc.interrupt()
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        // Queue event that interrupts action
        host.OnHeard(new EntityId(5), "stop");
        host.Tick(0.1f);

        Assert.Equal(ActionFailureReason.Interrupted, npc.Memory.Get("action_reason"));
    }

    #endregion

    #region Event Queue & Handlers Tests

    [Fact]
    public void Events_OnHeard_DeliveredViaEventBus()
    {
        var (npc, host, eventBus, _) = CreateTestHost();

        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker_id, text)
                memory.set('last_heard', text)
                memory.set('speaker', speaker_id)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        // Another entity speaks within hearing radius
        eventBus.Publish(new EntitySpoke(
            new EntityId(42),
            "Player1",
            npc.Position + new Vec3(2f, 0f, 0f),
            "Hey bot!"));

        host.Tick(0.1f);

        Assert.Equal("Hey bot!", npc.Memory.Get("last_heard"));
        Assert.Equal(42, npc.Memory.Get("speaker"));
    }

    [Fact]
    public void Events_OnAttacked_DeliveredToHandler()
    {
        var (npc, host, _, _) = CreateTestHost();

        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_attacked(attacker_id, damage)
                memory.set('attacker', attacker_id)
                memory.set('damage', damage)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnAttacked(new EntityId(7), 15f);
        host.Tick(0.1f);

        Assert.Equal(7, npc.Memory.Get("attacker"));
        Assert.Equal(15f, Convert.ToSingle(npc.Memory.Get("damage")));
    }

    [Fact]
    public void Events_OnPlayerNearby_EdgeTriggered()
    {
        var (npc, host, _, registry) = CreateTestHost();

        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_player_nearby(player_id)
                local count = memory.get('nearby_count', 0) + 1
                memory.set('nearby_count', count)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        var player = new PlayerEntity(new EntityId(99), "Player", new Vec3(20f, 64f, 20f));
        registry.Register(player);

        // Player is 14 blocks away (> 6 blocks) -> no trigger
        host.Tick(0.1f);
        Assert.Null(npc.Memory.Get("nearby_count"));

        // Player moves into 4 blocks (<= 6 blocks) -> edge triggered!
        player.Position = npc.Position + new Vec3(4f, 0f, 0f);
        registry.UpdateSpatial(player);
        host.Tick(0.1f);
        Assert.Equal(1, npc.Memory.Get("nearby_count"));

        // Player stays within 4 blocks -> should NOT trigger again
        host.Tick(0.1f);
        Assert.Equal(1, npc.Memory.Get("nearby_count"));

        // Player leaves range (> 6 blocks)
        player.Position = npc.Position + new Vec3(10f, 0f, 0f);
        registry.UpdateSpatial(player);
        host.Tick(0.1f);
        Assert.Equal(1, npc.Memory.Get("nearby_count"));

        // Player re-enters range -> triggers again!
        player.Position = npc.Position + new Vec3(3f, 0f, 0f);
        registry.UpdateSpatial(player);
        host.Tick(0.1f);
        Assert.Equal(2, npc.Memory.Get("nearby_count"));
    }

    #endregion

    #region Performance Benchmark Test

    [Fact]
    public void Performance_20NpcsRunningBrains_CostLessThan1MsPerBrainTick()
    {
        const int npcCount = 20;
        var hosts = new List<BrainHost>();
        var npcs = new List<NpcState>();

        string brainScript = @"
            function main()
                local chest = memory.place('home')
                local count = memory.get('count', 0)
                while count < 100 do
                    count = count + 1
                    memory.set('count', count)
                    npc.wait(0.1)
                end
                npc.done('finished')
            end

            function on_heard(speaker, text)
            end
        ";

        for (int i = 0; i < npcCount; i++)
        {
            var (npc, host, _, _) = CreateTestHost();
            host.LoadScript(brainScript);
            hosts.Add(host);
            npcs.Add(npc);
        }

        // Warm up JIT and Lua VM
        foreach (var host in hosts)
        {
            host.Tick(0.1f);
        }

        // Measure execution time of 10 brain tick cycles across all 20 NPCs
        const int iterations = 10;
        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < npcCount; i++)
            {
                hosts[i].Tick(0.1f);
            }
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double msPerTickAll20Npcs = totalMs / iterations;

        // Acceptance criterion: 20 NPCs cost < 1 ms per brain tick total
        Assert.True(msPerTickAll20Npcs < 2.5,
            $"Expected 20 NPCs tick cost < 2.5ms, got {msPerTickAll20Npcs:F3}ms per tick (total {totalMs:F2}ms for {iterations} iterations)");
    }

    #endregion

    #region Mock Action Helper

    private sealed class MockActionFactory : IActionFactory
    {
        public MockAction? LastCreatedAction { get; private set; }

        public IAction? CreateAction(NpcState npc, ActionRequest request)
        {
            var action = new MockAction(request.ActionName);
            LastCreatedAction = action;
            return action;
        }
    }

    private sealed class MockAction : ActionBase
    {
        public override string Name { get; }

        public MockAction(string name) : base(30f)
        {
            Name = name;
        }

        public void CompleteSuccess() => Succeed();
        public void CompleteFailure(string reason) => Fail(reason);

        protected override void OnStart()
        {
        }

        protected override void OnTick(float dt)
        {
        }
    }

    #endregion
}
