namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Speech;
using Xunit;

public class BrainCompilerTests
{
    private readonly EntityRegistry _registry;
    private readonly EventBus _eventBus;
    private readonly SpeechSystem _speechSystem;
    private readonly MainThreadDispatcher _dispatcher;
    private readonly BrainValidator _validator;
    private readonly PromptBuilder _promptBuilder;
    private readonly LlmConfig _config;

    public BrainCompilerTests()
    {
        _registry = new EntityRegistry();
        _eventBus = new EventBus();
        _speechSystem = new SpeechSystem(_registry, _eventBus);
        _dispatcher = new MainThreadDispatcher();
        _validator = new BrainValidator();
        _promptBuilder = new PromptBuilder(new LuaApiRegistry());
        _config = new LlmConfig { MaxRepairRetries = 2 };
    }

    private NpcState CreateTestNpc(string name = "Bob", string initialScript = "")
    {
        var id = _registry.GenerateNextId();
        var memory = new NpcMemory();
        var npc = new NpcState(
            id,
            name,
            position: new Vec3(10, 5, 10),
            personality: "helpful woodcutter",
            skills: new[] { "woodcutting", "gathering" },
            memory: memory);

        var brainHost = new BrainHost(
            npc,
            entityRegistry: _registry,
            eventBus: _eventBus);

        if (!string.IsNullOrEmpty(initialScript))
        {
            brainHost.LoadScript(initialScript);
        }

        npc.BrainHost = brainHost;
        _registry.Register(npc);
        return npc;
    }

    [Fact]
    public async Task CompileAsync_ValidInstruction_SucceedsOnFirstAttempt_AndSwapsBrain()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        bool eventRaised = false;
        compiler.ResultApplied += (n, r) =>
        {
            Assert.Same(npc, n);
            Assert.True(r.IsSuccess);
            eventRaised = true;
        };

        var instructorId = new EntityId(999);

        // 2. Act: "chop oak trees and gather logs" maps to gather_logs.lua in MockLlmClient
        var result = await compiler.CompileAsync(
            npc,
            "Please chop oak trees and gather logs",
            instructorId: instructorId,
            instructorName: "Adrian",
            currentTime: 42.0);

        // 3. Assert compile result
        Assert.Equal(CompileStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Attempts);
        Assert.NotEmpty(result.Reply);
        Assert.NotEmpty(result.TaskSummary);
        Assert.NotEmpty(result.Script);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation.Ok);

        // Drain main thread dispatcher to execute ApplyResult
        int drained = _dispatcher.Drain();
        Assert.True(drained > 0);
        Assert.True(eventRaised);

        // Verify NPC state updates
        Assert.Equal(result.TaskSummary, npc.CurrentTaskSummary);
        Assert.Equal(42.0, npc.TaskStartTime);
        Assert.Equal(instructorId, npc.InstructorId);
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(result.Script, npc.BrainHost.CurrentScript);

        // Verify NPC memory updates
        Assert.Equal(result.TaskSummary, npc.Memory.Get("current_task"));
        Assert.Equal(result.TaskSummary, npc.Memory.Get("task_summary"));
        var events = npc.Memory.GetAllEvents();
        Assert.Contains(events, e => e is InstructionReceivedEvent ire && ire.Instruction.Contains("chop oak trees"));
    }

    [Fact]
    public async Task CompileAsync_InvalidThenRepaired_SucceedsOnSecondAttempt()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: "chop wood and please test_repair" triggers invalid on turn 1, then valid on turn 2
        var result = await compiler.CompileAsync(
            npc,
            "chop wood and please test_repair",
            instructorName: "Adrian",
            currentTime: 50.0);

        // 3. Assert
        Assert.Equal(CompileStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Attempts); // 1 initial + 1 repair retry
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation.Ok);

        _dispatcher.Drain();

        Assert.Equal(result.TaskSummary, npc.CurrentTaskSummary);
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(result.Script, npc.BrainHost.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_InvalidExhausted_ReturnsValidationFailed_AndPreservesCurrentBrain()
    {
        // 1. Arrange
        string initialScript = "function main()\n npc.wait(1)\nend\n";
        var npc = CreateTestNpc("Bob", initialScript: initialScript);
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: "test_syntax_error" triggers syntax errors on every attempt (attempts = 1 + 2 retries = 3)
        var result = await compiler.CompileAsync(
            npc,
            "run test_syntax_error please",
            instructorName: "Adrian",
            currentTime: 60.0);

        // 3. Assert
        Assert.Equal(CompileStatus.ValidationFailed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.Attempts);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation.Ok);
        Assert.Equal(CompileResult.DefaultInvalidFallbackReply, result.Reply);

        _dispatcher.Drain();

        // Old script must be preserved untouched
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(initialScript, npc.BrainHost.CurrentScript);

        // Failure recorded in NPC memory
        var events = npc.Memory.GetAllEvents();
        Assert.Contains(events, e => e.EventType == "brain_error");
    }

    [Fact]
    public async Task CompileAsync_NeedsClarification_ReturnsClarificationStatus_AndPreservesCurrentBrain()
    {
        // 1. Arrange
        string initialScript = "function main()\n npc.wait(1)\nend\n";
        var npc = CreateTestNpc("Bob", initialScript: initialScript);
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Ambiguous instruction triggers needs_clarification = true
        var result = await compiler.CompileAsync(
            npc,
            "what should I do over there with that thing?",
            instructorName: "Adrian",
            currentTime: 70.0);

        // 3. Assert
        Assert.Equal(CompileStatus.ClarificationRequested, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.Attempts);
        Assert.Empty(result.Script);
        Assert.Contains("clarify", result.Reply, StringComparison.OrdinalIgnoreCase);

        _dispatcher.Drain();

        // Current brain script preserved
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(initialScript, npc.BrainHost.CurrentScript);

        // Instruction recorded in memory
        var events = npc.Memory.GetAllEvents();
        Assert.Contains(events, e => e.EventType == "instructed");
    }

    [Fact]
    public async Task CompileAsync_ChitChat_ReturnsKeepCurrentTask_AndPreservesCurrentBrain()
    {
        // 1. Arrange
        string initialScript = "function main()\n npc.wait(1)\nend\n";
        var npc = CreateTestNpc("Bob", initialScript: initialScript);
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Casual greeting triggers keep_current_task = true
        var result = await compiler.CompileAsync(
            npc,
            "Good morning Bob! How are you doing today?",
            instructorName: "Adrian",
            currentTime: 80.0);

        // 3. Assert
        Assert.Equal(CompileStatus.KeepCurrentTask, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.Attempts);
        Assert.Empty(result.Script);
        Assert.Contains("Hello", result.Reply, StringComparison.OrdinalIgnoreCase);

        _dispatcher.Drain();

        // Current brain script preserved
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(initialScript, npc.BrainHost.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_SimulatedError_ReturnsErrorStatus_AndSpokenFallbackLine()
    {
        // 1. Arrange
        string initialScript = "function main()\n npc.wait(1)\nend\n";
        var npc = CreateTestNpc("Bob", initialScript: initialScript);
        var mockClient = new MockLlmClient
        {
            SimulatedError = LlmError.Timeout("Simulated network timeout")
        };
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act
        var result = await compiler.CompileAsync(
            npc,
            "Chop some wood",
            currentTime: 90.0);

        // 3. Assert
        Assert.Equal(CompileStatus.Error, result.Status);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(LlmErrorKind.Timeout, result.Error.Kind);
        Assert.Equal(CompileResult.DefaultErrorFallbackReply, result.Reply);

        _dispatcher.Drain();

        // Old brain preserved
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(initialScript, npc.BrainHost.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_CancelledByToken_ReturnsCancelledStatus()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient
        {
            SimulatedLatency = TimeSpan.FromMilliseconds(500)
        };
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // 2. Act
        var result = await compiler.CompileAsync(
            npc,
            "Chop some wood",
            cancellationToken: cts.Token);

        // 3. Assert
        Assert.Equal(CompileStatus.Cancelled, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CompileAsync_PerNpcCancellation_NewInstructionCancelsOlderOne()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient
        {
            SimulatedLatency = TimeSpan.FromMilliseconds(200)
        };
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Launch instruction 1 (long running), then immediately instruction 2
        var task1 = compiler.CompileAsync(npc, "first long task chop wood");
        await Task.Delay(20);
        var task2 = compiler.CompileAsync(npc, "second task follow me closely");

        var result1 = await task1;
        var result2 = await task2;

        // 3. Assert: Task 1 was cancelled by Task 2
        Assert.Equal(CompileStatus.Cancelled, result1.Status);
        Assert.Equal(CompileStatus.Success, result2.Status);
    }

    [Fact]
    public async Task CompileAsync_EntityIdOverload_FindsNpcAndAppliesResult()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Alice");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Call using EntityId
        var result = await compiler.CompileAsync(
            npc.Id,
            "Follow me and stay close",
            instructorName: "Adrian");

        // 3. Assert
        Assert.Equal(CompileStatus.Success, result.Status);
        _dispatcher.Drain();
        Assert.Equal(result.TaskSummary, npc.CurrentTaskSummary);
        Assert.NotNull(npc.BrainHost);
        Assert.Equal(result.Script, npc.BrainHost.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_EntityIdNotFound_ThrowsKeyNotFoundException()
    {
        // 1. Arrange
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        var unknownId = new EntityId(99999);

        // 2. Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            compiler.CompileAsync(unknownId, "Follow me"));
    }

    [Fact]
    public async Task CompileAsync_MultiTurnConversationInspection_VerifiesRepairPromptStructure()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        int callCount = 0;
        LlmRequest? capturedRepairRequest = null;

        mockClient.CustomHandler = req =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First turn: return broken script with unknown API
                return LlmResponse.Success(
                    new LlmBrainResponse("I will do that.", script: "function main()\n unknown_call()\nend\n"),
                    "{\"reply\":\"I will do that.\",\"script\":\"function main()\\n unknown_call()\\nend\\n\",\"needs_clarification\":false,\"keep_current_task\":false,\"task_summary\":\"test\"}",
                    new LlmUsage(10, 10, 20));
            }

            // Second turn (repair): capture request and return valid script
            capturedRepairRequest = req;
            return LlmResponse.Success(
                new LlmBrainResponse("Repaired!", script: "function main()\n npc.wait(1)\nend\n", taskSummary: "Repaired task"),
                "{\"reply\":\"Repaired!\",\"script\":\"function main()\\n npc.wait(1)\\nend\\n\",\"needs_clarification\":false,\"keep_current_task\":false,\"task_summary\":\"Repaired task\"}",
                new LlmUsage(10, 10, 20));
        };

        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act
        var result = await compiler.CompileAsync(npc, "Do custom test");

        // 3. Assert
        Assert.Equal(CompileStatus.Success, result.Status);
        Assert.Equal(2, result.Attempts);
        Assert.NotNull(capturedRepairRequest);

        // Verify conversation history in repair turn
        // Turn 0: user original instruction
        // Turn 1: model previous output
        // Turn 2: user repair message with validation errors
        Assert.Equal(3, capturedRepairRequest.Messages.Count);
        Assert.Equal("user", capturedRepairRequest.Messages[0].Role);
        Assert.Equal("model", capturedRepairRequest.Messages[1].Role);
        Assert.Equal("user", capturedRepairRequest.Messages[2].Role);

        string repairMessage = capturedRepairRequest.Messages[2].Text;
        Assert.Contains("failed validation", repairMessage);
        Assert.Contains("Stage", repairMessage);
        Assert.Contains("repair the script", repairMessage);
    }

    [Fact]
    public async Task CompileAsync_AutoApplyFalse_DoesNotMutateState_UntilApplyResultCalled()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act with autoApply = false
        var result = await compiler.CompileAsync(
            npc,
            "Chop trees and gather logs",
            autoApply: false);

        Assert.Equal(CompileStatus.Success, result.Status);

        // Dispatcher has not received any work
        int drained = _dispatcher.Drain();
        Assert.Equal(0, drained);
        Assert.Null(npc.CurrentTaskSummary);

        // Now manually apply
        compiler.ApplyResult(npc, result, "Chop trees and gather logs");
        Assert.Equal(result.TaskSummary, npc.CurrentTaskSummary);
        Assert.Equal(result.Script, npc.BrainHost?.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_InvalidJsonFormat_TriggersStage1ValidationRepair()
    {
        // 1. Arrange
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        int callCount = 0;

        mockClient.CustomHandler = req =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Turn 1: Return invalid unparseable JSON (Stage 1 error)
                return new LlmResponse
                {
                    Content = null,
                    RawText = "This is not valid JSON at all! {unclosed",
                    Usage = new LlmUsage(10, 10, 20),
                    Error = null
                };
            }

            // Turn 2 (repair): Return valid response
            return LlmResponse.Success(
                new LlmBrainResponse("Now I have fixed my output.", script: "function main()\n npc.wait(1)\nend\n", taskSummary: "Fixed task"),
                "{\"reply\":\"Now I have fixed my output.\",\"script\":\"function main()\\n npc.wait(1)\\nend\\n\",\"needs_clarification\":false,\"keep_current_task\":false,\"task_summary\":\"Fixed task\"}",
                new LlmUsage(10, 10, 20));
        };

        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act
        var result = await compiler.CompileAsync(npc, "Do something");

        // 3. Assert: Repaired after Stage 1 parse error
        Assert.Equal(CompileStatus.Success, result.Status);
        Assert.Equal(2, result.Attempts);
        Assert.Equal("Fixed task", result.TaskSummary);
    }

    [Fact]
    public async Task CancelAll_CancelsAllActiveCompilationsAcrossNpcs()
    {
        // 1. Arrange
        var npc1 = CreateTestNpc("Bob");
        var npc2 = CreateTestNpc("Alice");
        var mockClient = new MockLlmClient
        {
            SimulatedLatency = TimeSpan.FromMilliseconds(500)
        };
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Start 2 compilations
        var task1 = compiler.CompileAsync(npc1, "gather wood");
        var task2 = compiler.CompileAsync(npc2, "follow me");

        await Task.Delay(20);
        compiler.CancelAll();

        var result1 = await task1;
        var result2 = await task2;

        // 3. Assert: Both cancelled
        Assert.Equal(CompileStatus.Cancelled, result1.Status);
        Assert.Equal(CompileStatus.Cancelled, result2.Status);
    }

    [Fact]
    public async Task CompileAsync_ConcurrentNpcs_CompileIndependently()
    {
        // 1. Arrange
        var npc1 = CreateTestNpc("Bob");
        var npc2 = CreateTestNpc("Alice");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        // 2. Act: Run concurrently
        var task1 = compiler.CompileAsync(npc1, "chop wood");
        var task2 = compiler.CompileAsync(npc2, "follow me");

        var results = await Task.WhenAll(task1, task2);

        // 3. Assert: Both succeeded
        Assert.Equal(CompileStatus.Success, results[0].Status);
        Assert.Equal(CompileStatus.Success, results[1].Status);

        _dispatcher.Drain();

        Assert.NotNull(npc1.BrainHost?.CurrentScript);
        Assert.NotNull(npc2.BrainHost?.CurrentScript);
    }

    [Fact]
    public async Task CompileAsync_RecordsPromptResponseAndTokenUsageOnNpc()
    {
        var npc = CreateTestNpc("Bob");
        var mockClient = new MockLlmClient();
        var compiler = BrainCompiler.FromClient(
            mockClient,
            _promptBuilder,
            _validator,
            _dispatcher,
            _speechSystem,
            _registry,
            config: _config);

        Assert.Null(npc.LastPrompt);
        Assert.Null(npc.LastResponse);
        Assert.Equal(LlmUsage.Zero, npc.TotalTokenUsage);

        var result = await compiler.CompileAsync(npc, "gather wood");
        _dispatcher.Drain();

        Assert.Equal(CompileStatus.Success, result.Status);
        Assert.NotNull(npc.LastPrompt);
        Assert.Contains("gather wood", npc.LastPrompt);
        Assert.NotNull(npc.LastResponse);
        Assert.True(npc.TotalTokenUsage.TotalTokens > 0, "Token usage should be accumulated from LLM response.");
    }
}
