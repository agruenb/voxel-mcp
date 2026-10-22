namespace VoxelKing.Core.Tests.Npc.Actions;

using System;
using VoxelKing.Core.Npc.Actions;
using Xunit;

public class ActionStatusTests
{
    [Fact]
    public void ActionStatus_InitialState_Correct()
    {
        var notStarted = ActionStatus.NotStarted;
        Assert.True(notStarted.IsNotStarted);
        Assert.False(notStarted.IsRunning);
        Assert.False(notStarted.IsSucceeded);
        Assert.False(notStarted.IsFailed);
        Assert.False(notStarted.IsCompleted);

        var running = ActionStatus.Running;
        Assert.False(running.IsNotStarted);
        Assert.True(running.IsRunning);
        Assert.False(running.IsSucceeded);
        Assert.False(running.IsFailed);
        Assert.False(running.IsCompleted);

        var succeeded = ActionStatus.Succeeded;
        Assert.True(succeeded.IsSucceeded);
        Assert.True(succeeded.IsCompleted);

        var failed = ActionStatus.Failed(ActionFailureReason.Timeout);
        Assert.True(failed.IsFailed);
        Assert.True(failed.IsCompleted);
        Assert.Equal(ActionFailureReason.Timeout, failed.FailureReason);
        Assert.Equal("Failed(timeout)", failed.ToString());
    }

    [Fact]
    public void ActionStatus_Failed_ThrowsOnNullOrEmptyReason()
    {
        Assert.Throws<ArgumentException>(() => ActionStatus.Failed(""));
        Assert.Throws<ArgumentException>(() => ActionStatus.Failed("   "));
    }

    [Fact]
    public void ActionFailureReason_ContainsAllClosedSetReasons()
    {
        var expected = new[]
        {
            "unreachable",
            "stuck",
            "too_far",
            "no_item",
            "inventory_full",
            "invalid_target",
            "target_gone",
            "occupied",
            "interrupted",
            "timeout"
        };

        Assert.Equal(10, ActionFailureReason.All.Count);
        foreach (var reason in expected)
        {
            Assert.True(ActionFailureReason.IsValid(reason), $"Reason '{reason}' should be valid");
        }

        Assert.False(ActionFailureReason.IsValid("unknown_reason"));
        Assert.False(ActionFailureReason.IsValid(""));
    }
}
