namespace VoxelKing.Core.Voxel;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Finds safe surface spawn coordinates above water level and clear of solid obstacles.
/// </summary>
public static class SurfaceSpawnFinder
{
    /// <summary>
    /// Searches outwards from (startX, startZ) to find a surface position where terrain is above water level
    /// and not occupied by a tree trunk.
    /// </summary>
    public static Vec3 FindSurfaceSpawn(ITerrainGenerator terrainGenerator, int startX = 0, int startZ = 0, int maxSearchRadius = 64)
    {
        ArgumentNullException.ThrowIfNull(terrainGenerator);

        // Check center coordinate first
        int centerHeight = terrainGenerator.GetHeight(startX, startZ);
        if (centerHeight > terrainGenerator.WaterLevel && !terrainGenerator.HasTreeTrunk(startX, startZ))
        {
            return new Vec3(startX + 0.5f, centerHeight + 1.0f, startZ + 0.5f);
        }

        // Expand outwards in square concentric rings
        for (int r = 1; r <= maxSearchRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    // Only evaluate perimeter points of the ring
                    if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                    {
                        continue;
                    }

                    int x = startX + dx;
                    int z = startZ + dz;
                    int height = terrainGenerator.GetHeight(x, z);

                    if (height > terrainGenerator.WaterLevel && !terrainGenerator.HasTreeTrunk(x, z))
                    {
                        return new Vec3(x + 0.5f, height + 1.0f, z + 0.5f);
                    }
                }
            }
        }

        // Fallback: start coordinate safely above water level
        int fallbackY = Math.Max(centerHeight, terrainGenerator.WaterLevel) + 2;
        return new Vec3(startX + 0.5f, fallbackY, startZ + 0.5f);
    }

    /// <summary>
    /// Searches outwards from (startX, startZ) verifying against loaded chunks in VoxelWorld if available.
    /// </summary>
    public static Vec3 FindSurfaceSpawn(
        ITerrainGenerator terrainGenerator,
        IVoxelWorld voxelWorld,
        int startX = 0,
        int startZ = 0,
        int maxSearchRadius = 64)
    {
        ArgumentNullException.ThrowIfNull(terrainGenerator);
        ArgumentNullException.ThrowIfNull(voxelWorld);

        var candidate = FindSurfaceSpawn(terrainGenerator, startX, startZ, maxSearchRadius);

        int blockX = (int)MathF.Floor(candidate.X);
        int blockY = (int)MathF.Floor(candidate.Y);
        int blockZ = (int)MathF.Floor(candidate.Z);

        // Check if head block is blocked by loaded leaves/canopy, offset if needed
        var blockRegistry = voxelWorld.BlockRegistry;
        if (blockRegistry.GetDefinition(voxelWorld.GetBlock(blockX, blockY, blockZ)).IsSolid ||
            blockRegistry.GetDefinition(voxelWorld.GetBlock(blockX, blockY + 1, blockZ)).IsSolid)
        {
            // If obstructed, search locally around candidate
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = blockX + dx;
                    int z = blockZ + dz;
                    int h = terrainGenerator.GetHeight(x, z);
                    if (h > terrainGenerator.WaterLevel && !terrainGenerator.HasTreeTrunk(x, z))
                    {
                        int testY = h + 1;
                        if (!blockRegistry.GetDefinition(voxelWorld.GetBlock(x, testY, z)).IsSolid &&
                            !blockRegistry.GetDefinition(voxelWorld.GetBlock(x, testY + 1, z)).IsSolid)
                        {
                            return new Vec3(x + 0.5f, testY, z + 0.5f);
                        }
                    }
                }
            }
        }

        return candidate;
    }
}
