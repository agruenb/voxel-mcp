namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;

/// <summary>
/// A single validation error emitted by <see cref="IBrainValidator"/> (LLM_INTEGRATION §4).
/// </summary>
public readonly record struct ValidationError(int Stage, int? Line, string Message);

/// <summary>
/// Result of a brain validation operation, indicating whether the script passed or containing validation errors.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the brain script passed all validation stages without errors.
    /// </summary>
    public bool Ok => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors if validation failed.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationResult(IReadOnlyList<ValidationError>? errors = null)
    {
        Errors = errors ?? Array.Empty<ValidationError>();
    }

    public static ValidationResult Success() => new(Array.Empty<ValidationError>());

    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);

    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(errors.ToArray());

    public override string ToString()
    {
        if (Ok) return "ValidationResult: OK";
        return $"ValidationResult: Failed ({Errors.Count} error(s)):\n" +
               string.Join("\n", Errors.Select(e => $"  [Stage {e.Stage}{(e.Line.HasValue ? $", Line {e.Line}" : "")}] {e.Message}"));
    }
}

/// <summary>
/// Interface for brain script validation pipelines (LLM_INTEGRATION §4).
/// </summary>
public interface IBrainValidator
{
    /// <summary>
    /// Validates a Lua brain script against safety and structural constraints (Stages 2–6).
    /// </summary>
    /// <param name="script">The raw Lua source code.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or a list of errors.</returns>
    ValidationResult Validate(string script);
}

/// <summary>
/// Default implementation of <see cref="IBrainValidator"/> implementing LLM_INTEGRATION §4 stages 2–6:
/// Stage 2 (size limit), Stage 3 (compile &amp; syntax), Stage 4 (top-level execution budget, main definition, allowed globals),
/// Stage 5 (dry run against <see cref="MockWorld"/>, handler checks, busy loop detection), and Stage 6 (ValidationResult).
/// </summary>
public class BrainValidator : IBrainValidator
{
    public const int MaxDryRunResumes = 200;
    public const int MaxDryRunInstructions = 200_000;

    private static readonly Regex LineNumberRegex = new(
        @"(?:(?:chunk|line|at|column)\s*:?\s*|\(|:)(\d+)(?:,\s*(\d+)\)?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly BrainLimitsConfig _limitsConfig;
    private readonly ILuaRuntime _runtime;
    private readonly LuaApiRegistry _registry;
    private readonly Func<MockWorld>? _mockWorldFactory;

    public BrainValidator(
        BrainLimitsConfig? limitsConfig = null,
        ILuaRuntime? runtime = null,
        LuaApiRegistry? registry = null,
        Func<MockWorld>? mockWorldFactory = null)
    {
        _limitsConfig = limitsConfig ?? new BrainLimitsConfig();
        _runtime = runtime ?? new MoonSharpRuntime(_limitsConfig);
        _registry = registry ?? LuaApiRegistry.Default;
        _mockWorldFactory = mockWorldFactory;
    }

    /// <inheritdoc />
    public virtual ValidationResult Validate(string script)
    {
        var errors = new List<ValidationError>();

        // 1. Stage 2: Size <= 8 KB and non-empty check
        if (string.IsNullOrWhiteSpace(script))
        {
            errors.Add(new ValidationError(2, 1, "Brain script cannot be empty."));
            return ValidationResult.Failure(errors);
        }

        int byteCount = Encoding.UTF8.GetByteCount(script);
        if (byteCount > _limitsConfig.MaxScriptSizeBytes)
        {
            errors.Add(new ValidationError(2, null,
                $"Script size ({byteCount} bytes) exceeds limit ({_limitsConfig.MaxScriptSizeBytes} bytes)."));
            return ValidationResult.Failure(errors);
        }

        // 2. Stage 3: Sandbox creation & compilation
        Script luaScript;
        try
        {
            luaScript = _runtime.CreateScript();
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError(3, null, $"Failed to initialize Lua sandbox: {ex.Message}"));
            return ValidationResult.Failure(errors);
        }

        // Setup mock environment (MockWorld, dummy NPC, inventory, memory)
        var mockWorld = _mockWorldFactory != null ? _mockWorldFactory() : new MockWorld(new Vec3(0, 64, 0));
        var inventory = new Inventory(16);
        inventory.AddItem("oak_log", 5, out _);
        inventory.AddItem("stone", 10, out _);
        inventory.AddItem("cobblestone", 10, out _);
        inventory.AddItem("wheat", 5, out _);

        var memory = new NpcMemory(_limitsConfig);
        memory.RememberPlace("home", new Vec3i(0, 64, 0));
        memory.RememberPlace("player_chest", new Vec3i(5, 64, 0));

        var dummyNpc = new NpcState(
            new EntityId(1),
            "ValidatorDummy",
            new Vec3(0, 64, 0),
            home: new Vec3(0, 64, 0),
            inventory: inventory,
            memory: memory);
        dummyNpc.InstructorId = new EntityId(100);
        dummyNpc.Perception = mockWorld;

        var dummyContext = new NpcBrainContext(dummyNpc);
        _registry.RegisterIntoScript(luaScript, dummyContext);

        // Record initial sandbox globals to verify only allowed symbols are declared at top level
        var initialGlobals = new HashSet<string>();
        foreach (var pair in luaScript.Globals.Pairs)
        {
            if (pair.Key.Type == DataType.String)
            {
                initialGlobals.Add(pair.Key.String);
            }
        }

        // Load and compile script chunk (Stage 3 syntax validation)
        DynValue topLevelChunk;
        try
        {
            topLevelChunk = luaScript.LoadString(script);
        }
        catch (SyntaxErrorException synEx)
        {
            int? line = ExtractLineNumber(synEx.DecoratedMessage ?? synEx.Message);
            errors.Add(new ValidationError(3, line, synEx.Message));
            return ValidationResult.Failure(errors);
        }
        catch (Exception ex)
        {
            int? line = ExtractLineNumber(ex.Message);
            errors.Add(new ValidationError(3, line, ex.Message));
            return ValidationResult.Failure(errors);
        }

        // 3. Stage 4: Top-level execution with 10 000-instruction budget
        var topLevelCoro = luaScript.CreateCoroutine(topLevelChunk);
        topLevelCoro.Coroutine.AutoYieldCounter = _limitsConfig.MaxTopLevelChunkInstructions;
        try
        {
            topLevelCoro.Coroutine.Resume();
            if (topLevelCoro.Coroutine.State is CoroutineState.ForceSuspended or CoroutineState.Suspended)
            {
                errors.Add(new ValidationError(4, null,
                    $"Top-level instruction budget exceeded ({_limitsConfig.MaxTopLevelChunkInstructions} instructions)."));
                return ValidationResult.Failure(errors);
            }
        }
        catch (ScriptRuntimeException runEx)
        {
            int? line = ExtractLineNumber(runEx.DecoratedMessage ?? runEx.Message);
            errors.Add(new ValidationError(4, line, runEx.Message));
            return ValidationResult.Failure(errors);
        }
        catch (Exception ex)
        {
            int? line = ExtractLineNumber(ex.Message);
            errors.Add(new ValidationError(4, line, ex.Message));
            return ValidationResult.Failure(errors);
        }

        // Contract: must define global function 'main'
        var mainVal = luaScript.Globals.Get("main");
        if (mainVal.IsNil())
        {
            errors.Add(new ValidationError(4, null, "Script must define a global function 'main()'."));
            return ValidationResult.Failure(errors);
        }

        if (mainVal.Type != DataType.Function)
        {
            errors.Add(new ValidationError(4, null, $"'main' must be a function, but found {mainVal.Type}."));
            return ValidationResult.Failure(errors);
        }

        // Contract: only main, on_* handlers, and locals may be defined at top level
        foreach (var pair in luaScript.Globals.Pairs)
        {
            if (pair.Key.Type != DataType.String) continue;
            string key = pair.Key.String;
            if (initialGlobals.Contains(key)) continue;

            if (key == "main") continue;

            if (key.StartsWith("on_"))
            {
                if (pair.Value.Type != DataType.Function)
                {
                    errors.Add(new ValidationError(4, null, $"Handler '{key}' must be a function, but found {pair.Value.Type}."));
                }
                continue;
            }

            errors.Add(new ValidationError(4, null,
                $"Only 'main', 'on_*' handlers and locals may be defined at top level; unauthorized global '{key}' defined."));
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors);
        }

        // 4. Stage 5: Dry run against MockWorld (up to 200 resumes / 200 000 instructions)
        var mainCoro = luaScript.CreateCoroutine(mainVal);
        int resumeCount = 0;
        int totalInstructions = 0;
        int consecutiveForcedYields = 0;
        DynValue[] resumeArgs = Array.Empty<DynValue>();

        while (resumeCount < MaxDryRunResumes && totalInstructions < MaxDryRunInstructions)
        {
            resumeCount++;
            dummyContext.ClearLastActionRequest();
            mainCoro.Coroutine.AutoYieldCounter = _limitsConfig.MaxInstructionsPerTick;

            try
            {
                if (resumeArgs.Length > 0)
                {
                    mainCoro.Coroutine.Resume(resumeArgs);
                }
                else
                {
                    mainCoro.Coroutine.Resume();
                }
            }
            catch (ScriptRuntimeException ex)
            {
                int? line = ExtractLineNumber(ex.DecoratedMessage ?? ex.Message);
                errors.Add(new ValidationError(5, line, ex.Message));
                return ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                int? line = ExtractLineNumber(ex.Message);
                errors.Add(new ValidationError(5, line, ex.Message));
                return ValidationResult.Failure(errors);
            }

            if (mainCoro.Coroutine.State == CoroutineState.Dead || dummyContext.IsDone)
            {
                break; // main() completed normally or called npc.done()
            }

            if (mainCoro.Coroutine.State is CoroutineState.Suspended or CoroutineState.ForceSuspended)
            {
                if (mainCoro.Coroutine.State == CoroutineState.Suspended && dummyContext.LastActionRequest != null)
                {
                    // Action requested: mock actions succeed
                    consecutiveForcedYields = 0;
                    var req = dummyContext.LastActionRequest;
                    if (req.ActionName == "wait")
                    {
                        resumeArgs = [DynValue.True];
                    }
                    else
                    {
                        resumeArgs = [DynValue.True, DynValue.Nil];
                    }
                }
                else
                {
                    // Forced yield by AutoYieldCounter
                    consecutiveForcedYields++;
                    totalInstructions += _limitsConfig.MaxInstructionsPerTick;

                    if (consecutiveForcedYields >= _limitsConfig.MaxConsecutiveForcedYields || totalInstructions >= MaxDryRunInstructions)
                    {
                        errors.Add(new ValidationError(5, null, "busy loop"));
                        return ValidationResult.Failure(errors);
                    }

                    resumeArgs = Array.Empty<DynValue>();
                }
            }
            else
            {
                break;
            }
        }

        // 5. Stage 5: Dry run each event handler once with sample arguments
        foreach (var pair in luaScript.Globals.Pairs)
        {
            if (pair.Key.Type != DataType.String) continue;
            string key = pair.Key.String;
            if (!key.StartsWith("on_") || pair.Value.Type != DataType.Function) continue;

            var handlerFunc = pair.Value;
            dummyContext.IsInsideHandler = true;
            try
            {
                var handlerCoro = luaScript.CreateCoroutine(handlerFunc);
                handlerCoro.Coroutine.AutoYieldCounter = _limitsConfig.MaxInstructionsPerHandler;
                var sampleArgs = GetSampleHandlerArguments(key);
                handlerCoro.Coroutine.Resume(sampleArgs);

                if (handlerCoro.Coroutine.State == CoroutineState.ForceSuspended)
                {
                    errors.Add(new ValidationError(5, null,
                        $"Handler '{key}' exceeded instruction budget ({_limitsConfig.MaxInstructionsPerHandler} instructions)."));
                    return ValidationResult.Failure(errors);
                }

                if (handlerCoro.Coroutine.State == CoroutineState.Suspended)
                {
                    errors.Add(new ValidationError(5, null,
                        $"Handler '{key}' attempted to yield, which is forbidden in event handlers."));
                    return ValidationResult.Failure(errors);
                }
            }
            catch (ScriptRuntimeException ex)
            {
                int? line = ExtractLineNumber(ex.DecoratedMessage ?? ex.Message);
                errors.Add(new ValidationError(5, line, ex.Message));
                return ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                int? line = ExtractLineNumber(ex.Message);
                errors.Add(new ValidationError(5, line, ex.Message));
                return ValidationResult.Failure(errors);
            }
            finally
            {
                dummyContext.IsInsideHandler = false;
            }
        }

        return ValidationResult.Success();
    }

    private static DynValue[] GetSampleHandlerArguments(string handlerName)
    {
        return handlerName switch
        {
            "on_heard" => [DynValue.NewNumber(100), DynValue.NewString("sample heard text")],
            "on_attacked" => [DynValue.NewNumber(100), DynValue.NewNumber(5.0)],
            "on_player_nearby" => [DynValue.NewNumber(100)],
            "on_interrupted" => [DynValue.NewString("reflex")],
            _ => [DynValue.NewNumber(100), DynValue.NewString("sample"), DynValue.NewNumber(1.0)]
        };
    }

    private static int? ExtractLineNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = LineNumberRegex.Match(text);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int line))
        {
            return line;
        }

        return null;
    }
}
