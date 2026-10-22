namespace VoxelKing.Core.Npc.Perception;

using System;

/// <summary>
/// World clock simulation tracking time of day in hours (0..24) and daytime status.
/// </summary>
public sealed class WorldTime : IWorldTime
{
    public const float DefaultDayStartHour = 6.0f;
    public const float DefaultDayEndHour = 18.0f;
    public const float DefaultInitialHour = 12.0f;

    private float _timeOfDayHours;

    public float DayStartHour { get; set; } = DefaultDayStartHour;
    public float DayEndHour { get; set; } = DefaultDayEndHour;

    public float TimeOfDayHours
    {
        get => _timeOfDayHours;
        set => _timeOfDayHours = NormalizeHours(value);
    }

    public bool IsDay => _timeOfDayHours >= DayStartHour && _timeOfDayHours < DayEndHour;

    public WorldTime(float initialHour = DefaultInitialHour, float dayStartHour = DefaultDayStartHour, float dayEndHour = DefaultDayEndHour)
    {
        DayStartHour = dayStartHour;
        DayEndHour = dayEndHour;
        TimeOfDayHours = initialHour;
    }

    public void Advance(float hours)
    {
        TimeOfDayHours += hours;
    }

    private static float NormalizeHours(float hours)
    {
        float mod = hours % 24.0f;
        return mod < 0f ? mod + 24.0f : mod;
    }
}
