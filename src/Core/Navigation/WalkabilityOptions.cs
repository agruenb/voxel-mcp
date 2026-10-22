namespace VoxelKing.Core.Navigation;

/// <summary>
/// Configurable options controlling voxel walkability and neighbor generation rules.
/// </summary>
public sealed record WalkabilityOptions
{
    /// <summary>
    /// Maximum vertical step-up height in blocks (default 1).
    /// </summary>
    public int MaxStepUp { get; init; } = 1;

    /// <summary>
    /// Maximum vertical drop-down height in blocks (default 3).
    /// </summary>
    public int MaxDrop { get; init; } = 3;

    /// <summary>
    /// If true, feet and head must also not be water (default false).
    /// </summary>
    public bool PreventWater { get; init; } = false;

    /// <summary>
    /// Default maximum Y height for surface column queries (default 255).
    /// </summary>
    public int DefaultMaxY { get; init; } = 255;

    /// <summary>
    /// Default minimum Y height for surface column queries (default 0).
    /// </summary>
    public int DefaultMinY { get; init; } = 0;
}
