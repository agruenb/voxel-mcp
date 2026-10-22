namespace VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Behaviour arbiter managing layer prioritization (Reflex &gt; Task &gt; Idle)
/// and pre-emption per ARCHITECTURE §5.2.
/// </summary>
public interface IBehaviourArbiter
{
    /// <summary>
    /// Current dominant execution layer controlling the NPC.
    /// </summary>
    BehaviourLayerKind ActiveLayer { get; }

    /// <summary>
    /// The reflex layer (Priority 1 - highest).
    /// </summary>
    IReflexLayer ReflexLayer { get; }

    /// <summary>
    /// The task layer (Priority 2 - driven by Lua BrainHost).
    /// </summary>
    ITaskLayer TaskLayer { get; }

    /// <summary>
    /// The idle layer (Priority 3 - lowest, wander and look).
    /// </summary>
    IIdleLayer IdleLayer { get; }

    /// <summary>
    /// Discrete arbitration tick, prioritizing Reflex &gt; Task &gt; Idle.
    /// </summary>
    void Tick(float dt);

    /// <summary>
    /// Physics process tick, evaluating immediate reflex hazards (water, cliffs, suffocation).
    /// </summary>
    void PhysicsTick(float dt);

    /// <summary>
    /// Resets all layers and returns arbiter to idle.
    /// </summary>
    void Reset();
}
