namespace VoxelKing.Core.Npc.Perception;

/// <summary>
/// Abstraction for world clock time and day/night status queried by NPC perception.
/// </summary>
public interface IWorldTime
{
    /// <summary>
    /// Current time of day in hours, ranging from 0.0 to 24.0.
    /// </summary>
    float TimeOfDayHours { get; set; }

    /// <summary>
    /// Whether the current time of day falls during daytime hours.
    /// </summary>
    bool IsDay { get; }

    /// <summary>
    /// Advances world clock simulation time by the specified elapsed hours.
    /// </summary>
    void Advance(float hours);
}
