namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MoonSharp.Interpreter;
using VoxelKing.Core.Brain.Bindings;

/// <summary>
/// Authoritative registry of Lua brain API bindings (ARCHITECTURE §5.4).
/// Discovers [LuaApi] annotations, binds functions to sandboxed scripts, and generates
/// API documentation for prompts and snapshot tests.
/// </summary>
public sealed class LuaApiRegistry
{
    private static readonly Lazy<LuaApiRegistry> LazyDefault = new(CreateDefault);

    private readonly Dictionary<string, LuaApiEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the shared default registry containing all standard API bindings from LUA_BRAIN_API §3.
    /// </summary>
    public static LuaApiRegistry Default => LazyDefault.Value;

    /// <summary>
    /// Creates a new default registry populated with all standard API bindings.
    /// </summary>
    public static LuaApiRegistry CreateDefault()
    {
        var registry = new LuaApiRegistry();
        registry.RegisterFromType(typeof(NpcApiBindings));
        registry.RegisterFromType(typeof(WorldApiBindings));
        registry.RegisterFromType(typeof(MemoryApiBindings));
        registry.RegisterFromType(typeof(UtilityApiBindings));
        return registry;
    }

    /// <summary>
    /// Registers a single API entry.
    /// </summary>
    public void Register(LuaApiEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Name] = entry;
    }

    /// <summary>
    /// Reflects over public static methods on <paramref name="type"/> and registers all methods
    /// annotated with <see cref="LuaApiAttribute"/>.
    /// </summary>
    public void RegisterFromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<LuaApiAttribute>();
            if (attr != null)
            {
                Register(LuaApiEntry.FromAttribute(attr, method));
            }
        }
    }

    /// <summary>
    /// Retrieves an entry by function name, or null if not registered.
    /// </summary>
    public LuaApiEntry? GetEntry(string name)
    {
        return _entries.TryGetValue(name, out var entry) ? entry : null;
    }

    /// <summary>
    /// Gets all registered API entries.
    /// </summary>
    public IReadOnlyList<LuaApiEntry> GetAllEntries()
    {
        return _entries.Values.ToList();
    }

    /// <summary>
    /// Binds all API functions to the specified sandboxed MoonSharp <see cref="Script"/> instance,
    /// wired to the provided <see cref="INpcBrainContext"/>.
    /// </summary>
    public void RegisterIntoScript(Script script, INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);

        // 1. Temporarily provide __begin_action and __yield callbacks for the prelude to close over
        script.Globals["__yield"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) =>
        {
            var yieldArgs = new DynValue[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                yieldArgs[i] = args[i];
            }
            return DynValue.NewYieldReq(yieldArgs);
        });

        script.Globals["__begin_action"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) =>
        {
            string actionName = args.Count > 0 ? (args[0].String ?? args[0].ToPrintString()) : "unknown";
            if (context.IsInsideHandler)
            {
                throw new ScriptRuntimeException(
                    $"blocking function 'npc.{actionName}' cannot be called from an event handler");
            }

            ValidateActionArguments(actionName, args);

            var request = context.BeginAction(actionName, args);
            return DynValue.NewString(request.ActionName);
        });

        // Initialize API tables on Globals before running the prelude
        var npcTable = new Table(script);
        script.Globals["npc"] = npcTable;

        var worldTable = new Table(script);
        script.Globals["world"] = worldTable;

        var memoryTable = new Table(script);
        script.Globals["memory"] = memoryTable;

        // 2. Execute prelude (defines pos metatable, vec, dist, and yielding action wrappers)
        script.DoString(LuaPrelude.Source);

        // 3. Remove __begin_action, __yield, and coroutine from globals so user scripts cannot call them
        script.Globals.Remove("__begin_action");
        script.Globals.Remove("__yield");
        script.Globals.Remove("coroutine");

        // 4. Bind npc.* namespace
        npcTable["id"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Id(context));
        npcTable["name"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Name(context));
        npcTable["pos"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Pos(script, context));
        npcTable["home"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Home(script, context));
        npcTable["health"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Health(context));
        npcTable["inventory"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Inventory(script, context));
        npcTable["count"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Count(context, args));
        npcTable["free_slots"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.FreeSlots(context));
        npcTable["instructor"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Instructor(context));
        npcTable["say"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Say(context, args));
        npcTable["look_at"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.LookAt(context, args));
        npcTable["done"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Done(context, args));
        npcTable["restart"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Restart(context));
        npcTable["interrupt"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Interrupt(context));

        // 5. Bind log(...)
        script.Globals["log"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => NpcApiBindings.Log(context, args));

        // 6. Bind world.* namespace
        worldTable["block"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.Block(context, args));
        worldTable["find_block"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.FindBlock(script, context, args));
        worldTable["find_blocks"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.FindBlocks(script, context, args));
        worldTable["find_entity"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.FindEntity(context, args));
        worldTable["find_entities"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.FindEntities(script, context, args));
        worldTable["entity"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.Entity(script, context, args));
        worldTable["is_walkable"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.IsWalkable(context, args));
        worldTable["surface"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.Surface(script, context, args));
        worldTable["time"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.Time(context));
        worldTable["is_day"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => WorldApiBindings.IsDay(context));

        // 7. Bind memory.* namespace
        memoryTable["get"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => MemoryApiBindings.Get(script, context, args));
        memoryTable["set"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => MemoryApiBindings.Set(context, args));
        memoryTable["place"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => MemoryApiBindings.Place(script, context, args));
        memoryTable["remember_place"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => MemoryApiBindings.RememberPlace(context, args));
        memoryTable["places"] = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)((sc, args) => MemoryApiBindings.Places(script, context));

        // 8. Protect API tables against setmetatable / getmetatable tampering
        LuaSandbox.ProtectTable(npcTable, script);
        LuaSandbox.ProtectTable(worldTable, script);
        LuaSandbox.ProtectTable(memoryTable, script);
    }

    /// <summary>
    /// Generates Markdown documentation for all registered API bindings, grouped by category.
    /// </summary>
    public string GenerateMarkdownReference()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Lua Brain API Reference");
        sb.AppendLine();

        // 3.1 npc self
        sb.AppendLine("### 3.1 `npc` — self (non-blocking queries)");
        sb.AppendLine();
        sb.AppendLine("| Function | Returns | Description |");
        sb.AppendLine("|---|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "npc.self").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            sb.AppendLine($"| `{signature}` | `{entry.Returns}` | {entry.Description} |");
        }
        sb.AppendLine();

        // 3.2 npc actions
        sb.AppendLine("### 3.2 `npc` — blocking actions (only inside `main`)");
        sb.AppendLine();
        sb.AppendLine("| Function | Description |");
        sb.AppendLine("|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "npc.actions").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            string ret = !string.IsNullOrEmpty(entry.Returns) ? $" → {entry.Returns}" : string.Empty;
            sb.AppendLine($"| `{signature}`{ret} | {entry.Description} |");
        }
        sb.AppendLine();

        // 3.3 npc control
        sb.AppendLine("### 3.3 `npc` — instantaneous / control");
        sb.AppendLine();
        sb.AppendLine("| Function | Description |");
        sb.AppendLine("|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "npc.control").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            sb.AppendLine($"| `{signature}` | {entry.Description} |");
        }
        sb.AppendLine();

        // 3.4 world
        sb.AppendLine("### 3.4 `world` — perception (non-blocking, limited by perception radius)");
        sb.AppendLine();
        sb.AppendLine("| Function | Returns | Description |");
        sb.AppendLine("|---|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "world").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            sb.AppendLine($"| `{signature}` | `{entry.Returns}` | {entry.Description} |");
        }
        sb.AppendLine();

        // 3.5 memory
        sb.AppendLine("### 3.5 `memory` — persistent per-NPC storage");
        sb.AppendLine();
        sb.AppendLine("| Function | Description |");
        sb.AppendLine("|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "memory").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            sb.AppendLine($"| `{signature}` | {entry.Description} |");
        }
        sb.AppendLine();

        // 3.6 utilities
        sb.AppendLine("### 3.6 Utilities");
        sb.AppendLine();
        sb.AppendLine("| Function | Description |");
        sb.AppendLine("|---|---|");
        foreach (var entry in _entries.Values.Where(e => e.Category == "utility").OrderBy(e => e.Name))
        {
            string signature = FormatSignature(entry);
            sb.AppendLine($"| `{signature}` | {entry.Description} |");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates plaintext documentation suitable for inclusion in LLM prompt templates.
    /// </summary>
    public string GeneratePlaintextReference()
    {
        var sb = new StringBuilder();
        foreach (var entry in _entries.Values.OrderBy(e => e.Name))
        {
            string sig = FormatSignature(entry);
            string ret = !string.IsNullOrEmpty(entry.Returns) ? $" -> {entry.Returns}" : string.Empty;
            string blockingTag = entry.IsBlocking ? " [BLOCKING]" : string.Empty;
            sb.AppendLine($"{sig}{ret}{blockingTag} -- {entry.Description}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatSignature(LuaApiEntry entry)
    {
        return string.IsNullOrEmpty(entry.Parameters)
            ? $"{entry.Name}()"
            : $"{entry.Name}({entry.Parameters})";
    }

    private static void ValidateActionArguments(string actionName, CallbackArguments args)
    {
        switch (actionName)
        {
            case "go_to":
                if (args.Count < 2) throw new ScriptRuntimeException("bad argument #1 to 'npc.go_to' (pos expected)");
                LuaValueConverter.ToVec3i(args[1]);
                if (args.Count > 2 && args[2].Type != DataType.Nil && args[2].Type != DataType.Table)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.go_to' (table expected, got {args[2].Type.ToLuaTypeString()})");
                break;

            case "go_near":
                if (args.Count < 3) throw new ScriptRuntimeException("bad arguments to 'npc.go_near' (pos and dist expected)");
                LuaValueConverter.ToVec3i(args[1]);
                if (args[2].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.go_near' (number expected, got {args[2].Type.ToLuaTypeString()})");
                break;

            case "follow":
                if (args.Count < 4) throw new ScriptRuntimeException("bad arguments to 'npc.follow' (entity_id, dist, seconds expected)");
                if (args[1].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #1 to 'npc.follow' (number expected, got {args[1].Type.ToLuaTypeString()})");
                if (args[2].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.follow' (number expected, got {args[2].Type.ToLuaTypeString()})");
                if (args[3].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #3 to 'npc.follow' (number expected, got {args[3].Type.ToLuaTypeString()})");
                break;

            case "mine":
                if (args.Count < 2) throw new ScriptRuntimeException("bad argument #1 to 'npc.mine' (pos expected)");
                LuaValueConverter.ToVec3i(args[1]);
                break;

            case "place":
                if (args.Count < 3) throw new ScriptRuntimeException("bad arguments to 'npc.place' (pos and block_id expected)");
                LuaValueConverter.ToVec3i(args[1]);
                if (args[2].Type != DataType.String)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.place' (string expected, got {args[2].Type.ToLuaTypeString()})");
                break;

            case "pick_up":
                if (args.Count < 2) throw new ScriptRuntimeException("bad argument #1 to 'npc.pick_up' (entity_id expected)");
                if (args[1].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #1 to 'npc.pick_up' (number expected, got {args[1].Type.ToLuaTypeString()})");
                break;

            case "drop":
                if (args.Count < 3) throw new ScriptRuntimeException("bad arguments to 'npc.drop' (item_id and count expected)");
                if (args[1].Type != DataType.String)
                    throw new ScriptRuntimeException($"bad argument #1 to 'npc.drop' (string expected, got {args[1].Type.ToLuaTypeString()})");
                if (args[2].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.drop' (number expected, got {args[2].Type.ToLuaTypeString()})");
                break;

            case "give":
                if (args.Count < 4) throw new ScriptRuntimeException("bad arguments to 'npc.give' (entity_id, item_id, count expected)");
                if (args[1].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #1 to 'npc.give' (number expected, got {args[1].Type.ToLuaTypeString()})");
                if (args[2].Type != DataType.String)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.give' (string expected, got {args[2].Type.ToLuaTypeString()})");
                if (args[3].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #3 to 'npc.give' (number expected, got {args[3].Type.ToLuaTypeString()})");
                break;

            case "deposit":
                if (args.Count < 4) throw new ScriptRuntimeException("bad arguments to 'npc.deposit' (pos, item_id, count expected)");
                LuaValueConverter.ToVec3i(args[1]);
                if (args[2].Type != DataType.String)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.deposit' (string expected, got {args[2].Type.ToLuaTypeString()})");
                if (args[3].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #3 to 'npc.deposit' (number expected, got {args[3].Type.ToLuaTypeString()})");
                break;

            case "withdraw":
                if (args.Count < 4) throw new ScriptRuntimeException("bad arguments to 'npc.withdraw' (pos, item_id, count expected)");
                LuaValueConverter.ToVec3i(args[1]);
                if (args[2].Type != DataType.String)
                    throw new ScriptRuntimeException($"bad argument #2 to 'npc.withdraw' (string expected, got {args[2].Type.ToLuaTypeString()})");
                if (args[3].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #3 to 'npc.withdraw' (number expected, got {args[3].Type.ToLuaTypeString()})");
                break;

            case "wait":
                if (args.Count < 2) throw new ScriptRuntimeException("bad argument #1 to 'npc.wait' (seconds expected)");
                if (args[1].Type != DataType.Number)
                    throw new ScriptRuntimeException($"bad argument #1 to 'npc.wait' (number expected, got {args[1].Type.ToLuaTypeString()})");
                break;
        }
    }
}
