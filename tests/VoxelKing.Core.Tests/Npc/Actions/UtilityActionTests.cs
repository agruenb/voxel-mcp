namespace VoxelKing.Core.Tests.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using Xunit;

public class UtilityActionTests
{
    [Fact]
    public void WaitAction_SucceedsAfterDuration()
    {
        var npc = new NpcState(new EntityId(1), "Waiter", new Vec3(0.5f, 1.0f, 0.5f));
        npc.Velocity = new Vec3(2f, 0f, 2f);

        var action = new WaitAction(npc, 1.0f);
        action.Start();

        Assert.True(action.Status.IsRunning);
        Assert.Equal(0f, npc.Velocity.X);
        Assert.Equal(0f, npc.Velocity.Z);

        action.Tick(0.5f);
        Assert.True(action.Status.IsRunning);

        action.Tick(0.6f);
        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void WaitAction_SucceedsImmediatelyWhenZeroSeconds()
    {
        var npc = new NpcState(new EntityId(1), "Waiter", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new WaitAction(npc, 0f);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
    }

    [Fact]
    public void WaitAction_FailsInvalidTargetWhenNegativeSeconds()
    {
        var npc = new NpcState(new EntityId(1), "Waiter", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new WaitAction(npc, -5f);
        action.Start();

        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.InvalidTarget, action.Status.FailureReason);
    }

    [Fact]
    public void WaitAction_FailsInterruptedWhenCancelled()
    {
        var npc = new NpcState(new EntityId(1), "Waiter", new Vec3(0.5f, 1.0f, 0.5f));
        var action = new WaitAction(npc, 10f);
        action.Start();

        action.Cancel();
        Assert.True(action.Status.IsFailed);
        Assert.Equal(ActionFailureReason.Interrupted, action.Status.FailureReason);
    }

    [Fact]
    public void SayAction_SpeaksAndPublishesEvent()
    {
        var bus = new EventBus();
        EntitySpoke? received = null;
        bus.Subscribe<EntitySpoke>(e => received = e);

        var npc = new NpcState(new EntityId(1), "Speaker", new Vec3(2.0f, 1.0f, 3.0f));
        var action = new SayAction(npc, "Hello world!", bus, currentTime: 10.0f);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.True(action.WasSpoken);
        Assert.NotNull(received);
        Assert.Equal("Hello world!", received.Value.Text);
        Assert.Equal(npc.Id, received.Value.SpeakerId);
        Assert.Equal(16.0f, received.Value.HearingRadius);
    }

    [Fact]
    public void SayAction_TruncatesTextLongerThan200Chars()
    {
        var bus = new EventBus();
        EntitySpoke? received = null;
        bus.Subscribe<EntitySpoke>(e => received = e);

        var npc = new NpcState(new EntityId(1), "Speaker", new Vec3(0, 0, 0));
        string longText = new('a', 300);

        var action = new SayAction(npc, longText, bus, currentTime: 10.0f);
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        Assert.NotNull(received);
        Assert.Equal(200, received.Value.Text.Length);
    }

    [Fact]
    public void SayAction_RateLimitsSubsequentCallsWithin2Seconds()
    {
        var bus = new EventBus();
        int speakCount = 0;
        bus.Subscribe<EntitySpoke>(_ => speakCount++);

        var npc = new NpcState(new EntityId(1), "Speaker", new Vec3(0, 0, 0));

        // First call at time 10s: succeeds and speaks
        var action1 = new SayAction(npc, "First", bus, currentTime: 10.0f);
        action1.Start();
        Assert.True(action1.WasSpoken);
        Assert.Equal(1, speakCount);

        // Second call at time 11s (< 2s elapsed): dropped per spec
        var action2 = new SayAction(npc, "Second", bus, currentTime: 11.0f);
        action2.Start();
        Assert.False(action2.WasSpoken);
        Assert.True(action2.Status.IsSucceeded); // Doesn't fail, just dropped
        Assert.Equal(1, speakCount);

        // Third call at time 12.5s (>= 2s elapsed since last speech at 10s): succeeds and speaks
        var action3 = new SayAction(npc, "Third", bus, currentTime: 12.5f);
        action3.Start();
        Assert.True(action3.WasSpoken);
        Assert.Equal(2, speakCount);
    }

    [Fact]
    public void LookAtAction_SetsNpcFacingDirectionTowardsTarget()
    {
        var npc = new NpcState(new EntityId(1), "Looker", new Vec3(0f, 1f, 0f));

        // Look toward +X
        var action = new LookAtAction(npc, new Vec3(5f, 1f, 0f));
        action.Start();

        Assert.True(action.Status.IsSucceeded);
        var dir = npc.GetFacingDirection();
        Assert.True(dir.X > 0.9f);
        Assert.True(MathF.Abs(dir.Z) < 0.1f);
    }
}
