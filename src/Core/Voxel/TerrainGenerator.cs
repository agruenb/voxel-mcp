namespace VoxelKing.Core.Voxel;

using System;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Deterministic, thread-safe procedural terrain generator producing heightmaps, layers, water, ore veins, and trees.
/// </summary>
public sealed class TerrainGenerator : ITerrainGenerator
{
    private readonly TerrainGeneratorConfig _config;
    private readonly FastNoise _heightNoise;
    private readonly FastNoise _coalNoise;

    private readonly IBlockRegistry _blockRegistry;
    private readonly ushort _airId;
    private readonly ushort _grassId;
    private readonly ushort _dirtId;
    private readonly ushort _stoneId;
    private readonly ushort _sandId;
    private readonly ushort _waterId;
    private readonly ushort _oakLogId;
    private readonly ushort _oakLeavesId;
    private readonly ushort _coalOreId;

    public int Seed { get; }
    public int WaterLevel => _config.WaterLevel;

    public TerrainGenerator(int seed, IBlockRegistry blockRegistry, TerrainGeneratorConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(blockRegistry);
        Seed = seed;
        _blockRegistry = blockRegistry;
        _config = config ?? new TerrainGeneratorConfig();

        _heightNoise = new FastNoise(seed);
        _coalNoise = new FastNoise(seed ^ 0x434F414C); // "COAL"

        _airId = blockRegistry.AirId;
        _grassId = blockRegistry.GetId("grass");
        _dirtId = blockRegistry.GetId("dirt");
        _stoneId = blockRegistry.GetId("stone");
        _sandId = blockRegistry.GetId("sand");
        _waterId = blockRegistry.GetId("water");
        _oakLogId = blockRegistry.GetId("oak_log");
        _oakLeavesId = blockRegistry.GetId("oak_leaves");
        _coalOreId = blockRegistry.GetId("coal_ore");
    }

    public TerrainGenerator(WorldConfig worldConfig, IBlockRegistry blockRegistry, TerrainGeneratorConfig? config = null)
        : this(worldConfig?.Seed ?? 1337, blockRegistry, config)
    {
    }

    public int GetHeight(int worldX, int worldZ)
    {
        float fbm = _heightNoise.GetFbm2D(
            worldX,
            worldZ,
            _config.NoiseOctaves,
            0.5f,
            2.0f,
            _config.NoiseFrequency
        );

        int height = (int)MathF.Round(_config.BaseHeight + (fbm * _config.HeightScale));
        return Math.Clamp(height, 1, 254);
    }

    public ChunkData GenerateChunk(Vec3i chunkCoord)
    {
        var chunk = new ChunkData(chunkCoord, _blockRegistry.IsIndexed);
        PopulateChunk(chunk);
        return chunk;
    }

    public void PopulateChunk(ChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (chunk.BlockIndex.IsIndexedPredicate == null)
        {
            chunk.BlockIndex.SetIndexPredicate(_blockRegistry.IsIndexed, chunk.Blocks);
        }

        int chunkMinX = chunk.ChunkCoord.X * ChunkData.Size;
        int chunkMinY = chunk.ChunkCoord.Y * ChunkData.Size;
        int chunkMinZ = chunk.ChunkCoord.Z * ChunkData.Size;

        int chunkMaxY = chunkMinY + ChunkData.Size - 1;

        // Optimization: Chunks completely above the maximum possible terrain + tree canopy are pure air
        int maxPossibleTerrain = (int)MathF.Ceiling(_config.BaseHeight + _config.HeightScale) + _config.TreeTrunkHeight + 3;
        if (chunkMinY > maxPossibleTerrain)
        {
            return;
        }

        // Optimization: Chunks completely deep underground only contain stone and ore
        int minPossibleTerrain = (int)MathF.Floor(_config.BaseHeight - _config.HeightScale) - 4;
        if (chunkMaxY < minPossibleTerrain)
        {
            PopulateUndergroundChunk(chunk, chunkMinX, chunkMinY, chunkMinZ);
            return;
        }

        // Cache 32x32 column surface heights for this chunk
        Span<int> columnHeights = stackalloc int[ChunkData.Size * ChunkData.Size];
        for (int lz = 0; lz < ChunkData.Size; lz++)
        {
            int wz = chunkMinZ + lz;
            int rowOffset = lz * ChunkData.Size;
            for (int lx = 0; lx < ChunkData.Size; lx++)
            {
                int wx = chunkMinX + lx;
                columnHeights[rowOffset + lx] = GetHeight(wx, wz);
            }
        }

        // Populate base terrain, water, and subsurface layers
        for (int lz = 0; lz < ChunkData.Size; lz++)
        {
            int wz = chunkMinZ + lz;
            int rowOffset = lz * ChunkData.Size;

            for (int lx = 0; lx < ChunkData.Size; lx++)
            {
                int wx = chunkMinX + lx;
                int surfaceY = columnHeights[rowOffset + lx];
                bool isBeach = surfaceY <= _config.WaterLevel + 1;

                for (int ly = 0; ly < ChunkData.Size; ly++)
                {
                    int wy = chunkMinY + ly;

                    if (wy > surfaceY)
                    {
                        if (wy <= _config.WaterLevel)
                        {
                            chunk.SetBlock(lx, ly, lz, _waterId);
                        }
                    }
                    else if (wy == surfaceY)
                    {
                        ushort topBlock = isBeach ? _sandId : _grassId;
                        chunk.SetBlock(lx, ly, lz, topBlock);
                    }
                    else if (wy >= surfaceY - 3)
                    {
                        ushort subBlock = isBeach ? _sandId : _dirtId;
                        chunk.SetBlock(lx, ly, lz, subBlock);
                    }
                    else
                    {
                        if (IsCoalOre(wx, wy, wz))
                        {
                            chunk.SetBlock(lx, ly, lz, _coalOreId);
                        }
                        else
                        {
                            chunk.SetBlock(lx, ly, lz, _stoneId);
                        }
                    }
                }
            }
        }

        // Place trees overlapping this chunk
        PopulateTrees(chunk, chunkMinX, chunkMinY, chunkMinZ);
    }

    private void PopulateUndergroundChunk(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ)
    {
        for (int lz = 0; lz < ChunkData.Size; lz++)
        {
            int wz = chunkMinZ + lz;
            for (int lx = 0; lx < ChunkData.Size; lx++)
            {
                int wx = chunkMinX + lx;
                for (int ly = 0; ly < ChunkData.Size; ly++)
                {
                    int wy = chunkMinY + ly;
                    ushort block = IsCoalOre(wx, wy, wz) ? _coalOreId : _stoneId;
                    chunk.SetBlock(lx, ly, lz, block);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCoalOre(int wx, int wy, int wz)
    {
        float noise = _coalNoise.GetNoise3D(
            wx * _config.CoalFrequency,
            wy * _config.CoalFrequency,
            wz * _config.CoalFrequency
        );
        return noise > _config.CoalThreshold;
    }

    private void PopulateTrees(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ)
    {
        int cellSize = _config.TreeCellSize;
        int trunkHeight = _config.TreeTrunkHeight;

        // Tree canopies have horizontal radius 2. Search cells that could overlap this chunk.
        int minWorldX = chunkMinX - 2;
        int maxWorldX = chunkMinX + ChunkData.Size + 1;
        int minWorldZ = chunkMinZ - 2;
        int maxWorldZ = chunkMinZ + ChunkData.Size + 1;

        int minCellX = minWorldX >> 3;
        int maxCellX = maxWorldX >> 3;
        int minCellZ = minWorldZ >> 3;
        int maxCellZ = maxWorldZ >> 3;

        for (int cz = minCellZ; cz <= maxCellZ; cz++)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                uint hash = HashCell(cx, cz, Seed ^ 0x5EED);

                // Approximately 1 in 3 cells contains a tree
                if ((hash % 3) != 0)
                {
                    continue;
                }

                int offsetX = (int)((hash >> 3) & 7);
                int offsetZ = (int)((hash >> 6) & 7);

                int treeX = (cx << 3) + offsetX;
                int treeZ = (cz << 3) + offsetZ;

                int treeBaseY = GetHeight(treeX, treeZ);

                // Trees only grow on grass (above water level + 1)
                if (treeBaseY <= _config.WaterLevel + 1)
                {
                    continue;
                }

                PlaceTree(chunk, chunkMinX, chunkMinY, chunkMinZ, treeX, treeBaseY, treeZ, trunkHeight);
            }
        }
    }

    /// <summary>
    /// Checks if a procedural tree trunk exists at the specified world X and Z coordinates.
    /// </summary>
    public bool HasTreeTrunk(int worldX, int worldZ)
    {
        int cx = worldX >> 3;
        int cz = worldZ >> 3;

        uint hash = HashCell(cx, cz, Seed ^ 0x5EED);
        if ((hash % 3) != 0)
        {
            return false;
        }

        int offsetX = (int)((hash >> 3) & 7);
        int offsetZ = (int)((hash >> 6) & 7);

        int treeX = (cx << 3) + offsetX;
        int treeZ = (cz << 3) + offsetZ;

        if (worldX != treeX || worldZ != treeZ)
        {
            return false;
        }

        int treeBaseY = GetHeight(treeX, treeZ);
        return treeBaseY > _config.WaterLevel + 1;
    }

    private void PlaceTree(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ, int treeX, int treeBaseY, int treeZ, int trunkHeight)
    {
        int topY = treeBaseY + trunkHeight;

        // 1. Trunk
        for (int y = treeBaseY + 1; y <= topY; y++)
        {
            int lx = treeX - chunkMinX;
            int ly = y - chunkMinY;
            int lz = treeZ - chunkMinZ;

            if ((uint)lx < ChunkData.Size && (uint)ly < ChunkData.Size && (uint)lz < ChunkData.Size)
            {
                chunk.SetBlock(lx, ly, lz, _oakLogId);
            }
        }

        // 2. Canopy leaves
        // Layers topY - 2 and topY - 1: 5x5 with trimmed corners
        for (int y = topY - 2; y <= topY - 1; y++)
        {
            int ly = y - chunkMinY;
            if ((uint)ly >= ChunkData.Size) continue;

            for (int dz = -2; dz <= 2; dz++)
            {
                int lz = (treeZ + dz) - chunkMinZ;
                if ((uint)lz >= ChunkData.Size) continue;

                for (int dx = -2; dx <= 2; dx++)
                {
                    if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2) continue; // Trim corners

                    int lx = (treeX + dx) - chunkMinX;
                    if ((uint)lx >= ChunkData.Size) continue;

                    // Do not overwrite trunk
                    if (dx == 0 && dz == 0 && y <= topY) continue;

                    if (chunk.GetBlock(lx, ly, lz) == _airId)
                    {
                        chunk.SetBlock(lx, ly, lz, _oakLeavesId);
                    }
                }
            }
        }

        // Layer topY: 3x3 square
        int lyTop = topY - chunkMinY;
        if ((uint)lyTop < ChunkData.Size)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int lz = (treeZ + dz) - chunkMinZ;
                if ((uint)lz >= ChunkData.Size) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    int lx = (treeX + dx) - chunkMinX;
                    if ((uint)lx >= ChunkData.Size) continue;

                    if (dx == 0 && dz == 0) continue; // Trunk top

                    if (chunk.GetBlock(lx, lyTop, lz) == _airId)
                    {
                        chunk.SetBlock(lx, lyTop, lz, _oakLeavesId);
                    }
                }
            }
        }

        // Layer topY + 1: cross shape (+ shaped, radius 1)
        int lyAbove = (topY + 1) - chunkMinY;
        if ((uint)lyAbove < ChunkData.Size)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int lz = (treeZ + dz) - chunkMinZ;
                if ((uint)lz >= ChunkData.Size) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) > 1) continue;

                    int lx = (treeX + dx) - chunkMinX;
                    if ((uint)lx >= ChunkData.Size) continue;

                    if (chunk.GetBlock(lx, lyAbove, lz) == _airId)
                    {
                        chunk.SetBlock(lx, lyAbove, lz, _oakLeavesId);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashCell(int x, int z, int seed)
    {
        uint h = (uint)seed ^ 0x9E3779B9u;
        h ^= (uint)x * 0x85EBCA6Bu;
        h = (h << 13) | (h >> 19);
        h ^= (uint)z * 0xC2B2AE35u;
        h = (h << 17) | (h >> 15);
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return h;
    }
}
