namespace VoxelKing.Core.Navigation;

/// <summary>
/// Status of an active path follower navigating an NPC through waypoints.
/// </summary>
public enum PathFollowerStatus
{
    Idle,
    Following,
    Arrived,
    Stuck,
    Invalidated
}
