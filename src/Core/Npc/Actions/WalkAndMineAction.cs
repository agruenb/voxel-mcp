namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Composite action that navigates an NPC near a target block and then mines it.
/// </summary>
public sealed class WalkAndMineAction : ActionBase
{
    public override string Name => "WalkAndMine";

    public NpcState Npc { get; }
    public Vec3i BlockPos { get; }
    public IVoxelWorld World { get; }
    public IBlockRegistry BlockRegistry { get; }
    public ContainerManager? ContainerManager { get; }
    public IPathfindingService? PathfindingService { get; }
    public IEventBus? EventBus { get; }

    private GoNearAction? _goNearAction;
    private MineAction? _mineAction;
    private bool _miningStarted;

    public WalkAndMineAction(
        NpcState npc,
        Vec3i blockPos,
        IVoxelWorld world,
        IBlockRegistry blockRegistry,
        ContainerManager? containerManager = null,
        IPathfindingService? pathfindingService = null,
        IEventBus? eventBus = null,
        float timeout = 45f)
        : base(timeout)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        BlockPos = blockPos;
        World = world ?? throw new ArgumentNullException(nameof(world));
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        ContainerManager = containerManager;
        PathfindingService = pathfindingService;
        EventBus = eventBus;
    }

    protected override void OnStart()
    {
        var blockCenter = new Vec3(BlockPos.X + 0.5f, BlockPos.Y + 0.5f, BlockPos.Z + 0.5f);
        if (Npc.Position.Distance(blockCenter) <= MineAction.DefaultMaxDistance)
        {
            StartMining();
        }
        else
        {
            _goNearAction = new GoNearAction(
                Npc,
                BlockPos,
                dist: 3.0f,
                timeout: Timeout,
                pathfindingService: PathfindingService,
                world: World,
                eventBus: EventBus);
            _goNearAction.Start();
        }
    }

    protected override void OnTick(float dt)
    {
        if (!_miningStarted)
        {
            if (_goNearAction == null)
            {
                Fail(ActionFailureReason.Unreachable);
                return;
            }

            _goNearAction.Tick(dt);

            if (_goNearAction.Status.IsSucceeded)
            {
                StartMining();
            }
            else if (_goNearAction.Status.IsFailed)
            {
                Fail(_goNearAction.Status.FailureReason ?? ActionFailureReason.Unreachable);
            }
        }
        else
        {
            if (_mineAction == null)
            {
                Fail(ActionFailureReason.InvalidTarget);
                return;
            }

            _mineAction.Tick(dt);

            if (_mineAction.Status.IsSucceeded)
            {
                Succeed();
            }
            else if (_mineAction.Status.IsFailed)
            {
                Fail(_mineAction.Status.FailureReason ?? ActionFailureReason.InvalidTarget);
            }
        }
    }

    private void StartMining()
    {
        _miningStarted = true;
        _mineAction = new MineAction(Npc, BlockPos, World, BlockRegistry, ContainerManager);
        _mineAction.Start();
        if (_mineAction.Status.IsCompleted && !_mineAction.Status.IsSucceeded)
        {
            Fail(_mineAction.Status.FailureReason ?? ActionFailureReason.InvalidTarget);
        }
    }

    protected override void OnCancel()
    {
        _goNearAction?.Cancel();
        _mineAction?.Cancel();
    }
}
