namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;

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
    /// Validates a Lua brain script against safety and structural constraints.
    /// </summary>
    /// <param name="script">The raw Lua source code.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or a list of errors.</returns>
    ValidationResult Validate(string script);
}

/// <summary>
/// Default implementation of <see cref="IBrainValidator"/> validating stages 2 through 4
/// (size limits, syntax parsing, and top-level compilation and execution with 'main' check).
/// </summary>
public class BrainValidator : IBrainValidator
{
    private static readonly Regex LineNumberRegex = new(
        @"(?:(?:chunk|line|at|column)\s*:?\s*|\(|:)(\d+)(?:,\s*(\d+)\)?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly BrainLimitsConfig _limitsConfig;
    private readonly ILuaRuntime _runtime;

    public BrainValidator(BrainLimitsConfig? limitsConfig = null, ILuaRuntime? runtime = null)
    {
        _limitsConfig = limitsConfig ?? new BrainLimitsConfig();
        _runtime = runtime ?? new MoonSharpRuntime(_limitsConfig);
    }

    /// <inheritdoc />
    public virtual ValidationResult Validate(string script)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(script))
        {
            errors.Add(new ValidationError(2, 1, "Brain script cannot be empty."));
            return ValidationResult.Failure(errors);
        }

        // 1. Stage 2: Size <= 8 KB
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

        // Register default API bindings and prelude so API symbols exist during compile/execution
        var dummyNpc = new Npc.NpcState(new Entities.EntityId(1), "ValidatorDummy", Vec3.Zero);
        var dummyContext = new NpcBrainContext(dummyNpc);
        LuaApiRegistry.Default.RegisterIntoScript(luaScript, dummyContext);

        // 3. Stage 3 & 4: Top-level compilation and execution
        try
        {
            luaScript.DoString(script);
        }
        catch (SyntaxErrorException synEx)
        {
            int? line = ExtractLineNumber(synEx.DecoratedMessage ?? synEx.Message);
            errors.Add(new ValidationError(3, line, synEx.Message));
            return ValidationResult.Failure(errors);
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
            errors.Add(new ValidationError(3, line, ex.Message));
            return ValidationResult.Failure(errors);
        }

        // 4. Stage 4: Top-level contract enforcement: must define global function 'main'
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

        return ValidationResult.Success();
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
