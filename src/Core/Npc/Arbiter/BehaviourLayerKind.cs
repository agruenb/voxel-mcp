namespace VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Execution layers of the behaviour arbiter ordered by increasing priority:
/// Idle (lowest) &lt; Task &lt; Reflex (highest).
/// </summary>
public enum BehaviourLayerKind
{
    None = 0,
    Idle = 1,
    Task = 2,
    Reflex = 3
}
