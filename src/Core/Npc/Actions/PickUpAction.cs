namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Action navigating an NPC to a dropped item entity and picking it up into inventory.
/// </summary>
public sealed class PickUpAction : ActionBase
{
    public const float DefaultPickupDistance = 1.5f;

    public override string Name => "PickUp";

    public NpcState Npc { get; }
    public EntityId TargetItemId { get; }
    public EntityRegistry EntityRegistry { get; }
    public NpcManager? NpcManager { get; }
    public IPathfindingService? PathfindingService { get; }
    public IVoxelWorld? World { get; }
    public IEventBus? EventBus { get; }
    public float PickupDistance { get; }

    private PathFollower? _follower;
    private Vec3 _lastItemPos;

    public PickUpAction(
        NpcState npc,
        EntityId targetItemId,
        EntityRegistry entityRegistry,
        NpcManager? npcManager = null,
        IPathfindingService? pathfindingService = null,
        IVoxelWorld? world = null,
        IEventBus? eventBus = null,
        float pickupDistance = DefaultPickupDistance,
        float timeout = 30f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        TargetItemId = targetItemId;
        EntityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        NpcManager = npcManager;
        PathfindingService = pathfindingService;
        World = world;
        EventBus = eventBus;
        PickupDistance = pickupDistance;
    }

    protected override void OnStart()
    {
        if (!EntityRegistry.TryGet(TargetItemId, out var entity) || entity is not ItemEntity item)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        if (item.IsCollected)
        {
            Fail(ActionFailureReason.TargetGone);
            return;
        }

        if (Npc.Inventory.FreeSlots == 0 && Npc.Inventory.CountItem(item.Stack.ItemId) == 0)
        {
            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        _lastItemPos = item.Position;
    }

    protected override void OnTick(float dt)
    {
        if (!EntityRegistry.TryGet(TargetItemId, out var entity) || entity is not ItemEntity item || item.IsCollected)
        {
            CleanUpFollower();
            Fail(ActionFailureReason.TargetGone);
            return;
        }

        float dist = Npc.Position.Distance(item.Position);
        if (dist <= PickupDistance)
        {
            CleanUpFollower();
            Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);

            if (item.TryPickup(Npc.Inventory, out int countPickedUp) && countPickedUp > 0)
            {
                if (NpcManager != null)
                {
                    NpcManager.DespawnItem(item.Id);
                }
                else
                {
                    item.MarkCollected();
                    EntityRegistry.Unregister(item.Id);
                }

                Succeed();
                return;
            }

            Fail(ActionFailureReason.InventoryFull);
            return;
        }

        // Navigate closer to item
        if (PathfindingService == null || World == null)
        {
            CleanUpFollower();
            Fail(ActionFailureReason.Unreachable);
            return;
        }

        bool needsRepath = _follower == null ||
                           _follower.Status != PathFollowerStatus.Following ||
                           item.Position.Distance(_lastItemPos) > 1.5f;

        if (needsRepath)
        {
            CleanUpFollower();
            _lastItemPos = item.Position;

            var startCell = Npc.Position.FloorToInt();
            var goalCell = item.Position.FloorToInt();

            var result = PathfindingService.FindPath(startCell, goalCell, new PathfindingOptions { AllowPartial = true });
            if (!result.HasPath)
            {
                Fail(ActionFailureReason.Unreachable);
                return;
            }

            _follower = new PathFollower(Npc, World, EventBus);
            Npc.PathFollower = _follower;
            _follower.FollowPath(result.Waypoints, goalCell);
        }
        else if (_follower != null)
        {
            _follower.Update(dt);
            if (_follower.Status == PathFollowerStatus.Stuck)
            {
                CleanUpFollower();
                Fail(ActionFailureReason.Stuck);
                return;
            }

            if (_follower.Status == PathFollowerStatus.Invalidated)
            {
                CleanUpFollower();
                Fail(ActionFailureReason.Unreachable);
                return;
            }
        }
    }

    protected override void OnCancel()
    {
        CleanUpFollower();
    }

    protected override void OnTimeout()
    {
        CleanUpFollower();
    }

    private void CleanUpFollower()
    {
        _follower?.Stop();
        if (ReferenceEquals(Npc.PathFollower, _follower))
        {
            Npc.PathFollower = null;
        }
        _follower = null;
    }
}
