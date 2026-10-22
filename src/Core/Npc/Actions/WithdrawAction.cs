namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Action taking items from a container at a block position within reach distance into the NPC inventory.
/// </summary>
public sealed class WithdrawAction : ActionBase
{
    public const float DefaultMaxDistance = 4.0f;

    public override string Name => "Withdraw";

    public NpcState Npc { get; }
    public Vec3i BlockPos { get; }
    public string ItemId { get; }
    public int Count { get; }
    public ContainerManager ContainerManager { get; }
    public float MaxDistance { get; }

    public WithdrawAction(
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

        int available = container.CountItem(ItemId);
        if (available <= 0)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        int withdrawCount = Count == -1 ? available : Count;
        if (withdrawCount > available)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        // NPC inventory space check
        if (Npc.Inventory.FreeSlots == 0 && Npc.Inventory.CountItem(ItemId) == 0)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        // Turn towards container
        Npc.SetFacingDirection(blockCenter - Npc.Position);

        // Transfer items
        bool added = Npc.Inventory.AddItem(ItemId, withdrawCount, out int remainder);
        int transferred = withdrawCount - remainder;
        if (transferred > 0)
        {
            container.RemoveItem(ItemId, transferred);
        }

        if (transferred < withdrawCount)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        Succeed();
    }
}
