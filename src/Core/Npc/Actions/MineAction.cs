namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Voxel;

/// <summary>
/// Action mining/breaking a block at a target coordinate within reach distance.
/// </summary>
public sealed class MineAction : ActionBase
{
    public const float DefaultMaxDistance = 4.0f;

    public override string Name => "Mine";

    public NpcState Npc { get; }
    public Vec3i BlockPos { get; }
    public IVoxelWorld World { get; }
    public IBlockRegistry BlockRegistry { get; }
    public ContainerManager? ContainerManager { get; }
    public float MaxDistance { get; }
    public float Duration { get; }

    private ushort _targetBlockId;
    private BlockDefinition? _blockDef;

    public MineAction(
        NpcState npc,
        Vec3i blockPos,
        IVoxelWorld world,
        IBlockRegistry blockRegistry,
        ContainerManager? containerManager = null,
        float maxDistance = DefaultMaxDistance,
        float? duration = null,
        float timeout = 10f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        BlockPos = blockPos;
        World = world ?? throw new ArgumentNullException(nameof(world));
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        ContainerManager = containerManager;
        MaxDistance = maxDistance;
        Duration = duration ?? -1f; // Calculated on start if negative
    }

    protected override void OnStart()
    {
        var blockCenter = new Vec3(BlockPos.X + 0.5f, BlockPos.Y + 0.5f, BlockPos.Z + 0.5f);

        // Precondition 1: reach distance
        if (Npc.Position.Distance(blockCenter) > MaxDistance)
        {
            Fail(ActionFailureReason.TooFar);
            return;
        }

        // Precondition 2: block validity
        _targetBlockId = World.GetBlock(BlockPos);
        if (_targetBlockId == BlockRegistry.AirId ||
            !BlockRegistry.TryGetDefinition(_targetBlockId, out _blockDef) ||
            _blockDef == null)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // Precondition 3: inventory room for drop
        if (_blockDef.DropItem != null)
        {
            if (Npc.Inventory.FreeSlots == 0 && Npc.Inventory.CountItem(_blockDef.DropItem) == 0)
            {
                Fail(ActionFailureReason.InventoryFull);
                return;
            }
        }

        // Turn towards block
        Npc.SetFacingDirection(blockCenter - Npc.Position);

        // If duration is 0 or not specified with 0 hardness, complete immediately
        float miningDuration = Duration >= 0f ? Duration : Math.Max(0.05f, _blockDef.Hardness * 0.25f);
        if (miningDuration <= 0f)
        {
            ExecuteBreak();
        }
    }

    protected override void OnTick(float dt)
    {
        var blockCenter = new Vec3(BlockPos.X + 0.5f, BlockPos.Y + 0.5f, BlockPos.Z + 0.5f);

        // Distance check during mining
        if (Npc.Position.Distance(blockCenter) > MaxDistance)
        {
            Fail(ActionFailureReason.TooFar);
            return;
        }

        // Target gone check (block mined by someone else)
        if (World.GetBlock(BlockPos) != _targetBlockId)
        {
            Fail(ActionFailureReason.TargetGone);
            return;
        }

        float miningDuration = Duration >= 0f ? Duration : Math.Max(0.05f, (_blockDef?.Hardness ?? 1f) * 0.25f);
        if (ElapsedTime >= miningDuration)
        {
            ExecuteBreak();
        }
    }

    private void ExecuteBreak()
    {
        if (_blockDef == null)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // Handle container breaking
        if (_blockDef.IsContainer && ContainerManager != null)
        {
            if (ContainerManager.RemoveContainer(BlockPos, out var container))
            {
                // Transfer container items into NPC inventory
                for (int i = 0; i < container.SlotCount; i++)
                {
                    var slot = container.GetSlot(i);
                    if (slot != null && !slot.IsEmpty)
                    {
                        Npc.Inventory.AddItem(slot.ItemId, slot.Count, out _);
                    }
                }
            }
        }

        // Remove block from world
        World.SetBlock(BlockPos, BlockRegistry.AirId);

        // Add drop item
        if (_blockDef.DropItem != null)
        {
            bool added = Npc.Inventory.AddItem(_blockDef.DropItem, 1, out int remainder);
            if (remainder > 0)
            {
                Fail(ActionFailureReason.InventoryFull);
                return;
            }
        }

        Succeed();
    }
}
