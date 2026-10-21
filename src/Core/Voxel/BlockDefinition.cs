namespace VoxelKing.Core.Voxel;

/// <summary>
/// Immutable definition of a block type loaded from configuration or registry.
/// </summary>
public sealed record BlockDefinition(
    ushort Id,
    string StringId,
    bool IsSolid,
    bool IsTransparent,
    float Hardness,
    string? DropItem,
    bool IsContainer,
    bool IsIndexed,
    BlockAtlasIndices AtlasIndices)
{
    public bool Solid => IsSolid;
    public bool Transparent => IsTransparent;
    public string? Drop => DropItem;
    public bool Container => IsContainer;
    public bool Indexed => IsIndexed;
}
