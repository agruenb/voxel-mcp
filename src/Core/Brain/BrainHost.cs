namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;

/// <summary>
/// Authoritative host executing an NPC's sandboxed Lua brain script (ARCHITECTURE §5.4, LUA_BRAIN_API).
/// Implements coroutine execution of main(), instruction budgeting via AutoYieldCounter,
/// blocking action wait/resume, wait timers, event queues, busy loop detection, and arbiter task layer integration.
/// </summary>
public sealed class BrainHost : IBrainHost
{
    public const float NearbyPlayerRange = 6.0f;

    private readonly NpcState _npc;
    private readonly ILuaRuntime _runtime;
    private readonly LuaApiRegistry _registry;
    private readonly IActionFactory _actionFactory;
    private readonly BrainLimitsConfig _limitsConfig;
    private readonly EntityRegistry? _entityRegistry;
    private readonly IEventBus? _eventBus;
    private readonly ILogger? _logger;
    private readonly INpcBrainContext _context;

    private Script? _script;
    private DynValue? _mainCoroutine;
    private BrainStatus _status = BrainStatus.Uninitialized;
    private string? _currentScript;
    private string? _lastError;
    private int _consecutiveForcedYields;
    private float _waitTimer;
    private IAction? _activeAction;

    private readonly Queue<QueuedEvent> _queuedEvents = new();
    private readonly HashSet<EntityId> _nearbyPlayerIds = new();
    private readonly IDisposable? _eventSubscription;
    private bool _disposed;

    /// <inheritdoc />
    public BrainStatus Status => _status;

    /// <inheritdoc />
    public string? CurrentScript => _currentScript;

    private readonly List<string> _recentLogs = new();
    private readonly object _logsLock = new();
    public const int MaxRecentLogs = 50;

    /// <inheritdoc />
    public string? LastError => _lastError;

    /// <inheritdoc />
    public INpcBrainContext Context => _context;

    /// <inheritdoc />
    public IReadOnlyList<string> RecentLogs
    {
        get
        {
            lock (_logsLock)
            {
                return _recentLogs.ToArray();
            }
        }
    }

    private int _lastTickInstructions;
    private long _totalInstructionsExecuted;

    /// <inheritdoc />
    public int LastTickInstructions => _lastTickInstructions;

    /// <inheritdoc />
    public long TotalInstructionsExecuted => _totalInstructionsExecuted;

    /// <inheritdoc />
    public int MaxInstructionsPerTick => _limitsConfig.MaxInstructionsPerTick;

    /// <inheritdoc />
    public int ConsecutiveForcedYields => _consecutiveForcedYields;

    /// <inheritdoc />
    public int MaxConsecutiveForcedYields => _limitsConfig.MaxConsecutiveForcedYields;

    /// <inheritdoc />
    public bool HasActiveTask => _status == BrainStatus.Running;

    public void AddLog(string message)
    {
        lock (_logsLock)
        {
            if (_recentLogs.Count >= MaxRecentLogs)
            {
                _recentLogs.RemoveAt(0);
            }
            _recentLogs.Add(message);
        }
    }

    public BrainHost(
        NpcState npc,
        ILuaRuntime? runtime = null,
        LuaApiRegistry? registry = null,
        IActionFactory? actionFactory = null,
        BrainLimitsConfig? limitsConfig = null,
        EntityRegistry? entityRegistry = null,
        IEventBus? eventBus = null,
        ILogger? logger = null)
    {
        _npc = npc ?? throw new ArgumentNullException(nameof(npc));
        _limitsConfig = limitsConfig ?? new BrainLimitsConfig();
        _runtime = runtime ?? new MoonSharpRuntime(_limitsConfig);
        _registry = registry ?? LuaApiRegistry.Default;
        _entityRegistry = entityRegistry;
        _eventBus = eventBus;
        _logger = logger;
        _actionFactory = actionFactory ?? new DefaultActionFactory(entityRegistry: entityRegistry, eventBus: eventBus);

        NpcBrainContext context = null!;
        context = new NpcBrainContext(
            _npc,
            logCallback: msg =>
            {
                float time = context != null ? context.CurrentTime : 0f;
                AddLog($"[{time:F1}s] {msg}");
                _logger?.LogDebug($"[NPC {_npc.Name}] {msg}");
            },
            sayCallback: text =>
            {
                float time = context != null ? context.CurrentTime : 0f;
                AddLog($"[{time:F1}s] said: \"{text}\"");
                OnBrainSaid(text);
            },
            actionCallback: (name, args) =>
            {
                RecordActionInstructionUsage();
                var argList = new List<object?>();
                for (int i = 1; i < args.Count; i++)
                {
                    argList.Add(LuaValueConverter.ToClrObject(args[i]));
                }
                return new ActionRequest(name, argList, context.CurrentTime);
            });
        _context = context;

        _eventSubscription = _eventBus?.Subscribe<EntitySpoke>(OnEntitySpoke);
    }

    /// <inheritdoc />
    public bool Swap(string script, EntityId? instructorId = null) => LoadScript(script, instructorId);

    /// <inheritdoc />
    public bool LoadScript(string script, EntityId? instructorId = null)
    {
        ArgumentNullException.ThrowIfNull(script);

        // Cancel any active action and reset timers
        if (_activeAction != null)
        {
            _activeAction.Cancel();
            _activeAction = null;
        }
        if (_npc.CurrentAction != null)
        {
            _npc.CurrentAction.Cancel();
            _npc.CurrentAction = null;
        }

        _waitTimer = 0f;
        _consecutiveForcedYields = 0;
        _queuedEvents.Clear();
        _lastError = null;
        _status = BrainStatus.Uninitialized;

        // 1. Script size limit check (LUA_BRAIN_API §4: 8 KB)
        int byteCount = Encoding.UTF8.GetByteCount(script);
        if (byteCount > _limitsConfig.MaxScriptSizeBytes)
        {
            _lastError = $"script size ({byteCount} bytes) exceeds limit ({_limitsConfig.MaxScriptSizeBytes} bytes)";
            _status = BrainStatus.Faulted;
            _npc.Memory.RecordBrainError(_lastError, _context.CurrentTime);
            _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) script rejected: {_lastError}");
            return false;
        }

        _npc.InstructorId = instructorId;

        // 2. Create sandboxed Script instance (deterministic seed per NPC per LUA_BRAIN_API §3.6)
        _script = _runtime.CreateScript(seed: _npc.Id.Value);

        // 3. Register API bindings into sandbox
        _registry.RegisterIntoScript(_script, _context);

        // 4. Load chunk and validate top-level instruction budget (LUA_BRAIN_API §4: 10,000 instructions)
        DynValue topLevelChunk;
        try
        {
            topLevelChunk = _script.LoadString(script);
        }
        catch (SyntaxErrorException ex)
        {
            _lastError = $"syntax error: {ex.DecoratedMessage ?? ex.Message}";
            _status = BrainStatus.Faulted;
            _npc.Memory.RecordBrainError(_lastError, _context.CurrentTime);
            _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) script syntax error: {_lastError}");
            return false;
        }

        var topLevelCoro = _script.CreateCoroutine(topLevelChunk);
        topLevelCoro.Coroutine.AutoYieldCounter = _limitsConfig.MaxTopLevelChunkInstructions;
        try
        {
            topLevelCoro.Coroutine.Resume();
            if (topLevelCoro.Coroutine.State is CoroutineState.ForceSuspended or CoroutineState.Suspended)
            {
                _lastError = "top-level instruction budget exceeded";
                _status = BrainStatus.Faulted;
                _npc.Memory.RecordBrainError(_lastError, _context.CurrentTime);
                _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) script rejected: {_lastError}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _lastError = $"top-level execution error: {ex.Message}";
            _status = BrainStatus.Faulted;
            _npc.Memory.RecordBrainError(_lastError, _context.CurrentTime);
            _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) top-level error: {_lastError}");
            return false;
        }

        int topRemaining = (int)Math.Max(0L, topLevelCoro.Coroutine.AutoYieldCounter);
        int topConsumed = _limitsConfig.MaxTopLevelChunkInstructions - topRemaining;
        _totalInstructionsExecuted = topConsumed;
        _lastTickInstructions = topConsumed;
        _consecutiveForcedYields = 0;

        // 5. Verify required main() function
        var mainVal = _script.Globals.Get("main");
        if (mainVal.Type != DataType.Function)
        {
            _lastError = "script must define a 'main' function";
            _status = BrainStatus.Faulted;
            _npc.Memory.RecordBrainError(_lastError, _context.CurrentTime);
            _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) script error: {_lastError}");
            return false;
        }

        // 6. Setup main coroutine; per LUA_BRAIN_API §1, main() starts on next brain tick
        _mainCoroutine = _script.CreateCoroutine(mainVal);
        _currentScript = script;
        _status = BrainStatus.Running;
        _context.ResetDone();
        _context.ResetRestartRequest();
        _context.ClearLastActionRequest();

        return true;
    }

    /// <inheritdoc />
    public void Restart()
    {
        if (string.IsNullOrEmpty(_currentScript))
        {
            return;
        }

        LoadScript(_currentScript, _npc.InstructorId);
    }

    /// <inheritdoc />
    public void QueueEvent(string handlerName, params object?[] args)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            return;
        }

        _queuedEvents.Enqueue(new QueuedEvent(handlerName, args));
    }

    /// <inheritdoc />
    public void OnHeard(EntityId speakerId, string text)
    {
        QueueEvent("on_heard", speakerId.Value, text);
    }

    /// <inheritdoc />
    public void OnAttacked(EntityId attackerId, float damage)
    {
        QueueEvent("on_attacked", attackerId.Value, damage);
    }

    /// <inheritdoc />
    public void OnPlayerNearby(EntityId playerId)
    {
        QueueEvent("on_player_nearby", playerId.Value);
    }

    /// <inheritdoc />
    public void OnInterrupted(string reason)
    {
        QueueEvent("on_interrupted", reason);
    }

    /// <inheritdoc />
    public void Interrupt()
    {
        if (_activeAction != null)
        {
            _activeAction.Cancel();
        }
        else if (_waitTimer > 0f)
        {
            _waitTimer = 0f;
            if (_mainCoroutine?.Coroutine?.State == CoroutineState.Suspended)
            {
                ResumeMain(DynValue.False, DynValue.NewString(ActionFailureReason.Interrupted));
            }
        }

        if (_npc.CurrentAction != null && _npc.CurrentAction.Status.IsRunning)
        {
            _npc.CurrentAction.Cancel();
        }

        OnInterrupted("reflex");
    }

    /// <inheritdoc />
    public void Tick(float dt)
    {
        if (_status != BrainStatus.Running)
        {
            return;
        }

        if (!_npc.IsActive || _npc.IsDead)
        {
            _status = BrainStatus.Completed;
            return;
        }

        _context.CurrentTime += dt;

        // 1. Edge-triggered player nearby detection (<= 6 blocks)
        UpdateNearbyPlayers();

        // 2. Process queued events into handlers
        ProcessQueuedEvents();

        if (_status != BrainStatus.Running)
        {
            return;
        }

        // 3. Action / timer waiting
        if (_waitTimer > 0f)
        {
            _waitTimer -= dt;
            if (_activeAction != null && _activeAction.Status.IsRunning)
            {
                // Advance wait action if running
                _activeAction.Tick(dt);
            }

            if (_activeAction != null && _activeAction.Status.IsFailed)
            {
                _waitTimer = 0f;
                string reason = _activeAction.Status.FailureReason ?? ActionFailureReason.Interrupted;
                _activeAction = null;
                _npc.CurrentAction = null;
                ResumeMain(DynValue.False, DynValue.NewString(reason));
                return;
            }

            if (_waitTimer > 0f)
            {
                return; // Still waiting
            }

            _waitTimer = 0f;
            if (_activeAction is WaitAction && ReferenceEquals(_npc.CurrentAction, _activeAction))
            {
                _npc.CurrentAction = null;
            }
            _activeAction = null;

            ResumeMain(DynValue.True);
            return;
        }

        if (_activeAction != null)
        {
            if (_activeAction.Status.IsRunning)
            {
                return; // Action still executing
            }

            var finishedAction = _activeAction;
            _activeAction = null;
            if (ReferenceEquals(_npc.CurrentAction, finishedAction))
            {
                _npc.CurrentAction = null;
            }

            if (finishedAction.Status.IsSucceeded)
            {
                ResumeMain(DynValue.True, DynValue.Nil);
            }
            else
            {
                string reason = finishedAction.Status.FailureReason ?? ActionFailureReason.InvalidTarget;
                ResumeMain(DynValue.False, DynValue.NewString(reason));
            }
            return;
        }

        // 4. Initial resume or resumed after forced AutoYieldCounter yield
        ResumeMain();
    }

    private void UpdateNearbyPlayers()
    {
        if (_entityRegistry == null)
        {
            return;
        }

        var players = _entityRegistry.GetAll(EntityKind.Player);
        var currentNearby = new HashSet<EntityId>();

        foreach (var player in players)
        {
            float dist = _npc.Position.Distance(player.Position);
            if (dist <= NearbyPlayerRange)
            {
                currentNearby.Add(player.Id);
                if (_nearbyPlayerIds.Add(player.Id))
                {
                    // Edge triggered: newly entered 6-block radius
                    OnPlayerNearby(player.Id);
                }
            }
        }

        _nearbyPlayerIds.RemoveWhere(id => !currentNearby.Contains(id));
    }

    private void ProcessQueuedEvents()
    {
        if (_script == null)
        {
            return;
        }

        while (_queuedEvents.Count > 0 && _status == BrainStatus.Running)
        {
            var evt = _queuedEvents.Dequeue();
            var handlerFunc = _script.Globals.Get(evt.HandlerName);

            if (handlerFunc.Type != DataType.Function)
            {
                continue;
            }

            _context.IsInsideHandler = true;
            int initialHandlerBudget = _limitsConfig.MaxInstructionsPerHandler;
            try
            {
                var coro = _script.CreateCoroutine(handlerFunc);
                coro.Coroutine.AutoYieldCounter = initialHandlerBudget;
                var dynArgs = ConvertArgsToDynValue(evt.Args);
                coro.Coroutine.Resume(dynArgs);

                int handlerRemaining = (int)Math.Max(0L, coro.Coroutine.AutoYieldCounter);
                int handlerConsumed = initialHandlerBudget - handlerRemaining;
                _lastTickInstructions += handlerConsumed;
                _totalInstructionsExecuted += handlerConsumed;

                if (coro.Coroutine.State == CoroutineState.ForceSuspended)
                {
                    HandleRuntimeError("handler budget exceeded");
                    return;
                }
                else if (coro.Coroutine.State == CoroutineState.Suspended)
                {
                    HandleRuntimeError("event handlers cannot yield");
                    return;
                }
            }
            catch (ScriptRuntimeException ex)
            {
                HandleRuntimeError(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                HandleRuntimeError(ex.Message);
                return;
            }
            finally
            {
                _context.IsInsideHandler = false;
            }

            if (_context.RestartRequested)
            {
                Restart();
                return;
            }

            if (_context.IsDone)
            {
                CompleteTask(_context.DoneSummary ?? "Task completed");
                return;
            }
        }
    }

    private void ResumeMain(params DynValue[] resumeArgs)
    {
        if (_mainCoroutine == null || _status != BrainStatus.Running)
        {
            return;
        }

        _context.ClearLastActionRequest();
        int initialBudget = _limitsConfig.MaxInstructionsPerTick;
        _mainCoroutine.Coroutine.AutoYieldCounter = initialBudget;

        try
        {
            if (resumeArgs.Length > 0)
            {
                _mainCoroutine.Coroutine.Resume(resumeArgs);
            }
            else
            {
                _mainCoroutine.Coroutine.Resume();
            }
        }
        catch (ScriptRuntimeException ex)
        {
            RecordInstructionsUsed(initialBudget);
            HandleRuntimeError(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            RecordInstructionsUsed(initialBudget);
            HandleRuntimeError(ex.Message);
            return;
        }

        RecordInstructionsUsed(initialBudget);

        // 1. Check if restart requested
        if (_context.RestartRequested)
        {
            Restart();
            return;
        }

        // 2. Check if npc.done called
        if (_context.IsDone)
        {
            CompleteTask(_context.DoneSummary ?? "Task completed");
            return;
        }

        // 3. Check coroutine dead (main returned normally)
        if (_mainCoroutine.Coroutine.State == CoroutineState.Dead)
        {
            CompleteTask(_context.DoneSummary ?? "Task completed");
            return;
        }

        // 4. Coroutine suspended or force suspended
        if (_mainCoroutine.Coroutine.State is CoroutineState.Suspended or CoroutineState.ForceSuspended)
        {
            if (_mainCoroutine.Coroutine.State == CoroutineState.Suspended && _context.LastActionRequest != null)
            {
                // Explicit blocking action request!
                _consecutiveForcedYields = 0;
                DispatchAction(_context.LastActionRequest);
            }
            else
            {
                // Forced yield by AutoYieldCounter
                _lastTickInstructions = _limitsConfig.MaxInstructionsPerTick;
                _totalInstructionsExecuted += _limitsConfig.MaxInstructionsPerTick;
                _consecutiveForcedYields++;
                if (_consecutiveForcedYields >= _limitsConfig.MaxConsecutiveForcedYields)
                {
                    HandleRuntimeError("busy loop");
                }
            }
        }
    }

    private void RecordActionInstructionUsage()
    {
        if (_mainCoroutine?.Coroutine != null)
        {
            int remaining = (int)Math.Max(0L, _mainCoroutine.Coroutine.AutoYieldCounter);
            int consumed = _limitsConfig.MaxInstructionsPerTick - remaining;
            _lastTickInstructions = Math.Max(1, consumed);
            _totalInstructionsExecuted += Math.Max(1, consumed);
        }
    }

    private void RecordInstructionsUsed(int initial)
    {
        if (_mainCoroutine?.Coroutine != null && _lastTickInstructions == 0)
        {
            int remaining = (int)Math.Max(0L, _mainCoroutine.Coroutine.AutoYieldCounter);
            int consumed = initial - remaining;
            if (consumed > 0)
            {
                _lastTickInstructions = consumed;
                _totalInstructionsExecuted += consumed;
            }
        }
    }

    private void DispatchAction(ActionRequest request)
    {
        if (request.ActionName == "wait")
        {
            float seconds = request.Arguments.Count > 0 && request.Arguments[0] != null
                ? Convert.ToSingle(request.Arguments[0])
                : 0f;

            if (seconds < 0f) seconds = 0f;
            if (seconds > WaitAction.MaxWaitSeconds) seconds = WaitAction.MaxWaitSeconds;

            var waitAction = new WaitAction(_npc, seconds);
            _activeAction = waitAction;
            _npc.CurrentAction = waitAction;
            waitAction.Start();

            if (seconds <= 0f || waitAction.Status.IsCompleted)
            {
                _waitTimer = 0f;
                _activeAction = null;
                _npc.CurrentAction = null;
                ResumeMain(DynValue.True);
            }
            else
            {
                _waitTimer = seconds;
            }
            return;
        }

        var action = _actionFactory.CreateAction(_npc, request);
        if (action == null)
        {
            action = new FailedAction(request.ActionName, ActionFailureReason.InvalidTarget);
        }

        _activeAction = action;
        _npc.CurrentAction = action;
        action.Start();
    }

    private void HandleRuntimeError(string errorMessage)
    {
        _lastError = errorMessage;
        _status = BrainStatus.Faulted;

        if (_activeAction != null)
        {
            _activeAction.Cancel();
            _activeAction = null;
        }
        if (_npc.CurrentAction != null)
        {
            _npc.CurrentAction.Cancel();
            _npc.CurrentAction = null;
        }

        _logger?.LogWarning($"NPC '{_npc.Name}' (ID {_npc.Id}) brain runtime error: {errorMessage}");
        _context.Log($"[BrainError] {errorMessage}");

        // Record brain_error in memory per LUA_BRAIN_API §1
        _npc.Memory.RecordBrainError(errorMessage, _context.CurrentTime);

        // Say short confused line per ARCHITECTURE §5.4 / LUA_BRAIN_API §1
        _context.Say("Huh? What was I doing?");
    }

    private void CompleteTask(string summary)
    {
        _status = BrainStatus.Completed;

        if (_activeAction != null)
        {
            _activeAction.Cancel();
            _activeAction = null;
        }
        if (_npc.CurrentAction != null)
        {
            _npc.CurrentAction.Cancel();
            _npc.CurrentAction = null;
        }

        if (!_context.IsDone)
        {
            _npc.Memory.RecordTaskFinished(summary, success: true, timestamp: _context.CurrentTime);
        }

        _logger?.LogDebug($"NPC '{_npc.Name}' (ID {_npc.Id}) completed task: {summary}");
    }

    private void OnBrainSaid(string text)
    {
        _eventBus?.Publish(new EntitySpoke(_npc.Id, _npc.Name, _npc.Position, text, 16.0f));
    }

    private void OnEntitySpoke(EntitySpoke spoke)
    {
        if (spoke.SpeakerId == _npc.Id)
        {
            return;
        }

        if (_status != BrainStatus.Running)
        {
            return;
        }

        if (_npc.Position.Distance(spoke.Position) <= spoke.HearingRadius)
        {
            OnHeard(spoke.SpeakerId, spoke.Text);
        }
    }

    private DynValue[] ConvertArgsToDynValue(object?[] args)
    {
        if (args == null || args.Length == 0 || _script == null)
        {
            return Array.Empty<DynValue>();
        }

        var dynValues = new DynValue[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            dynValues[i] = ObjectToDynValue(args[i]);
        }
        return dynValues;
    }

    private DynValue ObjectToDynValue(object? obj)
    {
        if (obj == null || _script == null)
        {
            return DynValue.Nil;
        }

        return obj switch
        {
            bool b => DynValue.NewBoolean(b),
            int i => DynValue.NewNumber(i),
            uint u => DynValue.NewNumber(u),
            long l => DynValue.NewNumber(l),
            float f => DynValue.NewNumber(f),
            double d => DynValue.NewNumber(d),
            string s => DynValue.NewString(s),
            EntityId id => DynValue.NewNumber(id.Value),
            Vec3i pos => LuaValueConverter.ToPosTable(_script, pos),
            DynValue dv => dv,
            _ => DynValue.NewString(obj.ToString() ?? "")
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventSubscription?.Dispose();

        if (_activeAction != null)
        {
            _activeAction.Cancel();
            _activeAction = null;
        }
    }

    private sealed record QueuedEvent(string HandlerName, object?[] Args);

    private sealed class FailedAction : ActionBase
    {
        private readonly string _reason;

        public override string Name { get; }

        public FailedAction(string name, string reason)
            : base(0f)
        {
            Name = name;
            _reason = reason;
        }

        protected override void OnStart() => Fail(_reason);

        protected override void OnTick(float dt)
        {
        }
    }
}
