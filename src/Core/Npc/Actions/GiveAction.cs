namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Action handing items from the NPC inventory to another entity within reach distance.
/// </summary>
public sealed class GiveAction : ActionBase
{
    public const float DefaultMaxDistance = 3.0f;

    public override string Name => "Give";

    public NpcState Npc { get; }
    public EntityId ReceiverId { get; }
    public string ItemId { get; }
    public int Count { get; }
    public EntityRegistry EntityRegistry { get; }
    public float MaxDistance { get; }

    public GiveAction(
        NpcState npc,
        EntityId receiverId,
        string itemId,
        int count,
        EntityRegistry entityRegistry,
        float maxDistance = DefaultMaxDistance,
        float timeout = 5f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        ReceiverId = receiverId;
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        Count = count;
        EntityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        MaxDistance = maxDistance;
    }

    protected override void OnStart()
    {
        if (ReceiverId == Npc.Id || Count <= 0 || string.IsNullOrWhiteSpace(ItemId))
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        if (!EntityRegistry.TryGet(ReceiverId, out var target) || target == null)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        IInventory? receiverInventory = null;
        if (target is NpcState targetNpc)
        {
            if (targetNpc.IsDead)
            {
                Fail(ActionFailureReason.TargetGone);
                return;
            }
            receiverInventory = targetNpc.Inventory;
        }
        else if (target is PlayerEntity player)
        {
            receiverInventory = player.Inventory;
        }
        else
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        if (receiverInventory == null)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        // Distance check
        if (Npc.Position.Distance(target.Position) > MaxDistance)
        {
            Fail(ActionFailureReason.TooFar);
            return;
        }

        // Item check
        if (Npc.Inventory.CountItem(ItemId) < Count)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        // Receiver inventory space check
        if (receiverInventory.FreeSlots == 0 && receiverInventory.CountItem(ItemId) == 0)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        // Turn towards receiver
        Npc.SetFacingDirection(target.Position - Npc.Position);

        // Transfer items
        bool added = receiverInventory.AddItem(ItemId, Count, out int remainder);
        int transferred = Count - remainder;
        if (transferred > 0)
        {
            Npc.Inventory.RemoveItem(ItemId, transferred);
        }

        if (transferred < Count)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        Succeed();
    }
}
