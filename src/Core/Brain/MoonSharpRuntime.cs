namespace VoxelKing.Core.Brain;

using MoonSharp.Interpreter;
using VoxelKing.Core.Common;

/// <summary>
/// Implementation of <see cref="ILuaRuntime"/> using MoonSharp, creating sandboxed <see cref="Script"/> instances.
/// </summary>
public sealed class MoonSharpRuntime : ILuaRuntime
{
    private readonly BrainLimitsConfig? _limitsConfig;

    /// <summary>
    /// Initializes a new instance of <see cref="MoonSharpRuntime"/>.
    /// </summary>
    /// <param name="limitsConfig">Optional brain limits configuration.</param>
    public MoonSharpRuntime(BrainLimitsConfig? limitsConfig = null)
    {
        _limitsConfig = limitsConfig;
    }

    /// <summary>
    /// Brain limits configuration, if provided.
    /// </summary>
    public BrainLimitsConfig? LimitsConfig => _limitsConfig;

    /// <inheritdoc />
    public Script CreateScript(int? seed = null, IRandomProvider? randomProvider = null)
    {
        return LuaSandbox.Create(seed, randomProvider);
    }

    /// <inheritdoc />
    public Script CreateScript() => CreateScript(null, null);

    /// <inheritdoc />
    public Script CreateScript(int seed) => CreateScript(seed, null);

    /// <inheritdoc />
    public Script CreateScript(IRandomProvider randomProvider) => CreateScript(null, randomProvider);
}
