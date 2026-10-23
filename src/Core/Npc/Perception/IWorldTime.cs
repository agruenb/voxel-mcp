namespace VoxelKing.Core.Npc.Perception;

using System;

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
    /// Total duration in real-world seconds of a full 24-hour day/night cycle.
    /// </summary>
    float DayLengthSeconds { get; set; }

    /// <summary>
    /// Cumulative simulation time elapsed in real-world seconds.
    /// </summary>
    double TotalElapsedSeconds { get; set; }

    /// <summary>
    /// Hour (0..24) marking the start of daytime.
    /// </summary>
    float DayStartHour { get; set; }

    /// <summary>
    /// Hour (0..24) marking the end of daytime (start of night).
    /// </summary>
    float DayEndHour { get; set; }

    /// <summary>
    /// Event triggered when daytime transitions to nighttime or vice-versa.
    /// </summary>
    event Action<bool>? DayNightChanged;

    /// <summary>
    /// Advances world clock simulation time by the specified elapsed hours.
    /// </summary>
    void Advance(float hours);

    /// <summary>
    /// Advances world clock simulation time by the specified elapsed seconds.
    /// </summary>
    void AdvanceSeconds(float deltaSeconds);

    /// <summary>
    /// Sets the current time of day in hours (0..24).
    /// </summary>
    void SetTime(float hours);
}
