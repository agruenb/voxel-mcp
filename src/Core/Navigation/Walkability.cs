namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Authoritative walkability, step-up/drop neighbor rules, and surface height queries for the voxel world.
/// </summary>
public sealed class Walkability : IWalkability
{
    private static readonly WalkabilityOptions DefaultOptions = new();

    private static readonly (int Dx, int Dz, bool IsDiagonal, float BaseCost)[] Directions =
    [
        // 4 cardinal directions (North, South, East, West)
        (0, -1, false, 1.0f),
        (0, 1, false, 1.0f),
        (1, 0, false, 1.0f),
        (-1, 0, false, 1.0f),

        // 4 diagonal directions
        (1, -1, true, 1.41421356f),
        (-1, -1, true, 1.41421356f),
        (1, 1, true, 1.41421356f),
        (-1, 1, true, 1.41421356f),
    ];

    public IVoxelWorld World { get; }
    public WalkabilityOptions Options { get; }

    public Walkability(IVoxelWorld world, WalkabilityOptions? options = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Options = options ?? DefaultOptions;
    }

    /// <inheritdoc />
    public bool IsWalkable(Vec3i pos) => IsWalkable(World, pos, Options);

    /// <inheritdoc />
    public IEnumerable<Vec3i> Neighbours(Vec3i pos)
    {
        foreach (var neighbor in NeighboursWithCost(World, pos, Options))
        {
            yield return neighbor.Destination;
        }
    }

    /// <inheritdoc />
    public IEnumerable<WalkNeighbor> NeighboursWithCost(Vec3i pos) =>
        NeighboursWithCost(World, pos, Options);

    /// <inheritdoc />
    public Vec3i? Surface(int x, int z, int? maxY = null, int? minY = null) =>
        Surface(World, x, z, maxY ?? Options.DefaultMaxY, minY ?? Options.DefaultMinY, Options);

    /// <summary>
    /// Checks whether the voxel block at the given position is solid.
    /// </summary>
    public static bool IsSolid(IVoxelWorld world, Vec3i pos)
    {
        ArgumentNullException.ThrowIfNull(world);

        ushort blockId = world.GetBlock(pos);
        if (blockId == 0)
        {
            return false;
        }

        return world.BlockRegistry.TryGetDefinition(blockId, out var def) && def.IsSolid;
    }

    /// <summary>
    /// Checks whether the voxel block at the given position is water.
    /// </summary>
    public static bool IsWater(IVoxelWorld world, Vec3i pos)
    {
        ArgumentNullException.ThrowIfNull(world);

        ushort blockId = world.GetBlock(pos);
        if (blockId == 0)
        {
            return false;
        }

        return world.BlockRegistry.TryGetDefinition(blockId, out var def) && def.StringId == "water";
    }

    /// <summary>
    /// Determines whether an entity can stand at the given cell coordinates.
    /// A cell is walkable if the block below is solid, and both the feet and head blocks are non-solid.
    /// </summary>
    public static bool IsWalkable(IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        options ??= DefaultOptions;

        // Ground below must be solid
        if (!IsSolid(world, pos.Offset(0, -1, 0)))
        {
            return false;
        }

        // Feet must be non-solid
        if (IsSolid(world, pos))
        {
            return false;
        }

        // Head must be non-solid
        if (IsSolid(world, pos.Offset(0, 1, 0)))
        {
            return false;
        }

        if (options.PreventWater)
        {
            if (IsWater(world, pos) || IsWater(world, pos.Offset(0, 1, 0)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Generates all valid navigable neighbor cell positions from the specified walkable cell.
    /// </summary>
    public static IEnumerable<Vec3i> Neighbours(IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null)
    {
        foreach (var neighbor in NeighboursWithCost(world, pos, options))
        {
            yield return neighbor.Destination;
        }
    }

    /// <summary>
    /// Generates all valid navigable neighbors with movement costs from the specified cell.
    /// Moves include 4 cardinal + 4 diagonals (only if both adjacent cells are clear), step-up 1, drop-down up to 3.
    /// </summary>
    public static IEnumerable<WalkNeighbor> NeighboursWithCost(IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        options ??= DefaultOptions;

        // Source must be a valid walkable cell
        if (!IsWalkable(world, pos, options))
        {
            yield break;
        }

        for (int i = 0; i < Directions.Length; i++)
        {
            var (dx, dz, isDiagonal, baseCost) = Directions[i];

            // 1. Test Step-Up (dy = +1)
            // Block in front at current feet height is solid
            if (IsSolid(world, pos.Offset(dx, 0, dz)))
            {
                if (options.MaxStepUp >= 1)
                {
                    // Headroom at source: agent must have clearance above head to step up
                    if (!IsSolid(world, pos.Offset(0, 2, 0)))
                    {
                        var dest = pos.Offset(dx, 1, dz);

                        // Destination feet and head must be clear (ground below is already known solid)
                        if (!IsSolid(world, dest) && !IsSolid(world, dest.Offset(0, 1, 0)))
                        {
                            if (!options.PreventWater || (!IsWater(world, dest) && !IsWater(world, dest.Offset(0, 1, 0))))
                            {
                                // For diagonal step-up, both adjacent cells must be clear across the full vertical span
                                if (!isDiagonal || AreDiagonalAdjacentCellsClear(world, pos, dx, dz, 1))
                                {
                                    float cost = MathF.Sqrt((dx * dx) + 1f + (dz * dz));
                                    yield return new WalkNeighbor(dest, cost, 1, isDiagonal);
                                }
                            }
                        }
                    }
                }

                // If block in front was solid, it cannot be a flat move or drop in this direction
                continue;
            }

            // 2. Test Flat Move (dy = 0)
            // Block in front at feet is non-solid, and block below it is solid
            if (IsSolid(world, pos.Offset(dx, -1, dz)))
            {
                var dest = pos.Offset(dx, 0, dz);

                // Destination head must be clear
                if (!IsSolid(world, dest.Offset(0, 1, 0)))
                {
                    if (!options.PreventWater || (!IsWater(world, dest) && !IsWater(world, dest.Offset(0, 1, 0))))
                    {
                        if (!isDiagonal || AreDiagonalAdjacentCellsClear(world, pos, dx, dz, 0))
                        {
                            yield return new WalkNeighbor(dest, baseCost, 0, isDiagonal);
                        }
                    }
                }

                continue;
            }

            // 3. Test Drop-Down (dy = -1 .. -MaxDrop)
            // Block at feet and below feet are both non-solid
            // Check departure clearance: agent's head at pos.Y + 1 must not hit an overhang in the neighbor column
            if (IsSolid(world, pos.Offset(dx, 1, dz)))
            {
                continue;
            }

            // For diagonal drops, adjacent cells must be clear at departure height
            if (isDiagonal && !AreDiagonalAdjacentCellsClear(world, pos, dx, dz, 0))
            {
                continue;
            }

            for (int drop = 1; drop <= options.MaxDrop; drop++)
            {
                var dropFeetPos = pos.Offset(dx, -drop, dz);

                // Column must remain clear of solid blocks as the agent drops
                if (IsSolid(world, dropFeetPos))
                {
                    break;
                }

                // Check for solid landing surface beneath this drop level
                var groundPos = dropFeetPos.Offset(0, -1, 0);
                if (IsSolid(world, groundPos))
                {
                    if (!options.PreventWater || (!IsWater(world, dropFeetPos) && !IsWater(world, dropFeetPos.Offset(0, 1, 0))))
                    {
                        float cost = MathF.Sqrt((dx * dx) + (drop * drop) + (dz * dz));
                        yield return new WalkNeighbor(dropFeetPos, cost, -drop, isDiagonal);
                    }

                    break; // Landing ground reached
                }
            }
        }
    }

    /// <summary>
    /// Verifies that both horizontally adjacent cells are non-solid obstacles for diagonal movement.
    /// </summary>
    private static bool AreDiagonalAdjacentCellsClear(IVoxelWorld world, Vec3i pos, int dx, int dz, int dy)
    {
        int minY = pos.Y;
        int maxY = pos.Y + 1 + Math.Max(0, dy);

        for (int y = minY; y <= maxY; y++)
        {
            if (IsSolid(world, new Vec3i(pos.X + dx, y, pos.Z)))
            {
                return false;
            }

            if (IsSolid(world, new Vec3i(pos.X, y, pos.Z + dz)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the highest walkable cell in the specified (x, z) column within [minY, maxY].
    /// </summary>
    public static Vec3i? Surface(
        IVoxelWorld world,
        int x,
        int z,
        int maxY = 255,
        int minY = 0,
        WalkabilityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        options ??= DefaultOptions;

        if (maxY < minY)
        {
            (maxY, minY) = (minY, maxY);
        }

        // Cache solidity check state to minimize duplicate block queries during vertical scan
        bool headSolid = IsSolid(world, new Vec3i(x, maxY + 1, z));
        bool feetSolid = IsSolid(world, new Vec3i(x, maxY, z));

        for (int y = maxY; y >= minY; y--)
        {
            bool groundSolid = IsSolid(world, new Vec3i(x, y - 1, z));

            if (groundSolid && !feetSolid && !headSolid)
            {
                var candidate = new Vec3i(x, y, z);
                if (!options.PreventWater || (!IsWater(world, candidate) && !IsWater(world, candidate.Offset(0, 1, 0))))
                {
                    return candidate;
                }
            }

            headSolid = feetSolid;
            feetSolid = groundSolid;
        }

        return null;
    }
}

/// <summary>
/// Extension methods for querying walkability directly on IVoxelWorld instances.
/// </summary>
public static class VoxelWorldWalkabilityExtensions
{
    public static bool IsWalkable(this IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null) =>
        Walkability.IsWalkable(world, pos, options);

    public static IEnumerable<Vec3i> GetWalkableNeighbours(this IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null) =>
        Walkability.Neighbours(world, pos, options);

    public static IEnumerable<WalkNeighbor> GetWalkableNeighboursWithCost(this IVoxelWorld world, Vec3i pos, WalkabilityOptions? options = null) =>
        Walkability.NeighboursWithCost(world, pos, options);

    public static Vec3i? GetSurface(this IVoxelWorld world, int x, int z, int maxY = 255, int minY = 0, WalkabilityOptions? options = null) =>
        Walkability.Surface(world, x, z, maxY, minY, options);
}
