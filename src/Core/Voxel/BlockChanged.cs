namespace VoxelKing.Core.Voxel;

using VoxelKing.Core.Common;

/// <summary>
/// Event broadcast on the event bus when a voxel at a world position changes.
/// </summary>
public readonly record struct BlockChanged(Vec3i Position, ushort OldBlock, ushort NewBlock)
{
    public ushort OldBlockId => OldBlock;
    public ushort NewBlockId => NewBlock;
}
