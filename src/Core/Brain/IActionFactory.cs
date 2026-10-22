namespace VoxelKing.Core.Brain;

using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;

/// <summary>
/// Factory interface for constructing concrete <see cref="IAction"/> instances
/// from Lua blocking action requests initiated by NPC brains.
/// </summary>
public interface IActionFactory
{
    /// <summary>
    /// Creates a concrete <see cref="IAction"/> for the specified action request on the given NPC.
    /// Returns null if the action cannot be created.
    /// </summary>
    IAction? CreateAction(NpcState npc, ActionRequest request);
}
