namespace VoxelKing.Core.Tests.Npc;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Arbiter;
using VoxelKing.Core.Npc.Memory;
using Xunit;

public class NpcConversationStateTests
{
    [Fact]
    public void NpcConversationState_DefaultsToNone()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));
        Assert.Equal(NpcConversationState.None, npc.ConversationState);
        Assert.Empty(npc.ConversationHistory);
    }

    [Fact]
    public void SetConversationState_TransitionsAndFiresEvent()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));

        var transitions = new List<(NpcConversationState oldState, NpcConversationState newState)>();
        npc.ConversationStateChanged += (sender, oldState, newState) =>
        {
            Assert.Same(npc, sender);
            transitions.Add((oldState, newState));
        };

        // Transition None -> Listening
        npc.ConversationState = NpcConversationState.Listening;
        Assert.Equal(NpcConversationState.Listening, npc.ConversationState);

        // Same state does not re-fire
        npc.ConversationState = NpcConversationState.Listening;

        // Listening -> Thinking
        npc.ConversationState = NpcConversationState.Thinking;
        Assert.Equal(NpcConversationState.Thinking, npc.ConversationState);

        // Thinking -> Replying
        npc.ConversationState = NpcConversationState.Replying;
        Assert.Equal(NpcConversationState.Replying, npc.ConversationState);

        // Replying -> None
        npc.ConversationState = NpcConversationState.None;
        Assert.Equal(NpcConversationState.None, npc.ConversationState);

        Assert.Equal(4, transitions.Count);
        Assert.Equal((NpcConversationState.None, NpcConversationState.Listening), transitions[0]);
        Assert.Equal((NpcConversationState.Listening, NpcConversationState.Thinking), transitions[1]);
        Assert.Equal((NpcConversationState.Thinking, NpcConversationState.Replying), transitions[2]);
        Assert.Equal((NpcConversationState.Replying, NpcConversationState.None), transitions[3]);
    }

    [Fact]
    public void NpcManager_PublishesNpcConversationStateChangedOnEventBus()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        var config = new NpcConfig();
        var manager = new NpcManager(registry, config, voxelWorld: null, eventBus: eventBus);

        var events = new List<NpcConversationStateChanged>();
        eventBus.Subscribe<NpcConversationStateChanged>(e => events.Add(e));

        var npc = manager.SpawnNpc("Alice", new Vec3(0, 0, 0));
        Assert.Empty(events);

        npc.ConversationState = NpcConversationState.Listening;
        Assert.Single(events);
        Assert.Equal(npc.Id, events[0].NpcId);
        Assert.Equal(NpcConversationState.None, events[0].OldState);
        Assert.Equal(NpcConversationState.Listening, events[0].NewState);

        npc.ConversationState = NpcConversationState.Thinking;
        Assert.Equal(2, events.Count);
        Assert.Equal(NpcConversationState.Listening, events[1].OldState);
        Assert.Equal(NpcConversationState.Thinking, events[1].NewState);

        // Despawn unhooks event
        manager.DespawnNpc(npc.Id);
        npc.ConversationState = NpcConversationState.None;
        Assert.Equal(2, events.Count); // No new event after despawn
    }

    [Fact]
    public void NpcManager_PhysicsTick_PausesActionAndZeroesHorizontalVelocityDuringConversation()
    {
        var registry = new EntityRegistry();
        var manager = new NpcManager(registry, new NpcConfig(), voxelWorld: null);

        var npc = manager.SpawnNpc("Bob", new Vec3(0, 0, 0));
        var mockAction = new MockAction();
        npc.CurrentAction = mockAction;
        npc.Velocity = new Vec3(5f, -2f, 5f);

        // 1. When in Listening state, action does NOT tick, horizontal velocity zeroed
        npc.ConversationState = NpcConversationState.Listening;
        manager.PhysicsTick(0.1f);

        Assert.Equal(0, mockAction.Ticks);
        Assert.Equal(0f, npc.Velocity.X);
        Assert.Equal(0f, npc.Velocity.Z);

        // 2. When in Thinking state, action does NOT tick
        npc.Velocity = new Vec3(3f, 0f, 3f);
        npc.ConversationState = NpcConversationState.Thinking;
        manager.PhysicsTick(0.1f);

        Assert.Equal(0, mockAction.Ticks);
        Assert.Equal(0f, npc.Velocity.X);
        Assert.Equal(0f, npc.Velocity.Z);

        // 3. When returned to None, action resumes ticking
        npc.ConversationState = NpcConversationState.None;
        manager.PhysicsTick(0.1f);

        Assert.Equal(1, mockAction.Ticks);
    }

    [Fact]
    public void NpcManager_StepBrainTick_PausesArbiterDuringConversation()
    {
        var registry = new EntityRegistry();
        var config = new NpcConfig { PhysicsTickRateHz = 10, BrainTickRateHz = 10 };
        var manager = new NpcManager(registry, config, voxelWorld: null);

        var npc = manager.SpawnNpc("Bob", new Vec3(0, 0, 0));
        var mockArbiter = new MockBehaviourArbiter();
        npc.Arbiter = mockArbiter;

        // When Listening or Thinking, brain tick is skipped
        npc.ConversationState = NpcConversationState.Listening;
        manager.StepBrainTick();
        Assert.Equal(0, mockArbiter.Ticks);

        npc.ConversationState = NpcConversationState.Thinking;
        manager.StepBrainTick();
        Assert.Equal(0, mockArbiter.Ticks);

        // When returned to None, brain ticks resume
        npc.ConversationState = NpcConversationState.None;
        manager.StepBrainTick();
        Assert.Equal(1, mockArbiter.Ticks);
    }

    [Fact]
    public void NpcState_ConversationHistory_RecordsAndBoundsTurns()
    {
        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));

        // Adding turns
        npc.AddConversationTurn("Player", "Hello Bob!");
        npc.AddConversationTurn("Bob", "Greetings traveller!");

        Assert.Equal(2, npc.ConversationHistory.Count);
        Assert.Equal("Player", npc.ConversationHistory[0].Speaker);
        Assert.Equal("Hello Bob!", npc.ConversationHistory[0].Message);
        Assert.Equal("Bob", npc.ConversationHistory[1].Speaker);
        Assert.Equal("Greetings traveller!", npc.ConversationHistory[1].Message);

        // Blank turns ignored
        npc.AddConversationTurn("", "valid message");
        npc.AddConversationTurn("Player", "   ");
        Assert.Equal(2, npc.ConversationHistory.Count);

        // Capacity bounds to 20
        for (int i = 0; i < 25; i++)
        {
            npc.AddConversationTurn("Player", $"Turn {i}");
        }
        Assert.Equal(20, npc.ConversationHistory.Count);
        Assert.Equal("Turn 24", npc.ConversationHistory[^1].Message);

        // Clear
        npc.ClearConversationHistory();
        Assert.Empty(npc.ConversationHistory);
    }

    [Fact]
    public async Task BrainCompiler_FullConversationLoop_UpdatesStatesAndRecordsTurns()
    {
        var client = new MockLlmClient();
        var validator = new BrainValidator();
        var compiler = BrainCompiler.FromClient(client, new PromptBuilder(new LuaApiRegistry()), validator);

        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));
        Assert.Equal(NpcConversationState.None, npc.ConversationState);

        var statesRecorded = new List<NpcConversationState>();
        npc.ConversationStateChanged += (n, oldState, newState) => statesRecorded.Add(newState);

        // Player instructed: "gather logs"
        var result = await compiler.CompileAsync(
            npc,
            "gather logs",
            instructorName: "Player",
            autoApply: true);

        Assert.Equal(CompileStatus.Success, result.Status);

        // Check that Thinking and Replying states were entered during flow, then returned to None
        Assert.Contains(NpcConversationState.Thinking, statesRecorded);
        Assert.Contains(NpcConversationState.Replying, statesRecorded);
        Assert.Equal(NpcConversationState.None, npc.ConversationState);

        // Check that conversation turns were recorded in NPC history
        Assert.Equal(2, npc.ConversationHistory.Count);
        Assert.Equal("Player", npc.ConversationHistory[0].Speaker);
        Assert.Equal("gather logs", npc.ConversationHistory[0].Message);
        Assert.Equal("Bob", npc.ConversationHistory[1].Speaker);
        Assert.False(string.IsNullOrWhiteSpace(npc.ConversationHistory[1].Message));
    }

    [Fact]
    public async Task BrainCompiler_OtherNpcsCanBeInstructedWhileOneIsThinking()
    {
        var client = new MockLlmClient
        {
            SimulatedLatency = TimeSpan.FromMilliseconds(50)
        };
        var validator = new BrainValidator();
        var compiler = BrainCompiler.FromClient(client, new PromptBuilder(new LuaApiRegistry()), validator);

        var bob = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));
        var alice = new NpcState(new EntityId(2), "Alice", new Vec3(10, 0, 10));

        // Start Bob compilation asynchronously
        var bobTask = compiler.CompileAsync(bob, "gather logs", autoApply: true);

        // Bob is now Thinking
        Assert.Equal(NpcConversationState.Thinking, bob.ConversationState);

        // Alice can be set to Listening and instructed immediately while Bob is Thinking
        alice.ConversationState = NpcConversationState.Listening;
        Assert.Equal(NpcConversationState.Listening, alice.ConversationState);

        var aliceTask = compiler.CompileAsync(alice, "follow me", autoApply: true);
        Assert.Equal(NpcConversationState.Thinking, alice.ConversationState);

        // Both complete successfully without deadlock or blocking each other
        var results = await Task.WhenAll(bobTask, aliceTask);
        Assert.Equal(CompileStatus.Success, results[0].Status);
        Assert.Equal(CompileStatus.Success, results[1].Status);

        Assert.Equal(NpcConversationState.None, bob.ConversationState);
        Assert.Equal(NpcConversationState.None, alice.ConversationState);
    }

    [Fact]
    public void BrainCompiler_Cancel_RevertsStateToNone()
    {
        var client = new MockLlmClient
        {
            SimulatedLatency = TimeSpan.FromSeconds(5) // Long simulated latency
        };
        var validator = new BrainValidator();
        var compiler = BrainCompiler.FromClient(client, new PromptBuilder(new LuaApiRegistry()), validator);

        var npc = new NpcState(new EntityId(1), "Bob", new Vec3(0, 0, 0));

        var task = compiler.CompileAsync(npc, "build wall", autoApply: true);
        Assert.Equal(NpcConversationState.Thinking, npc.ConversationState);

        // Cancel compilation
        bool cancelled = compiler.Cancel(npc.Id);
        Assert.True(cancelled);
        Assert.Equal(NpcConversationState.None, npc.ConversationState);
    }

    private sealed class MockAction : IAction
    {
        public string Name => "MockAction";
        public ActionStatus Status { get; set; } = ActionStatus.Running;
        public int Ticks { get; private set; }

        public void Start() { }

        public ActionStatus Tick(float dt)
        {
            Ticks++;
            return Status;
        }

        public void Cancel()
        {
            Status = ActionStatus.Failed(ActionFailureReason.Interrupted);
        }
    }

    private sealed class MockBehaviourArbiter : IBehaviourArbiter
    {
        public BehaviourLayerKind ActiveLayer => BehaviourLayerKind.Task;
        public IReflexLayer ReflexLayer => null!;
        public ITaskLayer TaskLayer => null!;
        public IIdleLayer IdleLayer => null!;
        public int Ticks { get; private set; }

        public void PhysicsTick(float dt) { }

        public void Tick(float dt)
        {
            Ticks++;
        }

        public void Reset() { }
    }
}
