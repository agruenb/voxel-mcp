namespace VoxelKing.Core.Brain;

using System;
using System.Reflection;

/// <summary>
/// Metadata describing a registered Lua brain API function.
/// </summary>
public sealed record LuaApiEntry(
    string Name,
    string Description,
    string Parameters,
    string Returns,
    bool IsBlocking,
    bool AllowedInHandler,
    string Category,
    MethodInfo? Method = null)
{
    /// <summary>
    /// Creates an entry from a <see cref="LuaApiAttribute"/> and optional <see cref="MethodInfo"/>.
    /// </summary>
    public static LuaApiEntry FromAttribute(LuaApiAttribute attribute, MethodInfo? method = null)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return new LuaApiEntry(
            attribute.Name,
            attribute.Description,
            attribute.Parameters,
            attribute.Returns,
            attribute.IsBlocking,
            attribute.AllowedInHandler,
            attribute.Category,
            method);
    }
}
