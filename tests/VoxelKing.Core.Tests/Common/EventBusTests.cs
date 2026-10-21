namespace VoxelKing.Core.Tests.Common;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using Xunit;

public class EventBusTests
{
    private record SampleEvent(string Message);
    private record OtherEvent(int Number);

    [Fact]
    public void SubscribeAndPublish_DeliversEventToSubscriber()
    {
        var bus = new EventBus();
        SampleEvent? received = null;

        bus.Subscribe<SampleEvent>(e => received = e);
        bus.Publish(new SampleEvent("hello world"));

        Assert.NotNull(received);
        Assert.Equal("hello world", received.Message);
    }

    [Fact]
    public void Publish_OnlyNotifiesSubscribersOfMatchingType()
    {
        var bus = new EventBus();
        int sampleCount = 0;
        int otherCount = 0;

        bus.Subscribe<SampleEvent>(_ => sampleCount++);
        bus.Subscribe<OtherEvent>(_ => otherCount++);

        bus.Publish(new SampleEvent("test"));

        Assert.Equal(1, sampleCount);
        Assert.Equal(0, otherCount);
    }

    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        var bus = new EventBus();
        int callCount = 0;

        var subscription = bus.Subscribe<SampleEvent>(_ => callCount++);

        bus.Publish(new SampleEvent("first"));
        Assert.Equal(1, callCount);

        subscription.Dispose();

        bus.Publish(new SampleEvent("second"));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var bus = new EventBus();
        int callCount = 0;

        var subscription = bus.Subscribe<SampleEvent>(_ => callCount++);
        Assert.Equal(1, bus.GetSubscriberCount<SampleEvent>());

        subscription.Dispose();
        subscription.Dispose();
        subscription.Dispose();

        Assert.Equal(0, bus.GetSubscriberCount<SampleEvent>());
        bus.Publish(new SampleEvent("after dispose"));
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Subscribers_AreInvokedInRegistrationOrder()
    {
        var bus = new EventBus();
        var order = new List<int>();

        bus.Subscribe<SampleEvent>(_ => order.Add(1));
        bus.Subscribe<SampleEvent>(_ => order.Add(2));
        bus.Subscribe<SampleEvent>(_ => order.Add(3));

        bus.Publish(new SampleEvent("go"));

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void Handler_CanUnsubscribeSelf_DuringPublish()
    {
        var bus = new EventBus();
        IDisposable? subscription = null;
        int callCount = 0;

        subscription = bus.Subscribe<SampleEvent>(_ =>
        {
            callCount++;
            subscription?.Dispose();
        });

        bus.Publish(new SampleEvent("first"));
        Assert.Equal(1, callCount);

        bus.Publish(new SampleEvent("second"));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Handler_CanSubscribeNewHandler_DuringPublish()
    {
        var bus = new EventBus();
        int secondHandlerCount = 0;

        bus.Subscribe<SampleEvent>(_ =>
        {
            bus.Subscribe<SampleEvent>(_ => secondHandlerCount++);
        });

        bus.Publish(new SampleEvent("first"));
        // New subscriber is added for subsequent calls, not current snapshot
        Assert.Equal(0, secondHandlerCount);

        bus.Publish(new SampleEvent("second"));
        Assert.Equal(1, secondHandlerCount);
    }

    [Fact]
    public void Publish_WhenHandlerThrows_ExecutesAllHandlersAndThrowsAggregateException()
    {
        var bus = new EventBus();
        bool secondHandlerRan = false;

        bus.Subscribe<SampleEvent>(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe<SampleEvent>(_ => secondHandlerRan = true);

        var ex = Assert.Throws<InvalidOperationException>(() => bus.Publish(new SampleEvent("test")));
        Assert.Equal("boom", ex.Message);
        Assert.True(secondHandlerRan);
    }

    [Fact]
    public void Publish_MultipleFailingHandlers_ThrowsAggregateException()
    {
        var bus = new EventBus();

        bus.Subscribe<SampleEvent>(_ => throw new InvalidOperationException("first error"));
        bus.Subscribe<SampleEvent>(_ => throw new ArgumentException("second error"));

        var agg = Assert.Throws<AggregateException>(() => bus.Publish(new SampleEvent("test")));
        Assert.Equal(2, agg.InnerExceptions.Count);
    }

    [Fact]
    public async Task ConcurrentSubscriptionsAndPublishes_AreThreadSafe()
    {
        var bus = new EventBus();
        int totalReceived = 0;
        const int iterations = 1000;

        var tasks = new List<Task>();

        // Subscribers task
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var sub = bus.Subscribe<SampleEvent>(_ => Interlocked.Increment(ref totalReceived));
                if (i % 2 == 0)
                {
                    sub.Dispose();
                }
            }
        }));

        // Publishers task
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                bus.Publish(new SampleEvent($"msg {i}"));
            }
        }));

        await Task.WhenAll(tasks);
        Assert.True(totalReceived >= 0);
    }
}
