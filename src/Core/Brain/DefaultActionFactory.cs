namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;

/// <summary>
/// Default implementation of <see cref="IActionFactory"/> that constructs standard game actions
/// from Lua blocking action requests.
/// </summary>
public sealed class DefaultActionFactory : IActionFactory
{
    private readonly IVoxelWorld? _world;
    private readonly IBlockRegistry? _blockRegistry;
    private readonly IPathfindingService? _pathfindingService;
    private readonly EntityRegistry? _entityRegistry;
    private readonly ContainerManager? _containerManager;
    private readonly IEventBus? _eventBus;
    private readonly NpcManager? _npcManager;
    private readonly IWalkability? _walkability;

    public DefaultActionFactory(
        IVoxelWorld? world = null,
        IBlockRegistry? blockRegistry = null,
        IPathfindingService? pathfindingService = null,
        EntityRegistry? entityRegistry = null,
        ContainerManager? containerManager = null,
        IEventBus? eventBus = null,
        NpcManager? npcManager = null,
        IWalkability? walkability = null)
    {
        _world = world;
        _blockRegistry = blockRegistry;
        _pathfindingService = pathfindingService;
        _entityRegistry = entityRegistry;
        _containerManager = containerManager;
        _eventBus = eventBus;
        _npcManager = npcManager;
        _walkability = walkability ?? (world != null ? new Walkability(world) : null);
    }

    /// <inheritdoc />
    public IAction? CreateAction(NpcState npc, ActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(request);

        return request.ActionName switch
        {
            "go_to" => CreateGoToAction(npc, request),
            "go_near" => CreateGoNearAction(npc, request),
            "follow" => CreateFollowAction(npc, request),
            "mine" => CreateMineAction(npc, request),
            "place" => CreatePlaceAction(npc, request),
            "pick_up" => CreatePickUpAction(npc, request),
            "drop" => CreateDropAction(npc, request),
            "give" => CreateGiveAction(npc, request),
            "deposit" => CreateDepositAction(npc, request),
            "withdraw" => CreateWithdrawAction(npc, request),
            "wait" => CreateWaitAction(npc, request),
            _ => new FailedAction(request.ActionName, ActionFailureReason.InvalidTarget)
        };
    }

    private IAction CreateGoToAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count == 0)
        {
            return new FailedAction("GoTo", ActionFailureReason.InvalidTarget);
        }

        var targetPos = ParsePos(request.Arguments[0]);
        var options = new GoToOptions();

        if (request.Arguments.Count > 1 && request.Arguments[1] is Dictionary<string, object?> opts)
        {
            float tolerance = opts.TryGetValue("tolerance", out var t) && t != null ? Convert.ToSingle(t) : 0f;
            float timeout = opts.TryGetValue("timeout", out var to) && to != null ? Convert.ToSingle(to) : 30f;
            bool allowPartial = opts.TryGetValue("allow_partial", out var ap) && ap is bool b && b;
            float moveSpeed = opts.TryGetValue("move_speed", out var ms) && ms != null ? Convert.ToSingle(ms) : 3.5f;

            options = new GoToOptions
            {
                Tolerance = tolerance,
                Timeout = timeout,
                AllowPartial = allowPartial,
                MoveSpeed = moveSpeed
            };
        }

        return new GoToAction(npc, targetPos, options, _pathfindingService, _world, _eventBus, _walkability);
    }

    private IAction CreateGoNearAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 2)
        {
            return new FailedAction("GoNear", ActionFailureReason.InvalidTarget);
        }

        var targetPos = ParsePos(request.Arguments[0]);
        float dist = Convert.ToSingle(request.Arguments[1]);

        return new GoNearAction(npc, targetPos, dist, timeout: 30f, _pathfindingService, _world, _eventBus, _walkability);
    }

    private IAction CreateFollowAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 3 || _entityRegistry == null)
        {
            return new FailedAction("Follow", ActionFailureReason.InvalidTarget);
        }

        var targetId = new EntityId(Convert.ToInt32(request.Arguments[0]));
        float dist = Convert.ToSingle(request.Arguments[1]);
        float seconds = Convert.ToSingle(request.Arguments[2]);

        return new FollowAction(npc, targetId, dist, seconds, _entityRegistry, _pathfindingService, _world, _eventBus, _walkability);
    }

    private IAction CreateMineAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 1 || _world == null || _blockRegistry == null)
        {
            return new FailedAction("Mine", ActionFailureReason.InvalidTarget);
        }

        var pos = ParsePos(request.Arguments[0]);
        return new MineAction(npc, pos, _world, _blockRegistry, _containerManager);
    }

    private IAction CreatePlaceAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 2 || _world == null || _blockRegistry == null)
        {
            return new FailedAction("Place", ActionFailureReason.InvalidTarget);
        }

        var pos = ParsePos(request.Arguments[0]);
        string blockId = request.Arguments[1]?.ToString() ?? "";

        return new PlaceAction(npc, pos, blockId, _world, _blockRegistry, _containerManager, _entityRegistry);
    }

    private IAction CreatePickUpAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 1 || _entityRegistry == null)
        {
            return new FailedAction("PickUp", ActionFailureReason.InvalidTarget);
        }

        var targetId = new EntityId(Convert.ToInt32(request.Arguments[0]));
        return new PickUpAction(npc, targetId, _entityRegistry, _npcManager, _pathfindingService, _world, _eventBus);
    }

    private IAction CreateDropAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 2 || _entityRegistry == null)
        {
            return new FailedAction("Drop", ActionFailureReason.InvalidTarget);
        }

        string itemId = request.Arguments[0]?.ToString() ?? "";
        int count = Convert.ToInt32(request.Arguments[1]);

        return new DropAction(npc, itemId, count, _entityRegistry, _npcManager);
    }

    private IAction CreateGiveAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 3 || _entityRegistry == null)
        {
            return new FailedAction("Give", ActionFailureReason.InvalidTarget);
        }

        var receiverId = new EntityId(Convert.ToInt32(request.Arguments[0]));
        string itemId = request.Arguments[1]?.ToString() ?? "";
        int count = Convert.ToInt32(request.Arguments[2]);

        return new GiveAction(npc, receiverId, itemId, count, _entityRegistry);
    }

    private IAction CreateDepositAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 3 || _containerManager == null)
        {
            return new FailedAction("Deposit", ActionFailureReason.InvalidTarget);
        }

        var pos = ParsePos(request.Arguments[0]);
        string itemId = request.Arguments[1]?.ToString() ?? "";
        int count = Convert.ToInt32(request.Arguments[2]);

        return new DepositAction(npc, pos, itemId, count, _containerManager);
    }

    private IAction CreateWithdrawAction(NpcState npc, ActionRequest request)
    {
        if (request.Arguments.Count < 3 || _containerManager == null)
        {
            return new FailedAction("Withdraw", ActionFailureReason.InvalidTarget);
        }

        var pos = ParsePos(request.Arguments[0]);
        string itemId = request.Arguments[1]?.ToString() ?? "";
        int count = Convert.ToInt32(request.Arguments[2]);

        return new WithdrawAction(npc, pos, itemId, count, _containerManager);
    }

    private static IAction CreateWaitAction(NpcState npc, ActionRequest request)
    {
        float seconds = request.Arguments.Count > 0 && request.Arguments[0] != null
            ? Convert.ToSingle(request.Arguments[0])
            : 0f;

        return new WaitAction(npc, seconds);
    }

    private static Vec3i ParsePos(object? arg)
    {
        if (arg is Vec3i v)
        {
            return v;
        }

        if (arg is Dictionary<string, object?> dict &&
            dict.TryGetValue("x", out var x) &&
            dict.TryGetValue("y", out var y) &&
            dict.TryGetValue("z", out var z))
        {
            return new Vec3i(Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(z));
        }

        return Vec3i.Zero;
    }

    /// <summary>
    /// Fallback action that immediately fails with the given reason on start.
    /// </summary>
    private sealed class FailedAction : ActionBase
    {
        private readonly string _reason;

        public override string Name { get; }

        public FailedAction(string name, string reason)
            : base(0f)
        {
            Name = name;
            _reason = reason;
        }

        protected override void OnStart()
        {
            Fail(_reason);
        }

        protected override void OnTick(float dt)
        {
        }
    }
}
