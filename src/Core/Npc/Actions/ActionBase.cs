namespace VoxelKing.Core.Npc.Actions;

using System;

/// <summary>
/// Abstract base class providing common lifecycle management, timeout tracking, and cancellation for actions.
/// </summary>
public abstract class ActionBase : IAction
{
    public abstract string Name { get; }
    public ActionStatus Status { get; protected set; } = ActionStatus.NotStarted;
    public float Timeout { get; }
    public float ElapsedTime { get; protected set; }

    protected ActionBase(float timeout = 0f)
    {
        Timeout = Math.Max(0f, timeout);
    }

    /// <inheritdoc />
    public void Start()
    {
        if (Status.IsRunning || Status.IsCompleted)
        {
            return;
        }

        Status = ActionStatus.Running;
        OnStart();
    }

    /// <inheritdoc />
    public ActionStatus Tick(float dt)
    {
        if (Status.IsCompleted)
        {
            return Status;
        }

        if (Status.IsNotStarted)
        {
            Start();
            if (Status.IsCompleted)
            {
                return Status;
            }
        }

        ElapsedTime += dt;

        if (Timeout > 0f && ElapsedTime >= Timeout)
        {
            OnTimeout();
            Fail(ActionFailureReason.Timeout);
            return Status;
        }

        OnTick(dt);
        return Status;
    }

    /// <inheritdoc />
    public void Cancel()
    {
        if (Status.IsRunning)
        {
            OnCancel();
            Fail(ActionFailureReason.Interrupted);
        }
    }

    protected void Succeed()
    {
        Status = ActionStatus.Succeeded;
    }

    protected void Fail(string reason)
    {
        Status = ActionStatus.Failed(reason);
    }

    protected virtual void OnStart() { }

    protected virtual void OnTick(float dt) { }

    protected virtual void OnCancel() { }

    protected virtual void OnTimeout() { }
}
