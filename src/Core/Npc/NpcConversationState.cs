namespace VoxelKing.Core.Npc;

/// <summary>
/// Conversation state of an NPC during interactive dialogue with a player or instructor (F-405).
/// </summary>
public enum NpcConversationState
{
    /// <summary>
    /// Not currently in conversation; normal simulation, actions, and idle/task behaviors run.
    /// </summary>
    None = 0,

    /// <summary>
    /// Player opened dialogue panel; NPC pauses current action and faces the player.
    /// </summary>
    Listening,

    /// <summary>
    /// Player submitted instruction; NPC shows animated "..." bubble while BrainCompiler runs.
    /// </summary>
    Thinking,

    /// <summary>
    /// BrainCompiler finished; NPC speaks the reply before resuming or starting the new task.
    /// </summary>
    Replying
}
