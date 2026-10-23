namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Speech;

/// <summary>
/// Orchestrates the Gemini brain compilation pipeline (F-404, LLM_INTEGRATION §3 &amp; §4).
/// Flow:
/// 1. Builds cached system prompt + dynamic user content using <see cref="IPromptBuilder"/>.
/// 2. Dispatches request via <see cref="ILlmRequestQueue"/> with per-NPC concurrency and cancellation.
/// 3. Parses structured JSON response into <see cref="LlmBrainResponse"/>.
/// 4. Validates Lua brain script against safety and sandbox limits via <see cref="IBrainValidator"/>.
/// 5. Executes multi-turn repair loop on validation errors (up to max retries).
/// 6. Marshals completed <see cref="CompileResult"/> to the main thread via <see cref="IMainThreadDispatcher"/>.
/// 7. Applies result to NPC state per the LLM_INTEGRATION §3 handling table (speech, script swap, memory updates).
/// </summary>
public class BrainCompiler : IBrainCompiler
{
    private readonly ILlmRequestQueue _requestQueue;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IBrainValidator _validator;
    private readonly IMainThreadDispatcher? _dispatcher;
    private readonly ISpeechSystem? _speechSystem;
    private readonly EntityRegistry? _entityRegistry;
    private readonly NpcManager? _npcManager;
    private readonly LlmConfig _config;
    private readonly ILogger? _logger;

    private readonly object _lock = new();
    private readonly Dictionary<EntityId, (CancellationTokenSource Cts, NpcState Npc)> _activeCompilations = new();

    /// <inheritdoc/>
    public event Action<NpcState, CompileResult>? ResultApplied;

    /// <summary>
    /// Gets the LLM request queue used by this compiler.
    /// </summary>
    public ILlmRequestQueue RequestQueue => _requestQueue;

    /// <summary>
    /// Gets the prompt builder used by this compiler.
    /// </summary>
    public IPromptBuilder PromptBuilder => _promptBuilder;

    /// <summary>
    /// Gets the brain validator used by this compiler.
    /// </summary>
    public IBrainValidator Validator => _validator;

    /// <summary>
    /// Gets the LLM configuration settings used by this compiler.
    /// </summary>
    public LlmConfig Config => _config;

    public BrainCompiler(
        ILlmRequestQueue requestQueue,
        IPromptBuilder promptBuilder,
        IBrainValidator validator,
        IMainThreadDispatcher? dispatcher = null,
        ISpeechSystem? speechSystem = null,
        EntityRegistry? entityRegistry = null,
        NpcManager? npcManager = null,
        LlmConfig? config = null,
        ILogger? logger = null)
    {
        _requestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _dispatcher = dispatcher;
        _speechSystem = speechSystem;
        _entityRegistry = entityRegistry;
        _npcManager = npcManager;
        _config = config ?? new LlmConfig();
        _logger = logger;
    }

    /// <summary>
    /// Factory method accepting an <see cref="ILlmClient"/> directly (wraps in an <see cref="LlmRequestQueue"/> if not already one).
    /// </summary>
    public static BrainCompiler FromClient(
        ILlmClient client,
        IPromptBuilder promptBuilder,
        IBrainValidator validator,
        IMainThreadDispatcher? dispatcher = null,
        ISpeechSystem? speechSystem = null,
        EntityRegistry? entityRegistry = null,
        NpcManager? npcManager = null,
        LlmConfig? config = null,
        ILogger? logger = null)
    {
        var queue = client is ILlmRequestQueue q ? q : new LlmRequestQueue(client, config ?? new LlmConfig());
        return new BrainCompiler(
            queue,
            promptBuilder,
            validator,
            dispatcher,
            speechSystem,
            entityRegistry,
            npcManager,
            config,
            logger);
    }

    /// <inheritdoc/>
    public async Task<CompileResult> CompileAsync(
        NpcState npc,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        bool autoApply = true,
        string? playerHeldItem = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new ArgumentException("Instruction cannot be null or whitespace.", nameof(instruction));
        }

        // Per-NPC cancellation: a new compilation for this NPC cancels any older in-flight compilation
        CancellationTokenSource npcCts = new();
        CancellationTokenSource linkedCts;
        lock (_lock)
        {
            if (_activeCompilations.TryGetValue(npc.Id, out var existing))
            {
                _logger?.LogInfo($"BrainCompiler: Superseding in-flight compilation for NPC {npc.Id} ({npc.Name}) due to newer instruction: \"{instruction}\"");
                try
                {
                    existing.Cts.Cancel();
                    existing.Cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _activeCompilations[npc.Id] = (npcCts, npc);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, npcCts.Token);
        }

        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();

            // 1. Build initial prompt
            string effectiveInstructor = instructorName ?? "Player";
            npc.ConversationState = NpcConversationState.Thinking;
            npc.AddConversationTurn(effectiveInstructor, instruction);

            _logger?.LogInfo($"BrainCompiler: Compiling instruction for NPC {npc.Id} ({npc.Name}): \"{instruction}\" from {effectiveInstructor}");

            var builtPrompt = _promptBuilder.BuildPrompt(
                npc,
                instruction,
                instructor: effectiveInstructor,
                conversation: conversation ?? (npc.ConversationHistory.Count > 0 ? npc.ConversationHistory : null),
                currentTime: currentTime,
                playerHeldItem: playerHeldItem);

            var messages = new List<LlmMessage>
            {
                LlmMessage.User(builtPrompt.UserContent)
            };

            var request = new LlmRequest(
                builtPrompt.SystemInstruction,
                messages,
                _config.Model,
                _config.MaxOutputTokens);

            npc.LastPrompt = $"[System Instruction]\n{builtPrompt.SystemInstruction}\n\n[User Instruction]\n{builtPrompt.UserContent}";

            // 2. Dispatch request to queue (turn 1)
            var response = await _requestQueue.EnqueueAsync(
                npc.Id,
                npc.Name,
                request,
                linkedCts.Token).ConfigureAwait(false);

            npc.LastResponse = response.RawText;
            if (response.Usage.TotalTokens > 0)
            {
                npc.TotalTokenUsage = new LlmUsage(
                    npc.TotalTokenUsage.PromptTokens + response.Usage.PromptTokens,
                    npc.TotalTokenUsage.OutputTokens + response.Usage.OutputTokens,
                    npc.TotalTokenUsage.TotalTokens + response.Usage.TotalTokens);
            }

            // 3. Handle transport or provider failure (network, timeout, quota, etc.)
            // Note: InvalidJson is a Stage 1 validation failure per LLM_INTEGRATION §4 and enters the repair loop.
            bool isStage1Failure = response.Error?.Kind == LlmErrorKind.InvalidJson ||
                                  (response.Error == null && response.Content == null && !string.IsNullOrWhiteSpace(response.RawText));

            if (!isStage1Failure && (!response.IsSuccess || response.Error != null))
            {
                if (response.Error?.Kind == LlmErrorKind.Cancelled || linkedCts.IsCancellationRequested)
                {
                    if (npc.ConversationState == NpcConversationState.Thinking)
                    {
                        npc.ConversationState = NpcConversationState.None;
                    }
                    _logger?.LogInfo($"BrainCompiler: Compilation cancelled for NPC {npc.Id} ({npc.Name}).");
                    return CompileResult.Cancelled("Compilation was cancelled.");
                }

                _logger?.LogWarning($"BrainCompiler: LLM request failed for NPC {npc.Id} ({npc.Name}): {response.Error?.Kind} - {response.Error?.Message}");

                var failResult = CompileResult.Failed(
                    response.Error ?? LlmError.Unknown("LLM request returned an unspecified failure."),
                    attempts: 1,
                    finalResponse: response);

                DispatchIfAutoApply(npc, failResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                return failResult;
            }

            // 4. Parse structured JSON response
            LlmBrainResponse? content = response.Content;
            string? parseError = null;
            if (content == null && !string.IsNullOrWhiteSpace(response.RawText))
            {
                if (!LlmResponseParser.TryParse(response.RawText, out content, out parseError))
                {
                    // Failed Stage 1 schema validation
                    _logger?.LogWarning($"BrainCompiler: Failed to parse structured JSON response for NPC {npc.Id} ({npc.Name}): {parseError}");
                }
            }

            // 5. Check if clarification requested or pure chit-chat
            if (content != null)
            {
                if (content.NeedsClarification)
                {
                    _logger?.LogInfo($"BrainCompiler: NPC {npc.Id} ({npc.Name}) requested clarification: \"{content.Reply}\"");
                    var clarifResult = CompileResult.Clarification(content.Reply, attempts: 1, finalResponse: response);
                    DispatchIfAutoApply(npc, clarifResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                    return clarifResult;
                }

                if (content.KeepCurrentTask)
                {
                    _logger?.LogInfo($"BrainCompiler: NPC {npc.Id} ({npc.Name}) chit-chat / keep current task: \"{content.Reply}\"");
                    var keepResult = CompileResult.KeepCurrent(content.Reply, attempts: 1, finalResponse: response);
                    DispatchIfAutoApply(npc, keepResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                    return keepResult;
                }
            }

            // 6. Validate generated Lua script (Stages 2–6)
            ValidationResult validation;
            if (content == null)
            {
                string errorMsg = response.Error?.Message ?? parseError ?? "Failed to parse structured JSON response matching schema.";
                validation = ValidationResult.Failure(new ValidationError(1, null, errorMsg));
            }
            else
            {
                validation = _validator.Validate(content.Script);
            }

            int attempts = 1;

            if (validation.Ok && content != null)
            {
                string effectiveSummary = !string.IsNullOrWhiteSpace(content.TaskSummary) ? content.TaskSummary : instruction;
                _logger?.LogInfo($"BrainCompiler: Successfully compiled Lua brain for NPC {npc.Id} ({npc.Name}) on attempt 1! Summary: \"{effectiveSummary}\"");
                var successResult = CompileResult.Success(
                    content.Reply,
                    effectiveSummary,
                    content.Script,
                    validation,
                    attempts,
                    finalResponse: response);

                DispatchIfAutoApply(npc, successResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                return successResult;
            }

            // 7. Repair Loop (LLM_INTEGRATION §4: up to MaxRepairRetries)
            int maxRetries = Math.Max(0, _config.MaxRepairRetries);
            for (int retry = 1; retry <= maxRetries; retry++)
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                _logger?.LogInfo($"NPC {npc.Id} ({npc.Name}) script validation failed on attempt {attempts}. Starting repair turn {retry}/{maxRetries}...");

                // Append previous model output
                string previousModelText = !string.IsNullOrWhiteSpace(response.RawText)
                    ? response.RawText
                    : (content != null ? JsonSerializer.Serialize(content) : "{}");
                messages.Add(LlmMessage.Model(previousModelText));

                // Append repair prompt turn containing error list
                string repairPrompt = _promptBuilder.BuildRepairContent(validation.Errors, content?.Script);
                messages.Add(LlmMessage.User(repairPrompt));

                attempts++;

                var repairRequest = new LlmRequest(
                    builtPrompt.SystemInstruction,
                    messages,
                    _config.Model,
                    _config.MaxOutputTokens);

                response = await _requestQueue.EnqueueAsync(
                    npc.Id,
                    npc.Name,
                    repairRequest,
                    linkedCts.Token).ConfigureAwait(false);

                npc.LastResponse = response.RawText;
                if (response.Usage.TotalTokens > 0)
                {
                    npc.TotalTokenUsage = new LlmUsage(
                        npc.TotalTokenUsage.PromptTokens + response.Usage.PromptTokens,
                        npc.TotalTokenUsage.OutputTokens + response.Usage.OutputTokens,
                        npc.TotalTokenUsage.TotalTokens + response.Usage.TotalTokens);
                }

                bool isRepairStage1Failure = response.Error?.Kind == LlmErrorKind.InvalidJson ||
                                             (response.Error == null && response.Content == null && !string.IsNullOrWhiteSpace(response.RawText));

                if (!isRepairStage1Failure && (!response.IsSuccess || response.Error != null))
                {
                    if (response.Error?.Kind == LlmErrorKind.Cancelled || linkedCts.IsCancellationRequested)
                    {
                        return CompileResult.Cancelled("Compilation was cancelled during repair.");
                    }

                    var failResult = CompileResult.Failed(
                        response.Error ?? LlmError.Unknown("LLM request failed during repair retry."),
                        attempts,
                        finalResponse: response);

                    DispatchIfAutoApply(npc, failResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                    return failResult;
                }

                content = response.Content;
                parseError = null;
                if (content == null && !string.IsNullOrWhiteSpace(response.RawText))
                {
                    LlmResponseParser.TryParse(response.RawText, out content, out parseError);
                }

                if (content == null)
                {
                    string errorMsg = response.Error?.Message ?? parseError ?? "Failed to parse JSON response during repair retry.";
                    validation = ValidationResult.Failure(new ValidationError(1, null, errorMsg));
                    continue;
                }

                if (content != null)
                {
                    if (content.NeedsClarification)
                    {
                        _logger?.LogInfo($"BrainCompiler: NPC {npc.Id} ({npc.Name}) requested clarification during repair retry {retry}: \"{content.Reply}\"");
                        var clarifResult = CompileResult.Clarification(content.Reply, attempts, finalResponse: response);
                        DispatchIfAutoApply(npc, clarifResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                        return clarifResult;
                    }

                    if (content.KeepCurrentTask)
                    {
                        _logger?.LogInfo($"BrainCompiler: NPC {npc.Id} ({npc.Name}) chit-chat / keep current task during repair retry {retry}: \"{content.Reply}\"");
                        var keepResult = CompileResult.KeepCurrent(content.Reply, attempts, finalResponse: response);
                        DispatchIfAutoApply(npc, keepResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                        return keepResult;
                    }

                    validation = _validator.Validate(content.Script);
                    if (validation.Ok)
                    {
                        string effectiveSummary = !string.IsNullOrWhiteSpace(content.TaskSummary) ? content.TaskSummary : instruction;
                        _logger?.LogInfo($"NPC {npc.Id} ({npc.Name}) script repaired successfully on attempt {attempts}!");
                        var successResult = CompileResult.Success(
                            content.Reply,
                            effectiveSummary,
                            content.Script,
                            validation,
                            attempts,
                            finalResponse: response);

                        DispatchIfAutoApply(npc, successResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
                        return successResult;
                    }
                }
                else
                {
                    validation = ValidationResult.Failure(new ValidationError(1, null, parseError ?? "Failed to parse JSON response during repair retry."));
                }
            }

            // 8. Retries exhausted
            _logger?.LogWarning($"NPC {npc.Id} ({npc.Name}) failed brain script validation after {attempts} attempts ({maxRetries} repair retries exhausted).");
            var exhaustedResult = CompileResult.ValidationFailed(
                validation,
                attempts,
                finalResponse: response);

            DispatchIfAutoApply(npc, exhaustedResult, instruction, instructorId, effectiveInstructor, currentTime, autoApply);
            return exhaustedResult;
        }
        catch (OperationCanceledException)
        {
            if (npc.ConversationState == NpcConversationState.Thinking)
            {
                npc.ConversationState = NpcConversationState.None;
            }
            return CompileResult.Cancelled("Compilation was cancelled.");
        }
        finally
        {
            lock (_lock)
            {
                if (_activeCompilations.TryGetValue(npc.Id, out var current) && current.Cts == npcCts)
                {
                    _activeCompilations.Remove(npc.Id);
                }
            }

            linkedCts.Dispose();
            npcCts.Dispose();
        }
    }

    /// <inheritdoc/>
    public Task<CompileResult> CompileAsync(
        EntityId npcId,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        bool autoApply = true,
        string? playerHeldItem = null,
        CancellationToken cancellationToken = default)
    {
        NpcState? npc = null;
        if (_npcManager != null && _npcManager.TryGetNpc(npcId, out var managerNpc))
        {
            npc = managerNpc;
        }
        else if (_entityRegistry != null && _entityRegistry.TryGet(npcId, out var entity) && entity is NpcState regNpc)
        {
            npc = regNpc;
        }

        if (npc == null)
        {
            throw new KeyNotFoundException($"NPC with ID {npcId} was not found.");
        }

        return CompileAsync(npc, instruction, instructorId, instructorName, conversation, currentTime, autoApply, playerHeldItem, cancellationToken);
    }

    /// <inheritdoc/>
    public void ApplyResult(
        NpcState npc,
        CompileResult result,
        string instruction,
        EntityId? instructorId = null,
        string? instructorName = null,
        double? currentTime = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(result);

        double time = currentTime ?? (_speechSystem?.CurrentTime ?? 0);
        npc.ConversationState = NpcConversationState.Replying;

        // 1. Spoken feedback per LLM_INTEGRATION §3 handling table
        if (!string.IsNullOrWhiteSpace(result.Reply))
        {
            npc.AddConversationTurn(npc.Name, result.Reply);
            if (_speechSystem != null)
            {
                _speechSystem.Speak(
                    npc.Id,
                    npc.Name,
                    npc.Position,
                    result.Reply,
                    timestamp: time,
                    force: true);
            }
            else
            {
                npc.LastSayTime = (float)time;
                npc.Memory.RecordSpeechSpoke(result.Reply, time);
            }
        }

        // 2. State and memory updates per LLM_INTEGRATION §3
        switch (result.Status)
        {
            case CompileStatus.Success:
                if (!string.IsNullOrEmpty(result.Script))
                {
                    npc.BrainHost?.Swap(result.Script, instructorId);
                }

                npc.CurrentTaskSummary = result.TaskSummary;
                npc.TaskStartTime = time;
                npc.InstructorId = instructorId;

                if (!string.IsNullOrEmpty(result.TaskSummary))
                {
                    npc.Memory.Set("current_task", result.TaskSummary);
                    npc.Memory.Set("task_summary", result.TaskSummary);
                }

                npc.Memory.RecordInstruction(instructorName ?? "Player", instruction, time);
                break;

            case CompileStatus.ClarificationRequested:
            case CompileStatus.KeepCurrentTask:
                // Current brain continues (resumes after dialogue); record instruction in memory
                npc.Memory.RecordInstruction(instructorName ?? "Player", instruction, time);
                break;

            case CompileStatus.ValidationFailed:
                // Current brain continues; failure logged and recorded in memory
                _logger?.LogWarning($"NPC {npc.Id} ({npc.Name}) brain validation failed after {result.Attempts} attempts: {result.Validation}");
                string firstErr = result.Validation?.Errors.FirstOrDefault().Message ?? "Validation failed";
                npc.Memory.RecordBrainError($"Validation failed: {firstErr}", time);
                break;

            case CompileStatus.Error:
                // Current brain continues; error logged
                _logger?.LogWarning($"NPC {npc.Id} ({npc.Name}) LLM compilation error: {result.Error}");
                break;

            case CompileStatus.Cancelled:
                break;
        }

        npc.ConversationState = NpcConversationState.None;
        ResultApplied?.Invoke(npc, result);
    }

    /// <inheritdoc/>
    public bool Cancel(EntityId npcId)
    {
        bool found = false;
        NpcState? targetNpc = null;
        lock (_lock)
        {
            if (_activeCompilations.TryGetValue(npcId, out var active))
            {
                targetNpc = active.Npc;
                try
                {
                    active.Cts.Cancel();
                    active.Cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }

                _activeCompilations.Remove(npcId);
                found = true;
            }
        }

        bool queueCancelled = _requestQueue.Cancel(npcId);

        if (targetNpc == null)
        {
            if (_npcManager != null && _npcManager.TryGetNpc(npcId, out var mNpc))
            {
                targetNpc = mNpc;
            }
            else if (_entityRegistry != null && _entityRegistry.TryGet(npcId, out var entity) && entity is NpcState regNpc)
            {
                targetNpc = regNpc;
            }
        }

        if (targetNpc != null && (targetNpc.ConversationState == NpcConversationState.Thinking || targetNpc.ConversationState == NpcConversationState.Listening))
        {
            targetNpc.ConversationState = NpcConversationState.None;
        }

        return found || queueCancelled;
    }

    /// <inheritdoc/>
    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var active in _activeCompilations.Values)
            {
                try
                {
                    active.Cts.Cancel();
                    active.Cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }

                if (active.Npc.ConversationState == NpcConversationState.Thinking || active.Npc.ConversationState == NpcConversationState.Listening)
                {
                    active.Npc.ConversationState = NpcConversationState.None;
                }
            }

            _activeCompilations.Clear();
        }

        _requestQueue.CancelAll();
    }

    private void DispatchIfAutoApply(
        NpcState npc,
        CompileResult result,
        string instruction,
        EntityId? instructorId,
        string? instructorName,
        double currentTime,
        bool autoApply)
    {
        if (!autoApply || result.Status == CompileStatus.Cancelled)
        {
            return;
        }

        if (_dispatcher != null)
        {
            _dispatcher.Enqueue(() =>
            {
                ApplyResult(npc, result, instruction, instructorId, instructorName, currentTime);
            });
        }
        else
        {
            ApplyResult(npc, result, instruction, instructorId, instructorName, currentTime);
        }
    }
}
