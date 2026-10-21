namespace VoxelKing.Core.Tests.Common;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using Xunit;

public class MainThreadDispatcherTests
{
    [Fact]
    public void Enqueue_ExecutesInStrictFifoOrder()
    {
        var dispatcher = new MainThreadDispatcher();
        var executionOrder = new List<int>();

        for (int i = 1; i <= 10; i++)
        {
            int index = i;
            dispatcher.Enqueue(() => executionOrder.Add(index));
        }

        Assert.Equal(10, dispatcher.PendingCount);

        int drained = dispatcher.Drain();

        Assert.Equal(10, drained);
        Assert.Equal(0, dispatcher.PendingCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
    }

    [Fact]
    public void Drain_WithZeroOrNegativeBudget_DoesNotExecuteActions()
    {
        var dispatcher = new MainThreadDispatcher();
        bool executed = false;

        dispatcher.Enqueue(() => executed = true);

        int drainedZero = dispatcher.Drain(TimeSpan.Zero);
        Assert.Equal(0, drainedZero);
        Assert.False(executed);
        Assert.Equal(1, dispatcher.PendingCount);

        int drainedNegative = dispatcher.Drain(TimeSpan.FromMilliseconds(-10));
        Assert.Equal(0, drainedNegative);
        Assert.False(executed);
        Assert.Equal(1, dispatcher.PendingCount);
    }

    [Fact]
    public void Drain_RespectsTimeBudget_AndPreservesOrderAcrossFrames()
    {
        var dispatcher = new MainThreadDispatcher();
        var executionOrder = new List<int>();

        // Enqueue actions where each sleeps 15ms
        for (int i = 1; i <= 5; i++)
        {
            int index = i;
            dispatcher.Enqueue(() =>
            {
                Thread.Sleep(15);
                executionOrder.Add(index);
            });
        }

        // Drain with 25ms budget — should execute ~2 items
        int drainedFrame1 = dispatcher.Drain(TimeSpan.FromMilliseconds(25));
        Assert.True(drainedFrame1 >= 1 && drainedFrame1 < 5);
        Assert.True(dispatcher.PendingCount > 0);

        // Next drain finishes remaining items
        int drainedFrame2 = dispatcher.Drain(TimeSpan.FromSeconds(2));
        Assert.Equal(5, drainedFrame1 + drainedFrame2);
        Assert.Equal(0, dispatcher.PendingCount);

        // Verify strictly preserved FIFO order across frames
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, executionOrder);
    }

    [Fact]
    public void Drain_WhenActionReEnqueues_AvoidsInfiniteLoopWhenNoTimeBudget()
    {
        var dispatcher = new MainThreadDispatcher();
        int initialCount = 0;
        int secondCount = 0;

        dispatcher.Enqueue(() =>
        {
            initialCount++;
            dispatcher.Enqueue(() => secondCount++);
        });

        int drainedFirst = dispatcher.Drain();
        Assert.Equal(1, drainedFirst);
        Assert.Equal(1, initialCount);
        Assert.Equal(0, secondCount);
        Assert.Equal(1, dispatcher.PendingCount);

        int drainedSecond = dispatcher.Drain();
        Assert.Equal(1, drainedSecond);
        Assert.Equal(1, secondCount);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public async Task EnqueueAsync_Action_CompletesWhenDrained()
    {
        var dispatcher = new MainThreadDispatcher();
        bool actionRan = false;

        var task = dispatcher.EnqueueAsync(() => actionRan = true);
        Assert.False(task.IsCompleted);

        dispatcher.Drain();

        await task;
        Assert.True(actionRan);
    }

    [Fact]
    public async Task EnqueueAsync_Func_ReturnsResultWhenDrained()
    {
        var dispatcher = new MainThreadDispatcher();

        var task = dispatcher.EnqueueAsync(() => 42 * 2);
        Assert.False(task.IsCompleted);

        dispatcher.Drain();

        int result = await task;
        Assert.Equal(84, result);
    }

    [Fact]
    public async Task EnqueueAsync_FaultsTask_WhenActionThrows()
    {
        var dispatcher = new MainThreadDispatcher();

        var task = dispatcher.EnqueueAsync(() => throw new InvalidOperationException("async failure"));

        dispatcher.Drain();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("async failure", ex.Message);
    }

    [Fact]
    public void Enqueue_WhenActionThrows_InvokesUnhandledExceptionHandler_AndContinuesDraining()
    {
        var dispatcher = new MainThreadDispatcher();
        Exception? caughtException = null;
        var executedActions = new List<int>();

        dispatcher.UnhandledException += ex => caughtException = ex;

        dispatcher.Enqueue(() => executedActions.Add(1));
        dispatcher.Enqueue(() => throw new ArgumentException("bad argument"));
        dispatcher.Enqueue(() => executedActions.Add(3));

        int drained = dispatcher.Drain();

        Assert.Equal(3, drained);
        Assert.NotNull(caughtException);
        Assert.IsType<ArgumentException>(caughtException);
        Assert.Equal("bad argument", caughtException.Message);
        Assert.Equal(new[] { 1, 3 }, executedActions);
    }

    [Fact]
    public async Task ConcurrentEnqueue_ThreadSafety()
    {
        var dispatcher = new MainThreadDispatcher();
        const int threads = 4;
        const int itemsPerThread = 250;
        var executed = 0;

        var tasks = new List<Task>();
        for (int t = 0; t < threads; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    dispatcher.Enqueue(() => Interlocked.Increment(ref executed));
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(threads * itemsPerThread, dispatcher.PendingCount);

        int drained = dispatcher.Drain();
        Assert.Equal(threads * itemsPerThread, drained);
        Assert.Equal(threads * itemsPerThread, executed);
        Assert.Equal(0, dispatcher.PendingCount);
    }
}
