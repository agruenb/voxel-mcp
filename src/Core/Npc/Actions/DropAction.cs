namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Action dropping items from the NPC inventory onto the ground at its feet.
/// </summary>
public sealed class DropAction : ActionBase
{
    public override string Name => "Drop";

    public NpcState Npc { get; }
    public string ItemId { get; }
    public int Count { get; }
    public EntityRegistry EntityRegistry { get; }
    public NpcManager? NpcManager { get; }

    public DropAction(
        NpcState npc,
        string itemId,
        int count,
        EntityRegistry entityRegistry,
        NpcManager? npcManager = null)
        : base(5f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        Count = count;
        EntityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        NpcManager = npcManager;
    }

    protected override void OnStart()
    {
        if (Count <= 0 || string.IsNullOrWhiteSpace(ItemId))
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        int held = Npc.Inventory.CountItem(ItemId);
        if (held < Count)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        bool removed = Npc.Inventory.RemoveItem(ItemId, Count);
        if (!removed)
        {
            Fail(ActionFailureReason.NoItem);
            return;
        }

        var fwd = Npc.GetFacingDirection();
        var dropPos = Npc.Position + new Vec3(0f, 0.5f, 0f) + (fwd * 0.4f);
        var dropVel = (fwd * 1.5f) + new Vec3(0f, 1.0f, 0f);

        if (NpcManager != null)
        {
            NpcManager.SpawnItem(new ItemStack(ItemId, Count), dropPos, dropVel, pickupCooldown: 1.0f);
        }
        else
        {
            var item = new ItemEntity(EntityRegistry.GenerateNextId(), new ItemStack(ItemId, Count), dropPos, dropVel, pickupCooldown: 1.0f);
            EntityRegistry.Register(item);
        }

        Succeed();
    }
}
