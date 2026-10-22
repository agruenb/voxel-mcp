namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Action navigating an NPC until it is within a specified distance of a target block position.
/// </summary>
public sealed class GoNearAction : GoToAction
{
    public override string Name => "GoNear";
    public float Distance { get; }

    public GoNearAction(
        NpcState npc,
        Vec3i targetPos,
        float dist,
        float timeout = 30f,
        IPathfindingService? pathfindingService = null,
        IVoxelWorld? world = null,
        IEventBus? eventBus = null,
        IWalkability? walkability = null)
        : base(
            npc,
            targetPos,
            new GoToOptions
            {
                Tolerance = Math.Max(0.1f, dist),
                Timeout = timeout,
                AllowPartial = false
            },
            pathfindingService,
            world,
            eventBus,
            walkability)
    {
        Distance = dist;
    }

    protected override void OnStart()
    {
        if (Distance < 0f)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        base.OnStart();
    }
}
