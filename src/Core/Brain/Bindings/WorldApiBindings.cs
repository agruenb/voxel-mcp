namespace VoxelKing.Core.Brain.Bindings;

using System;
using MoonSharp.Interpreter;
using VoxelKing.Core.Entities;

/// <summary>
/// Lua API bindings for the <c>world.*</c> perception namespace (LUA_BRAIN_API §3.4).
/// </summary>
public static class WorldApiBindings
{
    [LuaApi("world.block", "Block at pos; nil if outside perception radius (24)", parameters: "pos", returns: "block_id or nil", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue Block(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (args.Count == 0) throw new ScriptRuntimeException("world.block expects a pos table");
        var pos = LuaValueConverter.ToVec3i(args[0]);
        var blockId = context.Perception?.GetBlock(pos);
        return blockId != null ? DynValue.NewString(blockId) : DynValue.Nil;
    }

    [LuaApi("world.find_block", "Nearest block of that type (radius <= 24)", parameters: "block_id [, radius]", returns: "pos or nil", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue FindBlock(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        string blockId = args.AsType(0, "world.find_block", DataType.String).String;
        int? radius = args.Count > 1 && args[1].Type == DataType.Number ? (int)args[1].Number : null;

        var pos = context.Perception?.FindBlock(blockId, radius);
        return pos.HasValue ? LuaValueConverter.ToPosTable(script, pos.Value) : DynValue.Nil;
    }

    [LuaApi("world.find_blocks", "List of pos, nearest first (max <= 64)", parameters: "block_id [, radius [, max]]", returns: "list of pos", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue FindBlocks(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        string blockId = args.AsType(0, "world.find_blocks", DataType.String).String;
        int? radius = args.Count > 1 && args[1].Type == DataType.Number ? (int)args[1].Number : null;
        int max = args.Count > 2 && args[2].Type == DataType.Number ? Math.Min(64, (int)args[2].Number) : 64;

        var blocks = context.Perception?.FindBlocks(blockId, radius, max);
        var table = new Table(script);
        if (blocks != null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                table[i + 1] = LuaValueConverter.ToPosTable(script, blocks[i]);
            }
        }
        return DynValue.NewTable(table);
    }

    [LuaApi("world.find_entity", "Nearest entity of kind 'player', 'npc', 'item', 'animal' (radius <= 32)", parameters: "kind [, radius]", returns: "entity_id or nil", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue FindEntity(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        string kind = args.AsType(0, "world.find_entity", DataType.String).String;
        float? radius = args.Count > 1 && args[1].Type == DataType.Number ? (float)args[1].Number : null;

        var entityId = context.Perception?.FindEntity(kind, radius);
        return entityId.HasValue ? DynValue.NewNumber(entityId.Value.Value) : DynValue.Nil;
    }

    [LuaApi("world.find_entities", "List of entity_id, nearest first", parameters: "kind [, radius]", returns: "list of entity_id", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue FindEntities(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        string kind = args.AsType(0, "world.find_entities", DataType.String).String;
        float? radius = args.Count > 1 && args[1].Type == DataType.Number ? (float)args[1].Number : null;

        var entities = context.Perception?.FindEntities(kind, radius);
        var table = new Table(script);
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                table[i + 1] = entities[i].Value;
            }
        }
        return DynValue.NewTable(table);
    }

    [LuaApi("world.entity", "{id, kind, name, pos, health, item_id?, count?}; nil if not perceivable", parameters: "id", returns: "table or nil", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue Entity(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        int id = (int)args.AsType(0, "world.entity", DataType.Number).Number;

        var entity = context.Perception?.GetEntity(new EntityId(id));
        return entity != null ? LuaValueConverter.ToEntityTable(script, entity) : DynValue.Nil;
    }

    [LuaApi("world.is_walkable", "NPC could stand at pos", parameters: "pos", returns: "boolean", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue IsWalkable(INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (args.Count == 0) throw new ScriptRuntimeException("world.is_walkable expects a pos table");
        var pos = LuaValueConverter.ToVec3i(args[0]);

        bool walkable = context.Perception?.IsWalkable(pos) ?? false;
        return DynValue.NewBoolean(walkable);
    }

    [LuaApi("world.surface", "Highest walkable cell in column (within radius)", parameters: "x, z", returns: "pos or nil", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue Surface(Script script, INpcBrainContext context, CallbackArguments args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        int x = (int)args.AsType(0, "world.surface", DataType.Number).Number;
        int z = (int)args.AsType(1, "world.surface", DataType.Number).Number;

        var surface = context.Perception?.GetSurface(x, z);
        return surface.HasValue ? LuaValueConverter.ToPosTable(script, surface.Value) : DynValue.Nil;
    }

    [LuaApi("world.time", "Time of day in hours (0..24)", returns: "number 0..24", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue Time(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        float time = context.Perception?.GetTime() ?? 12.0f;
        return DynValue.NewNumber(time);
    }

    [LuaApi("world.is_day", "Whether it is currently daytime in the world", returns: "boolean", isBlocking: false, allowedInHandler: true, category: "world")]
    public static DynValue IsDay(INpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool isDay = context.Perception?.IsDay() ?? true;
        return DynValue.NewBoolean(isDay);
    }
}
