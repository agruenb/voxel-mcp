namespace VoxelKing.Core.Common;

/// <summary>
/// Read-only view of the game configuration tree.
/// </summary>
public interface IGameConfig
{
    WorldConfig World { get; }
    NpcConfig Npc { get; }
    BrainLimitsConfig BrainLimits { get; }
    LlmConfig Llm { get; }
    DebugConfig Debug { get; }
    PlayerConfig Player { get; }
    SaveConfig Save { get; }
}
