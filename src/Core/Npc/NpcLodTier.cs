namespace VoxelKing.Core.Npc;

/// <summary>
/// Simulation level-of-detail tier for an NPC based on distance to active players.
/// </summary>
public enum NpcLodTier : byte
{
    /// <summary>
    /// Near player (&lt;= LodDistanceThreshold, e.g. 64m): full brain tick rate (10 Hz nominal).
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Distant from players (&gt; LodDistanceThreshold and &lt;= FreezeDistance): reduced brain tick rate (2 Hz nominal).
    /// </summary>
    Distant = 1,

    /// <summary>
    /// Outside loaded chunk radius (&gt; FreezeDistance): frozen (0 Hz, no brain ticks).
    /// </summary>
    Frozen = 2
}
