namespace VoxelKing.Core.Brain.Bindings;

using System;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API bindings for general utilities: <c>vec</c>, <c>random</c>, and <c>dist</c> (LUA_BRAIN_API §3.6).
/// </summary>
public static class UtilityApiBindings
{
    [LuaApi("vec", "Create a pos table {x=, y=, z=} with integer coordinates", parameters: "x, y, z", returns: "pos", isBlocking: false, allowedInHandler: true, category: "utility")]
    public static DynValue Vec(Script script, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (args.Count < 3) throw new ScriptRuntimeException("vec(x, y, z) expects 3 numbers");

        var vecFunc = script.Globals.Get("vec");
        if (vecFunc.Type == DataType.Function)
        {
            return script.Call(vecFunc, args[0], args[1], args[2]);
        }

        var table = new Table(script);
        table["x"] = (int)Math.Floor(args[0].Number);
        table["y"] = (int)Math.Floor(args[1].Number);
        table["z"] = (int)Math.Floor(args[2].Number);
        return DynValue.NewTable(table);
    }

    [LuaApi("random", "Pseudo-random number generator (deterministic per NPC)", parameters: "a [, b]", returns: "number", isBlocking: false, allowedInHandler: true, category: "utility")]
    public static DynValue Random(Script script, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        var rndFunc = script.Globals.Get("random");
        if (rndFunc.Type == DataType.Function)
        {
            return script.Call(rndFunc, args);
        }
        return DynValue.NewNumber(0);
    }

    [LuaApi("dist", "Calculate Euclidean distance between two pos tables", parameters: "a, b", returns: "number", isBlocking: false, allowedInHandler: true, category: "utility")]
    public static DynValue Dist(CallbackArguments args)
    {
        if (args.Count < 2) throw new ScriptRuntimeException("dist(a, b) expects two pos tables");
        var posA = LuaValueConverter.ToVec3i(args[0]);
        var posB = LuaValueConverter.ToVec3i(args[1]);

        double dx = posA.X - posB.X;
        double dy = posA.Y - posB.Y;
        double dz = posA.Z - posB.Z;
        return DynValue.NewNumber(Math.Sqrt(dx * dx + dy * dy + dz * dz));
    }
}
