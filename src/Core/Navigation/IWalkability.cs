namespace VoxelKing.Core.Navigation;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Queries voxel walkability, navigable neighbor transitions, and terrain surfaces.
/// </summary>
public interface IWalkability
{
    /// <summary>
    /// Gets the voxel world referenced by this walkability instance.
    /// </summary>
    IVoxelWorld World { get; }

    /// <summary>
    /// Gets the current walkability options.
    /// </summary>
    WalkabilityOptions Options { get; }

    /// <summary>
    /// Determines whether an NPC can stand at the given cell coordinates.
    /// A cell is walkable if the block below is solid, and both the feet and head blocks are non-solid.
    /// </summary>
    bool IsWalkable(Vec3i pos);

    /// <summary>
    /// Generates all valid navigable neighbor cell positions from the specified walkable cell.
    /// Supports 4 cardinal + 4 diagonal moves, step-up of 1 block, and drops up to 3 blocks.
    /// </summary>
    IEnumerable<Vec3i> Neighbours(Vec3i pos);

    /// <summary>
    /// Generates all valid navigable neighbors with their movement costs from the specified cell.
    /// </summary>
    IEnumerable<WalkNeighbor> NeighboursWithCost(Vec3i pos);

    /// <summary>
    /// Finds the highest walkable cell in the specified (x, z) column within [minY, maxY].
    /// Returns null if no walkable cell exists in the column.
    /// </summary>
    Vec3i? Surface(int x, int z, int? maxY = null, int? minY = null);
}
