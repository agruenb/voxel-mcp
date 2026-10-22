namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;

/// <summary>
/// Configures and secures MoonSharp <see cref="Script"/> instances according to ARCHITECTURE §5.4.
/// Enforces sandbox preset, removal of dangerous globals and modules, protected metatables,
/// deterministic random number generation, and an unknown-global trap metamethod.
/// </summary>
public static class LuaSandbox
{
    /// <summary>
    /// List of global names explicitly forbidden and removed from the sandbox environment.
    /// </summary>
    public static readonly IReadOnlyList<string> ForbiddenGlobals = new[]
    {
        "io",
        "os",
        "load",
        "loadstring",
        "dofile",
        "require",
        "debug",
        "coroutine",
        "print",
        "rawset",
        "rawget",
        "package",
        "dynamic",
        "json",
        "collectgarbage",
        "module",
        "loadfile",
        "setfenv",
        "getfenv",
    };

    /// <summary>
    /// Creates and initializes a newly sandboxed MoonSharp <see cref="Script"/> instance.
    /// </summary>
    /// <param name="seed">Optional seed for deterministic random generation.</param>
    /// <param name="randomProvider">Optional custom random provider.</param>
    /// <returns>A fully configured and sandboxed <see cref="Script"/> instance.</returns>
    public static Script Create(int? seed = null, IRandomProvider? randomProvider = null)
    {
        var script = new Script(CoreModules.Preset_SoftSandbox);

        // Harden script options: disable file system loader and external debug print
        script.Options.ScriptLoader = null;
        script.Options.DebugPrint = null;

        Configure(script, seed, randomProvider);

        return script;
    }

    /// <summary>
    /// Configures an existing <see cref="Script"/> instance with sandbox restrictions:
    /// removing forbidden globals, trapping unknown globals, and binding deterministic random.
    /// </summary>
    /// <param name="script">The script to configure.</param>
    /// <param name="seed">Optional seed for deterministic random generation.</param>
    /// <param name="randomProvider">Optional custom random provider.</param>
    public static void Configure(Script script, int? seed = null, IRandomProvider? randomProvider = null)
    {
        ArgumentNullException.ThrowIfNull(script);

        // Remove forbidden globals
        foreach (var globalName in ForbiddenGlobals)
        {
            script.Globals.Remove(globalName);
        }

        // Remove string.dump and protect string table
        var stringTable = script.Globals.Get("string").Table;
        if (stringTable != null)
        {
            stringTable.Remove("dump");
            ProtectTable(stringTable, script);
        }

        // Remove math.randomseed and protect math table
        var mathTable = script.Globals.Get("math").Table;
        if (mathTable != null)
        {
            mathTable.Remove("randomseed");
            ProtectTable(mathTable, script);
        }

        // Protect table standard library table
        var tableTable = script.Globals.Get("table").Table;
        if (tableTable != null)
        {
            ProtectTable(tableTable, script);
        }

        // Bind deterministic random provider
        var provider = randomProvider ?? new DefaultRandomProvider(seed);
        BindDeterministicRandom(script, provider);

        // Configure unknown-global trap on _G via metatable
        var meta = new Table(script);
        meta["__index"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((context, args) =>
        {
            var key = args.Count > 1
                ? (args[1].String ?? args[1].ToPrintString())
                : "unknown";
            throw new ScriptRuntimeException($"unknown global '{key}'");
        });
        meta["__metatable"] = "protected";
        script.Globals.MetaTable = meta;
    }

    /// <summary>
    /// Protects a table's metatable from inspection or modification via Lua's
    /// getmetatable and setmetatable functions by setting __metatable = "protected".
    /// </summary>
    /// <param name="table">The table to protect.</param>
    /// <param name="script">The owning script instance.</param>
    public static void ProtectTable(Table table, Script script)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(script);

        var meta = table.MetaTable ?? new Table(script);
        meta["__metatable"] = "protected";
        table.MetaTable = meta;
    }

    private static void BindDeterministicRandom(Script script, IRandomProvider randomProvider)
    {
        var randomCallback = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((context, args) =>
        {
            if (args.Count == 0)
            {
                return DynValue.NewNumber(randomProvider.NextSingle());
            }

            if (args[0].Type != DataType.Number)
            {
                throw new ScriptRuntimeException(
                    $"bad argument #1 to 'random' (number expected, got {args[0].Type.ToLuaTypeString()})");
            }

            if (args.Count == 1)
            {
                int max = (int)args[0].Number;
                if (max < 1)
                {
                    throw new ScriptRuntimeException("bad argument #1 to 'random' (interval is empty)");
                }
                return DynValue.NewNumber(randomProvider.Next(1, max + 1));
            }

            if (args[1].Type != DataType.Number)
            {
                throw new ScriptRuntimeException(
                    $"bad argument #2 to 'random' (number expected, got {args[1].Type.ToLuaTypeString()})");
            }

            int a = (int)args[0].Number;
            int b = (int)args[1].Number;
            if (a > b)
            {
                throw new ScriptRuntimeException("bad argument #2 to 'random' (interval is empty)");
            }
            if (a == b)
            {
                return DynValue.NewNumber(a);
            }

            if (b == int.MaxValue)
            {
                long range = (long)b - a + 1;
                long val = a + (long)Math.Floor(randomProvider.NextSingle() * range);
                return DynValue.NewNumber(val);
            }

            return DynValue.NewNumber(randomProvider.Next(a, b + 1));
        });

        script.Globals["random"] = randomCallback;

        // Route math.random to deterministic random as well
        var mathTable = script.Globals.Get("math").Table;
        if (mathTable != null)
        {
            mathTable["random"] = randomCallback;
        }
    }
}
