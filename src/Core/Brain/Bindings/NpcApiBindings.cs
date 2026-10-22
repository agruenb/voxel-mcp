namespace VoxelKing.Core.Brain.Bindings;

using System;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;

/// <summary>
/// Lua API bindings for the <c>npc.*</c> and <c>log(...)</c> namespace (LUA_BRAIN_API §3.1, §3.2, §3.3).
/// </summary>
public static class NpcApiBindings
{
    #region 3.1 npc — self (non-blocking queries)

    [LuaApi("npc.id", "Own id", returns: "entity_id", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Id(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DynValue.NewNumber(context.Npc.Id.Value);
    }

    [LuaApi("npc.name", "Own name", returns: "string", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Name(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DynValue.NewString(context.Npc.Name);
    }

    [LuaApi("npc.pos", "Block position of the feet", returns: "pos", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Pos(Script script, INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        return LuaValueConverter.ToPosTable(script, context.Npc.Position.FloorToInt());
    }

    [LuaApi("npc.home", "Home/spawn position", returns: "pos", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Home(Script script, INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        return LuaValueConverter.ToPosTable(script, context.Npc.Home.FloorToInt());
    }

    [LuaApi("npc.health", "Current health (0..100)", returns: "number 0..100", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Health(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DynValue.NewNumber(context.Npc.Health);
    }

    [LuaApi("npc.inventory", "Copy of the inventory as {item_id = count}", returns: "table {item_id = count}", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Inventory(Script script, INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);

        var table = new Table(script);
        if (context.Npc.Inventory != null)
        {
            foreach (var kvp in context.Npc.Inventory.GetAllCounts())
            {
                table[kvp.Key] = kvp.Value;
            }
        }
        return DynValue.NewTable(table);
    }

    [LuaApi("npc.count", "Number of items held with specified item_id", parameters: "item_id", returns: "integer", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Count(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string itemId = args.AsType(0, "npc.count", DataType.String).String;
        int count = context.Npc.Inventory?.CountItem(itemId) ?? 0;
        return DynValue.NewNumber(count);
    }

    [LuaApi("npc.free_slots", "Number of free inventory slots", returns: "integer", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue FreeSlots(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int free = context.Npc.Inventory?.FreeSlots ?? 0;
        return DynValue.NewNumber(free);
    }

    [LuaApi("npc.instructor", "Entity that gave the current instruction, or nil", returns: "entity_id", isBlocking: false, allowedInHandler: true, category: "npc.self")]
    public static DynValue Instructor(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Npc.InstructorId.HasValue
            ? DynValue.NewNumber(context.Npc.InstructorId.Value.Value)
            : DynValue.Nil;
    }

    #endregion

    #region 3.2 npc — blocking actions (only inside main)

    [LuaApi("npc.go_to", "Walk to pos. opts.tolerance (default 0), opts.timeout, opts.allow_partial", parameters: "pos [, opts]", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void GoTo() { }

    [LuaApi("npc.go_near", "Walk until within dist blocks of pos", parameters: "pos, dist", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void GoNear() { }

    [LuaApi("npc.follow", "Follow an entity for a duration (seconds = 0 -> until gone or interrupted)", parameters: "entity_id, dist, seconds", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Follow() { }

    [LuaApi("npc.mine", "Break the block at pos (must be within 4 blocks); drop goes into inventory", parameters: "pos", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Mine() { }

    [LuaApi("npc.place", "Place a block from inventory (within 4 blocks, target must be air and adjacent to solid)", parameters: "pos, block_id", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Place() { }

    [LuaApi("npc.pick_up", "Pick up a dropped item entity (walks to it)", parameters: "entity_id", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void PickUp() { }

    [LuaApi("npc.drop", "Drop items at feet", parameters: "item_id, count", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Drop() { }

    [LuaApi("npc.give", "Hand items to an entity within 3 blocks", parameters: "entity_id, item_id, count", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Give() { }

    [LuaApi("npc.deposit", "Put items into container at pos (within 4 blocks). count = -1 -> all", parameters: "pos, item_id, count", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Deposit() { }

    [LuaApi("npc.withdraw", "Take items from a container at pos", parameters: "pos, item_id, count", returns: "ok, reason", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Withdraw() { }

    [LuaApi("npc.wait", "Idle for the given time in seconds (max 600)", parameters: "seconds", returns: "true", isBlocking: true, allowedInHandler: false, category: "npc.actions")]
    public static void Wait() { }

    #endregion

    #region 3.3 npc — instantaneous / control

    [LuaApi("npc.say", "Speak (max 200 chars). Heard within 16 blocks. Rate-limited to 1 per 2 s", parameters: "text", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue Say(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string text = args.Count > 0 ? args[0].ToPrintString() : string.Empty;
        context.Say(text);
        return DynValue.Nil;
    }

    [LuaApi("npc.look_at", "Turn head/body towards a position", parameters: "pos", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue LookAt(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (args.Count == 0) throw new ScriptRuntimeException("npc.look_at expects a pos table");
        var pos = LuaValueConverter.ToVec3i(args[0]);
        context.LookAt(pos);
        return DynValue.Nil;
    }

    [LuaApi("npc.done", "Mark the task as finished; ends the brain", parameters: "[summary]", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue Done(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? summary = args.Count > 0 && !args[0].IsNil() ? args[0].ToPrintString() : null;
        context.Done(summary);
        return DynValue.Nil;
    }

    [LuaApi("npc.restart", "Restart main() from the top (callable from handlers)", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue Restart(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RequestRestart();
        return DynValue.Nil;
    }

    [LuaApi("npc.interrupt", "Cancel currently running blocking action (returns false, 'interrupted')", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue Interrupt(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Interrupt();
        return DynValue.Nil;
    }

    [LuaApi("log", "Write to the NPC debug log (inspector overlay)", parameters: "...", isBlocking: false, allowedInHandler: true, category: "npc.control")]
    public static DynValue Log(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (args.Count == 0)
        {
            context.Log(string.Empty);
            return DynValue.Nil;
        }

        var parts = new string[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            parts[i] = args[i].ToPrintString();
        }
        context.Log(string.Join(" ", parts));
        return DynValue.Nil;
    }

    #endregion
}
