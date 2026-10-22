namespace VoxelKing.Core.Navigation;

using VoxelKing.Core.Common;

/// <summary>
/// A navigable neighbor destination and transition cost from a source cell.
/// </summary>
public readonly record struct WalkNeighbor(Vec3i Destination, float Cost, int DeltaY, bool IsDiagonal)
{
    public static implicit operator Vec3i(WalkNeighbor neighbor) => neighbor.Destination;
}
