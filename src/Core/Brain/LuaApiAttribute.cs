namespace VoxelKing.Core.Brain;

using System;

/// <summary>
/// Annotates a C# binding method that exposes functionality to the Lua NPC brain runtime.
/// Acts as the single source of truth for runtime registration and prompt documentation (ARCHITECTURE §5.4).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaApiAttribute : Attribute
{
    /// <summary>
    /// Function name as exposed in Lua, e.g. "npc.id", "world.block", "vec".
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable description of what the function does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Parameter signature specification, e.g. "pos [, opts]".
    /// </summary>
    public string Parameters { get; }

    /// <summary>
    /// Return type description, e.g. "ok, reason", "entity_id", "pos or nil".
    /// </summary>
    public string Returns { get; }

    /// <summary>
    /// Whether this function performs a long-running/yielding blocking action.
    /// Blocking actions can only be called inside main() coroutine, never in event handlers.
    /// </summary>
    public bool IsBlocking { get; }

    /// <summary>
    /// Whether this function is safe to be called from within event handlers (e.g. on_heard).
    /// Always false for blocking actions.
    /// </summary>
    public bool AllowedInHandler { get; }

    /// <summary>
    /// Functional category (e.g. "npc.self", "npc.actions", "npc.control", "world", "memory", "utility").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="LuaApiAttribute"/>.
    /// </summary>
    public LuaApiAttribute(
        string name,
        string description,
        string parameters = "",
        string returns = "",
        bool isBlocking = false,
        bool allowedInHandler = true,
        string? category = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Parameters = parameters;
        Returns = returns;
        IsBlocking = isBlocking;
        AllowedInHandler = isBlocking ? false : allowedInHandler;

        if (!string.IsNullOrWhiteSpace(category))
        {
            Category = category;
        }
        else
        {
            int dotIdx = name.IndexOf('.');
            Category = dotIdx > 0 ? name[..dotIdx] : "utility";
        }
    }
}
