namespace VoxelKing.Core.Voxel;

/// <summary>
/// Configurable generation parameters for procedural natural terrain, biomes, and trees.
/// </summary>
public sealed record TerrainGeneratorConfig
{
    public int WaterLevel { get; init; } = 58;
    public int BaseHeight { get; init; } = 64;
    public float PlainsHeightScale { get; init; } = 7.0f;
    public float WoodsHeightScale { get; init; } = 12.0f;
    public float NoiseFrequency { get; init; } = 0.0035f;
    public int NoiseOctaves { get; init; } = 4;
    public float Persistence { get; init; } = 0.38f;

    // Biome frequency
    public float BiomeFrequency { get; init; } = 0.0012f;

    // Lake & basin parameters (limits lakes to natural valleys/depressions)
    public float BasinFrequency { get; init; } = 0.006f;
    public float LakeThreshold { get; init; } = -0.25f;
    public float LakeDepthScale { get; init; } = 9.0f;

    // Ores
    public float CoalFrequency { get; init; } = 0.08f;
    public float CoalThreshold { get; init; } = 0.62f;

    // Trees & foliage
    public int TreeCellSize { get; init; } = 8;
    public float PlainsTreeChance { get; init; } = 0.06f;
    public float WoodsTreeChance { get; init; } = 0.70f;
    public int MinPlainsTreeHeight { get; init; } = 7;
    public int MaxPlainsTreeHeight { get; init; } = 10;
    public int MinWoodsTreeHeight { get; init; } = 11;
    public int MaxWoodsTreeHeight { get; init; } = 16;
}
