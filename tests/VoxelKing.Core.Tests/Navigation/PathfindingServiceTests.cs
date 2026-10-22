namespace VoxelKing.Core.Tests.Navigation;

using System;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;
using Xunit;

public class PathfindingServiceTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks) CreateTestWorld()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        return (world, blocks);
    }

    [Fact]
    public void FindPath_Synchronous_ReturnsValidResult()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var service = new PathfindingService(world);
        var result = service.FindPath(new Vec3i(0, 1, 0), new Vec3i(5, 1, 0));

        Assert.True(result.Success);
        Assert.Equal(6, result.Waypoints.Count);
    }

    [Fact]
    public async Task RequestPathAsync_ProcessesOnTick()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        for (int x = 0; x <= 5; x++)
        {
            world.SetBlock(x, 0, 0, stoneId);
        }

        var service = new PathfindingService(world);
        var task = service.RequestPathAsync(new Vec3i(0, 1, 0), new Vec3i(5, 1, 0));

        Assert.Equal(1, service.PendingRequestsCount);
        Assert.False(task.IsCompleted);

        // Process frame tick
        service.Tick();

        Assert.Equal(0, service.PendingRequestsCount);
        Assert.True(task.IsCompletedSuccessfully);

        var result = await task;
        Assert.True(result.Success);
        Assert.Equal(new Vec3i(5, 1, 0), result.Waypoints[^1]);
    }

    [Fact]
    public async Task CancelRequest_CompletesWithCancelledReason()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");
        world.SetBlock(0, 0, 0, stoneId);

        var service = new PathfindingService(world);
        var request = new PathRequest(42, new Vec3i(0, 1, 0), new Vec3i(5, 1, 0));
        service.RequestPath(request);

        Assert.Equal(1, service.PendingRequestsCount);

        bool cancelled = service.CancelRequest(42);
        Assert.True(cancelled);

        var result = await request.CompletionSource.Task;
        Assert.False(result.Success);
        Assert.Equal(PathFailureReason.Cancelled, result.FailureReason);
    }
}
