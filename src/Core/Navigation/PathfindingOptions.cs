namespace VoxelKing.Core.Navigation;

/// <summary>
/// Configurable options controlling A* search limits, budgets, and partial path fallback.
/// </summary>
public sealed record PathfindingOptions
{
    /// <summary>
    /// Maximum Euclidean search range in blocks from start position (default 64).
    /// </summary>
    public float MaxRange { get; init; } = 64.0f;

    /// <summary>
    /// Maximum number of nodes expanded before stopping the search (default 1000).
    /// </summary>
    public int NodeBudget { get; init; } = 1000;

    /// <summary>
    /// Whether to return a partial path to the closest reached node when the goal is unreachable (default true).
    /// </summary>
    public bool AllowPartial { get; init; } = true;

    /// <summary>
    /// Optional walkability rule overrides for this search.
    /// </summary>
    public WalkabilityOptions? WalkabilityOptions { get; init; }
}
