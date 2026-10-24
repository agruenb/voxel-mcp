namespace VoxelKing.Core.Voxel;

using System;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Deterministic, thread-safe procedural terrain generator producing biomes, gentle heightmaps,
/// natural lake basins, ore veins, and organic procedural trees.
/// </summary>
public sealed class TerrainGenerator : ITerrainGenerator
{
    private readonly TerrainGeneratorConfig _config;
    private readonly FastNoise _heightNoise;
    private readonly FastNoise _biomeNoise;
    private readonly FastNoise _basinNoise;
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
        _biomeNoise = new FastNoise(seed ^ 0xB10EE); // "BIOME"
        _basinNoise = new FastNoise(seed ^ 0xBA511); // "BASIN"
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetBiomeValue(int worldX, int worldZ)
    {
        return _biomeNoise.GetNoise2D(worldX * _config.BiomeFrequency, worldZ * _config.BiomeFrequency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BiomeType GetBiome(int worldX, int worldZ)
    {
        return GetBiomeValue(worldX, worldZ) >= 0.0f ? BiomeType.Woods : BiomeType.Plains;
    }

    public int GetHeight(int worldX, int worldZ)
    {
        float biomeVal = GetBiomeValue(worldX, worldZ);
        // Smooth transition weight t: 0.0 for pure plains, 1.0 for pure woods
        float t = Math.Clamp((biomeVal + 0.15f) * 2.5f, 0.0f, 1.0f);
        float heightScale = _config.PlainsHeightScale + (t * (_config.WoodsHeightScale - _config.PlainsHeightScale));

        float fbm = _heightNoise.GetFbm2D(
            worldX,
            worldZ,
            _config.NoiseOctaves,
            _config.Persistence,
            2.0f,
            _config.NoiseFrequency
        );

        float height = _config.BaseHeight + (fbm * heightScale);

        // Basin lake depression (carves localized lakes in deep basins rather than global flooding)
        float basin = _basinNoise.GetNoise2D(worldX * _config.BasinFrequency, worldZ * _config.BasinFrequency);
        if (basin < _config.LakeThreshold)
        {
            float depthFraction = (_config.LakeThreshold - basin) / (1.0f + _config.LakeThreshold);
            float smoothDepression = depthFraction * depthFraction * (3.0f - (2.0f * depthFraction));
            height -= smoothDepression * _config.LakeDepthScale;
        }

        int finalHeight = (int)MathF.Round(height);
        return Math.Clamp(finalHeight, 1, 254);
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
        int maxPossibleTerrain = (int)MathF.Ceiling(_config.BaseHeight + _config.WoodsHeightScale) + _config.MaxWoodsTreeHeight + 6;
        if (chunkMinY > maxPossibleTerrain)
        {
            return;
        }

        // Optimization: Chunks completely deep underground only contain stone and ore
        int minPossibleTerrain = (int)MathF.Floor(_config.BaseHeight - _config.WoodsHeightScale - _config.LakeDepthScale) - 4;
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

        // Place organic trees overlapping this chunk
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

    private readonly struct TreeSpec
    {
        public readonly bool Exists;
        public readonly int X;
        public readonly int BaseY;
        public readonly int Z;
        public readonly int TrunkHeight;
        public readonly float CanopyRadiusX;
        public readonly float CanopyRadiusY;
        public readonly float CanopyRadiusZ;
        public readonly byte BranchCount;
        public readonly (sbyte Dx, sbyte Dy, sbyte Dz) Branch0;
        public readonly (sbyte Dx, sbyte Dy, sbyte Dz) Branch1;

        public TreeSpec(
            bool exists,
            int x, int baseY, int z,
            int trunkHeight,
            float canopyRadiusX, float canopyRadiusY, float canopyRadiusZ,
            byte branchCount,
            (sbyte, sbyte, sbyte) branch0 = default,
            (sbyte, sbyte, sbyte) branch1 = default)
        {
            Exists = exists;
            X = x;
            BaseY = baseY;
            Z = z;
            TrunkHeight = trunkHeight;
            CanopyRadiusX = canopyRadiusX;
            CanopyRadiusY = canopyRadiusY;
            CanopyRadiusZ = canopyRadiusZ;
            BranchCount = branchCount;
            Branch0 = branch0;
            Branch1 = branch1;
        }
    }

    private bool TryGetTreeSpec(int cx, int cz, out TreeSpec spec)
    {
        uint hash = HashCell(cx, cz, Seed ^ 0x5EED);
        int offsetX = (int)((hash >> 3) & 7);
        int offsetZ = (int)((hash >> 6) & 7);

        int treeX = (cx << 3) + offsetX;
        int treeZ = (cz << 3) + offsetZ;

        float biomeVal = GetBiomeValue(treeX, treeZ);
        bool isWoods = biomeVal >= 0.0f;

        int chancePermille = (int)((hash >> 9) % 1000);
        int threshold = isWoods
            ? (int)(_config.WoodsTreeChance * 1000)
            : (int)(_config.PlainsTreeChance * 1000);

        if (chancePermille >= threshold)
        {
            spec = default;
            return false;
        }

        int treeBaseY = GetHeight(treeX, treeZ);
        if (treeBaseY <= _config.WaterLevel + 1)
        {
            spec = default;
            return false;
        }

        int trunkHeight;
        float rx, ry, rz;
        byte branchCount = 0;
        (sbyte, sbyte, sbyte) b0 = default;
        (sbyte, sbyte, sbyte) b1 = default;

        if (isWoods)
        {
            int range = Math.Max(1, _config.MaxWoodsTreeHeight - _config.MinWoodsTreeHeight + 1);
            trunkHeight = _config.MinWoodsTreeHeight + (int)((hash >> 19) % (uint)range);
            rx = 3.2f + (((hash >> 23) & 3) * 0.25f);
            rz = 3.2f + (((hash >> 25) & 3) * 0.25f);
            ry = 4.0f + (((hash >> 27) & 3) * 0.4f);

            if (trunkHeight >= 12)
            {
                branchCount = 2;
                sbyte dirX1 = (sbyte)(((hash & 1) == 0) ? 1 : -1);
                sbyte dirZ1 = (sbyte)(((hash & 2) == 0) ? 1 : -1);
                sbyte dirX2 = (sbyte)-dirX1;
                sbyte dirZ2 = (sbyte)(((hash & 4) == 0) ? 1 : -1);
                b0 = (dirX1, (sbyte)(trunkHeight - 3), dirZ1);
                b1 = (dirX2, (sbyte)(trunkHeight - 2), dirZ2);
            }
        }
        else
        {
            int range = Math.Max(1, _config.MaxPlainsTreeHeight - _config.MinPlainsTreeHeight + 1);
            trunkHeight = _config.MinPlainsTreeHeight + (int)((hash >> 19) % (uint)range);
            rx = 2.8f + (((hash >> 23) & 3) * 0.3f);
            rz = 2.8f + (((hash >> 25) & 3) * 0.3f);
            ry = 2.8f + (((hash >> 27) & 3) * 0.35f);
        }

        spec = new TreeSpec(
            true,
            treeX, treeBaseY, treeZ,
            trunkHeight,
            rx, ry, rz,
            branchCount,
            b0, b1
        );
        return true;
    }

    private void PopulateTrees(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ)
    {
        // Search cells that could have trunks or canopies overlapping this chunk (canopy max radius ~4)
        int minWorldX = chunkMinX - 4;
        int maxWorldX = chunkMinX + ChunkData.Size + 3;
        int minWorldZ = chunkMinZ - 4;
        int maxWorldZ = chunkMinZ + ChunkData.Size + 3;

        int minCellX = minWorldX >> 3;
        int maxCellX = maxWorldX >> 3;
        int minCellZ = minWorldZ >> 3;
        int maxCellZ = maxWorldZ >> 3;

        for (int cz = minCellZ; cz <= maxCellZ; cz++)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                if (TryGetTreeSpec(cx, cz, out var spec))
                {
                    PlaceTree(chunk, chunkMinX, chunkMinY, chunkMinZ, in spec);
                }
            }
        }
    }

    /// <summary>
    /// Checks if a procedural tree trunk or branch exists at the specified world X and Z coordinates.
    /// </summary>
    public bool HasTreeTrunk(int worldX, int worldZ)
    {
        int baseCx = worldX >> 3;
        int baseCz = worldZ >> 3;

        for (int cz = baseCz - 1; cz <= baseCz + 1; cz++)
        {
            for (int cx = baseCx - 1; cx <= baseCx + 1; cx++)
            {
                if (!TryGetTreeSpec(cx, cz, out var spec))
                {
                    continue;
                }

                if (spec.X == worldX && spec.Z == worldZ)
                {
                    return true;
                }

                if (spec.BranchCount > 0 && spec.X + spec.Branch0.Dx == worldX && spec.Z + spec.Branch0.Dz == worldZ)
                {
                    return true;
                }

                if (spec.BranchCount > 1 && spec.X + spec.Branch1.Dx == worldX && spec.Z + spec.Branch1.Dz == worldZ)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void PlaceTree(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ, in TreeSpec spec)
    {
        int topY = spec.BaseY + spec.TrunkHeight;

        // 1. Trunk
        for (int y = spec.BaseY + 1; y <= topY; y++)
        {
            int lx = spec.X - chunkMinX;
            int ly = y - chunkMinY;
            int lz = spec.Z - chunkMinZ;

            if ((uint)lx < ChunkData.Size && (uint)ly < ChunkData.Size && (uint)lz < ChunkData.Size)
            {
                chunk.SetBlock(lx, ly, lz, _oakLogId);
            }
        }

        // 2. Branches
        if (spec.BranchCount > 0)
        {
            PlaceBranch(chunk, chunkMinX, chunkMinY, chunkMinZ, spec.X, spec.BaseY, spec.Z, spec.Branch0);
            if (spec.BranchCount > 1)
            {
                PlaceBranch(chunk, chunkMinX, chunkMinY, chunkMinZ, spec.X, spec.BaseY, spec.Z, spec.Branch1);
            }
        }

        // 3. Organic Ellipsoidal Canopy
        int centerY = topY - 1;
        int minDy = -(int)MathF.Floor(spec.CanopyRadiusY);
        int maxDy = (int)MathF.Ceiling(spec.CanopyRadiusY);
        int maxDx = (int)MathF.Ceiling(spec.CanopyRadiusX);
        int maxDz = (int)MathF.Ceiling(spec.CanopyRadiusZ);

        float invRxSq = 1.0f / (spec.CanopyRadiusX * spec.CanopyRadiusX);
        float invRySq = 1.0f / (spec.CanopyRadiusY * spec.CanopyRadiusY);
        float invRzSq = 1.0f / (spec.CanopyRadiusZ * spec.CanopyRadiusZ);

        for (int dy = minDy; dy <= maxDy; dy++)
        {
            int worldY = centerY + dy;
            int ly = worldY - chunkMinY;
            if ((uint)ly >= ChunkData.Size) continue;

            float nySq = (dy * dy) * invRySq;
            if (nySq > 1.25f) continue;

            for (int dz = -maxDz; dz <= maxDz; dz++)
            {
                int worldZ = spec.Z + dz;
                int lz = worldZ - chunkMinZ;
                if ((uint)lz >= ChunkData.Size) continue;

                float nzSq = (dz * dz) * invRzSq;
                if (nySq + nzSq > 1.35f) continue;

                for (int dx = -maxDx; dx <= maxDx; dx++)
                {
                    float nxSq = (dx * dx) * invRxSq;
                    float distSq = nxSq + nySq + nzSq;
                    if (distSq > 1.05f) continue;

                    int worldX = spec.X + dx;
                    int lx = worldX - chunkMinX;
                    if ((uint)lx >= ChunkData.Size) continue;

                    // Do not overwrite trunk or branch logs
                    ushort current = chunk.GetBlock(lx, ly, lz);
                    if (current != _airId) continue;

                    chunk.SetBlock(lx, ly, lz, _oakLeavesId);
                }
            }
        }
    }

    private void PlaceBranch(ChunkData chunk, int chunkMinX, int chunkMinY, int chunkMinZ, int baseX, int baseY, int baseZ, (sbyte Dx, sbyte Dy, sbyte Dz) branch)
    {
        int wx = baseX + branch.Dx;
        int wy = baseY + branch.Dy;
        int wz = baseZ + branch.Dz;

        int lx = wx - chunkMinX;
        int ly = wy - chunkMinY;
        int lz = wz - chunkMinZ;

        if ((uint)lx < ChunkData.Size && (uint)ly < ChunkData.Size && (uint)lz < ChunkData.Size)
        {
            chunk.SetBlock(lx, ly, lz, _oakLogId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashLeaf(int x, int y, int z)
    {
        uint h = ((uint)x * 0x85EBCA6Bu) ^ ((uint)y * 0xC2B2AE35u) ^ ((uint)z * 0x27D4EB2Fu);
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        return h;
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
