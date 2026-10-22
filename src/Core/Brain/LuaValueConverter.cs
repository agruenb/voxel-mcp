namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc.Perception;

/// <summary>
/// Helper converting between MoonSharp <see cref="DynValue"/> instances and Core C# types
/// such as <see cref="Vec3i"/>, <see cref="PerceivedEntity"/>, primitives, and collections.
/// </summary>
public static class LuaValueConverter
{
    /// <summary>
    /// Converts a Lua pos table into a Core <see cref="Vec3i"/>.
    /// </summary>
    public static Vec3i ToVec3i(DynValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Type != DataType.Table)
        {
            throw new ScriptRuntimeException($"expected pos table with x, y, z fields, got {value.Type.ToLuaTypeString()}");
        }

        var table = value.Table;
        var xVal = table.Get("x");
        var yVal = table.Get("y");
        var zVal = table.Get("z");

        if (xVal.Type != DataType.Number || yVal.Type != DataType.Number || zVal.Type != DataType.Number)
        {
            throw new ScriptRuntimeException("pos table must contain numeric 'x', 'y', and 'z' fields");
        }

        return new Vec3i(
            (int)Math.Floor(xVal.Number),
            (int)Math.Floor(yVal.Number),
            (int)Math.Floor(zVal.Number));
    }

    /// <summary>
    /// Converts a Core <see cref="Vec3i"/> into a Lua pos table with metatable support.
    /// </summary>
    public static DynValue ToPosTable(Script script, Vec3i pos)
    {
        ArgumentNullException.ThrowIfNull(script);

        // If the vec utility function is defined in script, use it so it automatically inherits pos metatable
        var vecFunc = script.Globals.Get("vec");
        if (vecFunc.Type == DataType.Function)
        {
            return script.Call(vecFunc, DynValue.NewNumber(pos.X), DynValue.NewNumber(pos.Y), DynValue.NewNumber(pos.Z));
        }

        var table = new Table(script);
        table["x"] = pos.X;
        table["y"] = pos.Y;
        table["z"] = pos.Z;
        return DynValue.NewTable(table);
    }

    /// <summary>
    /// Converts a <see cref="PerceivedEntity"/> snapshot to a Lua table matching LUA_BRAIN_API §3.4.
    /// </summary>
    public static DynValue ToEntityTable(Script script, PerceivedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(entity);

        var table = new Table(script);
        table["id"] = entity.Id.Value;
        table["kind"] = entity.KindString;
        table["name"] = entity.Name;
        table["pos"] = ToPosTable(script, entity.Position.FloorToInt());
        table["health"] = entity.Health;

        if (entity.ItemId != null)
        {
            table["item_id"] = entity.ItemId;
        }

        if (entity.Count.HasValue)
        {
            table["count"] = entity.Count.Value;
        }

        return DynValue.NewTable(table);
    }

    /// <summary>
    /// Converts a Lua <see cref="DynValue"/> into a CLR object suitable for <see cref="VoxelKing.Core.Npc.Memory.INpcMemory"/>
    /// and action parameters.
    /// </summary>
    public static object? ToClrObject(DynValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (value.Type)
        {
            case DataType.Nil:
            case DataType.Void:
                return null;

            case DataType.Boolean:
                return value.Boolean;

            case DataType.Number:
                // If it's a whole integer within standard integer range, store as int
                double num = value.Number;
                if (Math.Abs(num % 1) < 1e-6 && num is >= int.MinValue and <= int.MaxValue)
                {
                    return (int)num;
                }
                return num;

            case DataType.String:
                return value.String;

            case DataType.Table:
                var table = value.Table;
                // Check if it represents a pos table
                var xVal = table.Get("x");
                var yVal = table.Get("y");
                var zVal = table.Get("z");
                if (xVal.Type == DataType.Number && yVal.Type == DataType.Number && zVal.Type == DataType.Number)
                {
                    return new Vec3i((int)xVal.Number, (int)yVal.Number, (int)zVal.Number);
                }

                // Check if it is a sequential array
                int len = table.Length;
                if (len > 0)
                {
                    var list = new List<object?>(len);
                    for (int i = 1; i <= len; i++)
                    {
                        list.Add(ToClrObject(table.Get(i)));
                    }
                    return list;
                }

                // Otherwise convert as dictionary
                var dict = new Dictionary<string, object?>();
                foreach (var pair in table.Pairs)
                {
                    string key = pair.Key.Type == DataType.String
                        ? pair.Key.String
                        : pair.Key.ToPrintString();
                    dict[key] = ToClrObject(pair.Value);
                }
                return dict;

            default:
                throw new ScriptRuntimeException($"Unsupported value type '{value.Type.ToLuaTypeString()}' for memory conversion");
        }
    }

    /// <summary>
    /// Converts a CLR object into a corresponding MoonSharp <see cref="DynValue"/>.
    /// </summary>
    public static DynValue ToDynValue(Script script, object? value)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (value == null)
        {
            return DynValue.Nil;
        }

        switch (value)
        {
            case bool b:
                return DynValue.NewBoolean(b);

            case sbyte sb:
                return DynValue.NewNumber(sb);
            case byte b:
                return DynValue.NewNumber(b);
            case short s:
                return DynValue.NewNumber(s);
            case ushort us:
                return DynValue.NewNumber(us);
            case int i:
                return DynValue.NewNumber(i);
            case uint ui:
                return DynValue.NewNumber(ui);
            case long l:
                return DynValue.NewNumber(l);
            case ulong ul:
                return DynValue.NewNumber(ul);
            case float f:
                return DynValue.NewNumber(f);
            case double d:
                return DynValue.NewNumber(d);

            case string str:
                return DynValue.NewString(str);

            case Vec3i pos:
                return ToPosTable(script, pos);

            case IDictionary<string, object> dict:
                var table = new Table(script);
                foreach (var kvp in dict)
                {
                    table[kvp.Key] = ToDynValue(script, kvp.Value);
                }
                return DynValue.NewTable(table);

            case IList<object> list:
                var arrTable = new Table(script);
                for (int i = 0; i < list.Count; i++)
                {
                    arrTable[i + 1] = ToDynValue(script, list[i]);
                }
                return DynValue.NewTable(arrTable);

            case IEnumerable<string> strSeq:
                var strArr = new Table(script);
                int idx = 1;
                foreach (var s in strSeq)
                {
                    strArr[idx++] = DynValue.NewString(s);
                }
                return DynValue.NewTable(strArr);

            case IEnumerable<Vec3i> posSeq:
                var posArr = new Table(script);
                int pIdx = 1;
                foreach (var p in posSeq)
                {
                    posArr[pIdx++] = ToPosTable(script, p);
                }
                return DynValue.NewTable(posArr);

            default:
                return DynValue.FromObject(script, value);
        }
    }
}
