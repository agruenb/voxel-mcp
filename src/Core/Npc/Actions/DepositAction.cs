namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Action depositing items from NPC inventory into a container at a block position within reach distance.
/// </summary>
public sealed class DepositAction : ActionBase
{
    public const float DefaultMaxDistance = 4.0f;

    public override string Name => "Deposit";

    public NpcState Npc { get; }
    public Vec3i BlockPos { get; }
    public string ItemId { get; }
    public int Count { get; }
    public ContainerManager ContainerManager { get; }
    public float MaxDistance { get; }

    public DepositAction(
        NpcState npc,
        Vec3i blockPos,
        string itemId,
        int count,
        ContainerManager containerManager,
        float maxDistance = DefaultMaxDistance,
        float timeout = 5f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        BlockPos = blockPos;
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        Count = count;
        ContainerManager = containerManager ?? throw new ArgumentNullException(nameof(containerManager));
        MaxDistance = maxDistance;
    }

    protected override void OnStart()
    {
        var blockCenter = new Vec3(BlockPos.X + 0.5f, BlockPos.Y + 0.5f, BlockPos.Z + 0.5f);

        // Distance check
        if (Npc.Position.Distance(blockCenter) > MaxDistance)
        {
            Fail(ActionFailureReason.TooFar);
            return;
        }

        // Container check
        if (!ContainerManager.TryGetContainer(BlockPos, out var container))
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemId) || (Count <= 0 && Count != -1))
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        int held = Npc.Inventory.CountItem(ItemId);
        if (held <= 0)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        int depositCount = Count == -1 ? held : Count;
        if (depositCount > held)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        // Container capacity check
        if (container.FreeSlots == 0 && container.CountItem(ItemId) == 0)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        // Turn towards container
        Npc.SetFacingDirection(blockCenter - Npc.Position);

        // Transfer items
        bool added = container.AddItem(ItemId, depositCount, out int remainder);
        int transferred = depositCount - remainder;
        if (transferred > 0)
        {
            Npc.Inventory.RemoveItem(ItemId, transferred);
        }

        if (transferred < depositCount)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        Succeed();
    }
}
