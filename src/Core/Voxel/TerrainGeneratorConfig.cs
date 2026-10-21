namespace VoxelKing.Core.Voxel;

/// <summary>
/// Configurable generation parameters for procedural terrain.
/// </summary>
public sealed record TerrainGeneratorConfig
{
    public int WaterLevel { get; init; } = 62;
    public int BaseHeight { get; init; } = 64;
    public float HeightScale { get; init; } = 20.0f;
    public float NoiseFrequency { get; init; } = 0.005f;
    public int NoiseOctaves { get; init; } = 4;
    public float CoalFrequency { get; init; } = 0.08f;
    public float CoalThreshold { get; init; } = 0.62f;
    public int TreeCellSize { get; init; } = 8;
    public int TreeTrunkHeight { get; init; } = 5;
}
