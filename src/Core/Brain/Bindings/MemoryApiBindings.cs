namespace VoxelKing.Core.Brain.Bindings;

using System;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API bindings for the <c>memory.*</c> storage namespace (LUA_BRAIN_API §3.5).
/// </summary>
public static class MemoryApiBindings
{
    [LuaApi("memory.get", "Read a value by key, returning default if absent", parameters: "key [, default]", returns: "value", isBlocking: false, allowedInHandler: true, category: "memory")]
    public static DynValue Get(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        string key = args.AsType(0, "memory.get", DataType.String).String;

        if (!context.Memory.Has(key))
        {
            return args.Count > 1 ? args[1] : DynValue.Nil;
        }

        object? val = context.Memory.Get(key);
        return LuaValueConverter.ToDynValue(script, val);
    }

    [LuaApi("memory.set", "Store a number, string, boolean, pos or flat table of those. Limits: 64 keys, 16 KB total", parameters: "key, value", isBlocking: false, allowedInHandler: true, category: "memory")]
    public static DynValue Set(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string key = args.AsType(0, "memory.set", DataType.String).String;

        if (args.Count < 2 || args[1].IsNil())
        {
            context.Memory.Delete(key);
            return DynValue.Nil;
        }

        try
        {
            object? clrObj = LuaValueConverter.ToClrObject(args[1]);
            context.Memory.Set(key, clrObj);
        }
        catch (Exception ex)
        {
            throw new ScriptRuntimeException(ex.Message);
        }

        return DynValue.Nil;
    }

    [LuaApi("memory.place", "pos of a named known place or nil (e.g. 'home', 'player_chest')", parameters: "name", returns: "pos or nil", isBlocking: false, allowedInHandler: true, category: "memory")]
    public static DynValue Place(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        string name = args.AsType(0, "memory.place", DataType.String).String;

        var pos = context.Memory.Place(name);
        return pos.HasValue ? LuaValueConverter.ToPosTable(script, pos.Value) : DynValue.Nil;
    }

    [LuaApi("memory.remember_place", "Store/overwrite a named place", parameters: "name, pos", isBlocking: false, allowedInHandler: true, category: "memory")]
    public static DynValue RememberPlace(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string name = args.AsType(0, "memory.remember_place", DataType.String).String;
        if (args.Count < 2) throw new ScriptRuntimeException("memory.remember_place expects name and pos");

        var pos = LuaValueConverter.ToVec3i(args[1]);
        context.Memory.RememberPlace(name, pos);
        return DynValue.Nil;
    }

    [LuaApi("memory.places", "List of known place names", returns: "list of place names", isBlocking: false, allowedInHandler: true, category: "memory")]
    public static DynValue Places(Script script, INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);

        var places = context.Memory.Places;
        var table = new Table(script);
        for (int i = 0; i < places.Count; i++)
        {
            table[i + 1] = places[i];
        }
        return DynValue.NewTable(table);
    }
}
