namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Abstraction for random number generation to allow deterministic testing.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a non-negative random integer that is within a specified range [minValue, maxValue).
    /// </summary>
    int Next(int minValue, int maxValue);

    /// <summary>
    /// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
    /// </summary>
    float NextSingle();
}

/// <summary>
/// Default implementation of <see cref="IRandomProvider"/> wrapping <see cref="Random"/>.
/// </summary>
public sealed class DefaultRandomProvider : IRandomProvider
{
    private readonly Random _random;

    public DefaultRandomProvider(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

    public float NextSingle() => (float)_random.NextDouble();
}
