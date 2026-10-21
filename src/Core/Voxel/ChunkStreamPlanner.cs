namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;

/// <summary>
/// Configuration parameters for chunk streaming and distance-based loading/unloading.
/// </summary>
public sealed record ChunkStreamPlannerConfig
{
    public int HorizontalRadius { get; init; } = 4;
    public int Hysteresis { get; init; } = 1;
    public int MinChunkY { get; init; } = 0;
    public int MaxChunkY { get; init; } = 7;

    public int UnloadRadius => HorizontalRadius + Hysteresis;

    public ChunkStreamPlannerConfig(int horizontalRadius = 4, int hysteresis = 1, int minChunkY = 0, int maxChunkY = 7)
    {
        if (horizontalRadius < 1) throw new ArgumentOutOfRangeException(nameof(horizontalRadius), "Horizontal radius must be at least 1.");
        if (hysteresis < 0) throw new ArgumentOutOfRangeException(nameof(hysteresis), "Hysteresis cannot be negative.");
        if (minChunkY > maxChunkY) throw new ArgumentException("MinChunkY cannot be greater than MaxChunkY.");

        HorizontalRadius = horizontalRadius;
        Hysteresis = hysteresis;
        MinChunkY = minChunkY;
        MaxChunkY = maxChunkY;
    }
}

/// <summary>
/// Deterministic spatial query planner for chunk streaming.
/// Calculates chunk loading priorities, generation sets, and hysteresis-safe unload sets.
/// Completely decoupled from the Godot engine.
/// </summary>
public sealed class ChunkStreamPlanner
{
    public ChunkStreamPlannerConfig Config { get; }

    public ChunkStreamPlanner(ChunkStreamPlannerConfig? config = null)
    {
        Config = config ?? new ChunkStreamPlannerConfig();
    }

    public ChunkStreamPlanner(WorldConfig worldConfig, int hysteresis = 1)
    {
        ArgumentNullException.ThrowIfNull(worldConfig);
        Config = new ChunkStreamPlannerConfig(
            horizontalRadius: worldConfig.LoadedChunkRadius > 0 ? worldConfig.LoadedChunkRadius : 4,
            hysteresis: hysteresis
        );
    }

    /// <summary>
    /// Returns all chunk coordinates within the specified radius around the center chunk,
    /// ordered by ascending distance squared (closest to center first).
    /// </summary>
    public IReadOnlyList<Vec3i> GetChunksInRadius(Vec3i centerChunk, int radius)
    {
        if (radius < 0) return Array.Empty<Vec3i>();

        int radiusSq = radius * radius;
        var results = new List<Vec3i>();

        for (int dx = -radius; dx <= radius; dx++)
        {
            int cx = centerChunk.X + dx;
            int dxSq = dx * dx;

            for (int dz = -radius; dz <= radius; dz++)
            {
                int cz = centerChunk.Z + dz;
                int distHorizSq = dxSq + (dz * dz);

                if (distHorizSq > radiusSq)
                {
                    continue;
                }

                for (int cy = Config.MinChunkY; cy <= Config.MaxChunkY; cy++)
                {
                    results.Add(new Vec3i(cx, cy, cz));
                }
            }
        }

        // Sort closest first (by 3D Euclidean distance squared)
        results.Sort((a, b) =>
        {
            long distA = centerChunk.DistanceSquared(a);
            long distB = centerChunk.DistanceSquared(b);
            int comp = distA.CompareTo(distB);
            if (comp != 0) return comp;
            return a.CompareTo(b);
        });

        return results;
    }

    /// <summary>
    /// Returns all chunk coordinates within the configured HorizontalRadius around centerChunk,
    /// sorted closest first.
    /// </summary>
    public IReadOnlyList<Vec3i> GetChunksInLoadRadius(Vec3i centerChunk) =>
        GetChunksInRadius(centerChunk, Config.HorizontalRadius);

    /// <summary>
    /// Returns all chunk coordinates within generation radius (HorizontalRadius + 1),
    /// ensuring neighbor data is available before meshing.
    /// </summary>
    public IReadOnlyList<Vec3i> GetChunksInGenerationRadius(Vec3i centerChunk) =>
        GetChunksInRadius(centerChunk, Config.HorizontalRadius + 1);

    /// <summary>
    /// Determines which currently loaded chunks should be unloaded given the center chunk.
    /// Chunks are only unloaded when their horizontal distance exceeds HorizontalRadius + Hysteresis,
    /// or when their vertical coordinate falls outside [MinChunkY, MaxChunkY].
    /// </summary>
    public IReadOnlyList<Vec3i> GetChunksToUnload(Vec3i centerChunk, IEnumerable<Vec3i> activeChunks)
    {
        ArgumentNullException.ThrowIfNull(activeChunks);

        int unloadRadius = Config.UnloadRadius;
        long unloadRadiusSq = (long)unloadRadius * unloadRadius;
        var toUnload = new List<Vec3i>();

        foreach (var coord in activeChunks)
        {
            if (coord.Y < Config.MinChunkY || coord.Y > Config.MaxChunkY)
            {
                toUnload.Add(coord);
                continue;
            }

            long dx = (long)coord.X - centerChunk.X;
            long dz = (long)coord.Z - centerChunk.Z;
            long horizDistSq = (dx * dx) + (dz * dz);

            if (horizDistSq > unloadRadiusSq)
            {
                toUnload.Add(coord);
            }
        }

        return toUnload;
    }

    public bool IsWithinLoadRadius(Vec3i chunkCoord, Vec3i centerChunk)
    {
        if (chunkCoord.Y < Config.MinChunkY || chunkCoord.Y > Config.MaxChunkY)
        {
            return false;
        }

        long dx = (long)chunkCoord.X - centerChunk.X;
        long dz = (long)chunkCoord.Z - centerChunk.Z;
        return (dx * dx) + (dz * dz) <= ((long)Config.HorizontalRadius * Config.HorizontalRadius);
    }

    public bool IsBeyondUnloadRadius(Vec3i chunkCoord, Vec3i centerChunk)
    {
        if (chunkCoord.Y < Config.MinChunkY || chunkCoord.Y > Config.MaxChunkY)
        {
            return true;
        }

        long dx = (long)chunkCoord.X - centerChunk.X;
        long dz = (long)chunkCoord.Z - centerChunk.Z;
        int unloadRadius = Config.UnloadRadius;
        return (dx * dx) + (dz * dz) > ((long)unloadRadius * unloadRadius);
    }
}
