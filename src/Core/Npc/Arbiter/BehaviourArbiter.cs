namespace VoxelKing.Core.Npc.Arbiter;

using System;
using VoxelKing.Core.Npc.Actions;

/// <summary>
/// Authoritative behaviour arbiter coordinating Reflex &gt; Task &gt; Idle layers per ARCHITECTURE §5.2.
/// Reflexes pre-empt the task layer temporarily; when task layer is idle or finished, the idle layer runs.
/// </summary>
public sealed class BehaviourArbiter : IBehaviourArbiter
{
    private readonly NpcState _npc;

    public BehaviourLayerKind ActiveLayer { get; private set; } = BehaviourLayerKind.None;
    public IReflexLayer ReflexLayer { get; }
    public ITaskLayer TaskLayer { get; }
    public IIdleLayer IdleLayer { get; }

    public BehaviourArbiter(
        NpcState npc,
        IReflexLayer reflexLayer,
        ITaskLayer taskLayer,
        IIdleLayer idleLayer)
    {
        _npc = npc ?? throw new ArgumentNullException(nameof(npc));
        ReflexLayer = reflexLayer ?? throw new ArgumentNullException(nameof(reflexLayer));
        TaskLayer = taskLayer ?? throw new ArgumentNullException(nameof(taskLayer));
        IdleLayer = idleLayer ?? throw new ArgumentNullException(nameof(idleLayer));
    }

    /// <inheritdoc />
    public void PhysicsTick(float dt)
    {
        // Immediate reflex evaluation during physics ticks (e.g. 60Hz) to prevent falling/suffocating
        if (ReflexLayer.CheckReflex(dt))
        {
            if (ActiveLayer != BehaviourLayerKind.Reflex)
            {
                PreemptForReflex();
            }
        }
    }

    /// <inheritdoc />
    public void Tick(float dt)
    {
        // 1. Reflex layer (Priority 1 - highest)
        if (ReflexLayer.CheckReflex(dt))
        {
            if (ActiveLayer != BehaviourLayerKind.Reflex)
            {
                PreemptForReflex();
            }
            return;
        }

        // Reflex condition cleared
        if (ActiveLayer == BehaviourLayerKind.Reflex)
        {
            ActiveLayer = BehaviourLayerKind.None;
        }

        // 2. Task layer (Priority 2)
        if (TaskLayer.HasActiveTask)
        {
            ActiveLayer = BehaviourLayerKind.Task;
            TaskLayer.Tick(dt);
            return;
        }

        // 3. Idle layer (Priority 3 - lowest)
        ActiveLayer = BehaviourLayerKind.Idle;
        IdleLayer.Tick(dt);
    }

    /// <inheritdoc />
    public void Reset()
    {
        ActiveLayer = BehaviourLayerKind.None;
        ReflexLayer.Reset();
        IdleLayer.Reset();
    }

    private void PreemptForReflex()
    {
        if (TaskLayer.HasActiveTask)
        {
            TaskLayer.Interrupt();
        }

        if (_npc.CurrentAction != null && _npc.CurrentAction.Status.IsRunning)
        {
            _npc.CurrentAction.Cancel();
        }

        ActiveLayer = BehaviourLayerKind.Reflex;
    }
}
