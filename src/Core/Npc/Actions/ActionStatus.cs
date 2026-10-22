namespace VoxelKing.Core.Npc.Actions;

using System;

/// <summary>
/// Status codes representing the lifecycle state of an NPC action.
/// </summary>
public enum ActionStatusCode
{
    NotStarted = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

/// <summary>
/// Immutable value representing the current execution status and optional failure reason of an action.
/// </summary>
public readonly record struct ActionStatus(ActionStatusCode Code, string? FailureReason = null)
{
    public bool IsNotStarted => Code == ActionStatusCode.NotStarted;
    public bool IsRunning => Code == ActionStatusCode.Running;
    public bool IsSucceeded => Code == ActionStatusCode.Succeeded;
    public bool IsFailed => Code == ActionStatusCode.Failed;
    public bool IsCompleted => IsSucceeded || IsFailed;

    public static ActionStatus NotStarted => new(ActionStatusCode.NotStarted);
    public static ActionStatus Running => new(ActionStatusCode.Running);
    public static ActionStatus Succeeded => new(ActionStatusCode.Succeeded);

    public static ActionStatus Failed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason cannot be null or empty.", nameof(reason));
        }

        return new ActionStatus(ActionStatusCode.Failed, reason);
    }

    public override string ToString() => IsFailed
        ? $"Failed({FailureReason})"
        : Code.ToString();
}
