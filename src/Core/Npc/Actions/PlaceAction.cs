namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Voxel;

/// <summary>
/// Action placing a block from NPC inventory at a target position adjacent to a solid block.
/// </summary>
public sealed class PlaceAction : ActionBase
{
    public const float DefaultMaxDistance = 4.0f;

    private static readonly Vec3i[] CardinalOffsets = new[]
    {
        new Vec3i(1, 0, 0),
        new Vec3i(-1, 0, 0),
        new Vec3i(0, 1, 0),
        new Vec3i(0, -1, 0),
        new Vec3i(0, 0, 1),
        new Vec3i(0, 0, -1)
    };

    public override string Name => "Place";

    public NpcState Npc { get; }
    public Vec3i BlockPos { get; }
    public string BlockId { get; }
    public IVoxelWorld World { get; }
    public IBlockRegistry BlockRegistry { get; }
    public ContainerManager? ContainerManager { get; }
    public EntityRegistry? EntityRegistry { get; }
    public float MaxDistance { get; }

    public PlaceAction(
        NpcState npc,
        Vec3i blockPos,
        string blockId,
        IVoxelWorld world,
        IBlockRegistry blockRegistry,
        ContainerManager? containerManager = null,
        EntityRegistry? entityRegistry = null,
        float maxDistance = DefaultMaxDistance,
        float timeout = 10f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        BlockPos = blockPos;
        BlockId = blockId ?? throw new ArgumentNullException(nameof(blockId));
        World = world ?? throw new ArgumentNullException(nameof(world));
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        ContainerManager = containerManager;
        EntityRegistry = entityRegistry;
        MaxDistance = maxDistance;
    }

    protected override void OnStart()
    {
        // 1. Item held check
        if (Npc.Inventory.CountItem(BlockId) <= 0)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        // 2. Reach distance check
        var blockCenter = new Vec3(BlockPos.X + 0.5f, BlockPos.Y + 0.5f, BlockPos.Z + 0.5f);
        if (Npc.Position.Distance(blockCenter) > MaxDistance)
        {
            Fail(ActionFailureReason.TooFar);
            return;
        }

        // 3. Target cell must be air
        ushort currentBlock = World.GetBlock(BlockPos);
        if (currentBlock != BlockRegistry.AirId)
        {
            Fail(ActionFailureReason.Occupied);
            return;
        }

        // 4. Target must be adjacent to at least one solid block
        bool hasSolidNeighbor = false;
        foreach (var offset in CardinalOffsets)
        {
            ushort neighborId = World.GetBlock(BlockPos + offset);
            if (neighborId != BlockRegistry.AirId &&
                BlockRegistry.TryGetDefinition(neighborId, out var nDef) &&
                nDef.IsSolid)
            {
                hasSolidNeighbor = true;
                break;
            }
        }

        if (!hasSolidNeighbor)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // 5. Must not intersect NPC body
        if (BlockPlacementHelper.IntersectsEntity(BlockPos, Npc.Position, Npc.Width / 2f, Npc.Height))
        {
            Fail(ActionFailureReason.Occupied);
            return;
        }

        // 6. Must not intersect other entities
        if (EntityRegistry != null)
        {
            var nearby = EntityRegistry.FindAll(blockCenter, 2.5f);
            foreach (var entity in nearby)
            {
                if (entity.Id == Npc.Id)
                {
                    continue;
                }

                if (BlockPlacementHelper.IntersectsEntity(BlockPos, entity.Position))
                {
                    Fail(ActionFailureReason.Occupied);
                    return;
                }
            }
        }

        // 7. Validate block ID
        if (!BlockRegistry.TryGetId(BlockId, out ushort placeBlockNumericId) ||
            !BlockRegistry.TryGetDefinition(placeBlockNumericId, out var blockDef) ||
            blockDef == null)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // Turn towards placement position
        Npc.SetFacingDirection(blockCenter - Npc.Position);

        // Place block in world
        bool placed = World.SetBlock(BlockPos, placeBlockNumericId);
        if (!placed)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // Deduct item from inventory
        Npc.Inventory.RemoveItem(BlockId, 1);

        // If placed block is a container, create container inventory
        if (blockDef.IsContainer && ContainerManager != null)
        {
            ContainerManager.GetOrCreateContainer(BlockPos, 27);
        }

        Succeed();
    }
}
