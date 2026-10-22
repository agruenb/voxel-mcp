namespace VoxelKing.Core.Brain;

using MoonSharp.Interpreter;
using VoxelKing.Core.Common;

/// <summary>
/// Abstraction for the Lua execution runtime, enabling sandboxed script creation.
/// </summary>
public interface ILuaRuntime
{
    /// <summary>
    /// Creates a newly initialized and sandboxed MoonSharp <see cref="Script"/> instance.
    /// </summary>
    /// <param name="seed">Optional seed for deterministic pseudo-random generation.</param>
    /// <param name="randomProvider">Optional custom random provider.</param>
    /// <returns>A sandboxed MoonSharp <see cref="Script"/> instance.</returns>
    Script CreateScript(int? seed = null, IRandomProvider? randomProvider = null);

    /// <summary>
    /// Creates a newly initialized and sandboxed MoonSharp <see cref="Script"/> instance.
    /// </summary>
    /// <returns>A sandboxed MoonSharp <see cref="Script"/> instance.</returns>
    Script CreateScript();

    /// <summary>
    /// Creates a newly initialized and sandboxed MoonSharp <see cref="Script"/> instance with a specified random seed.
    /// </summary>
    /// <param name="seed">Seed for deterministic pseudo-random generation.</param>
    /// <returns>A sandboxed MoonSharp <see cref="Script"/> instance.</returns>
    Script CreateScript(int seed);

    /// <summary>
    /// Creates a newly initialized and sandboxed MoonSharp <see cref="Script"/> instance with a custom random provider.
    /// </summary>
    /// <param name="randomProvider">Custom random provider.</param>
    /// <returns>A sandboxed MoonSharp <see cref="Script"/> instance.</returns>
    Script CreateScript(IRandomProvider randomProvider);
}
