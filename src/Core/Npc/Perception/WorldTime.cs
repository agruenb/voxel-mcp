namespace VoxelKing.Core.Npc.Perception;

using System;

/// <summary>
/// World clock simulation tracking time of day in hours (0..24), daytime status,
/// and total elapsed time backed by a configurable day length.
/// </summary>
public sealed class WorldTime : IWorldTime
{
    public const float DefaultDayStartHour = 6.0f;
    public const float DefaultDayEndHour = 18.0f;
    public const float DefaultInitialHour = 12.0f;
    public const float DefaultDayLengthSeconds = 600.0f;

    private float _timeOfDayHours;
    private double _totalElapsedSeconds;
    private float _dayLengthSeconds;

    public float DayStartHour { get; set; } = DefaultDayStartHour;
    public float DayEndHour { get; set; } = DefaultDayEndHour;

    public float DayLengthSeconds
    {
        get => _dayLengthSeconds;
        set => _dayLengthSeconds = Math.Max(0.0f, value);
    }

    public float TimeOfDayHours
    {
        get => _timeOfDayHours;
        set
        {
            bool wasDay = IsDay;
            _timeOfDayHours = NormalizeHours(value);
            SyncTotalSecondsFromHours();
            CheckDayNightTransition(wasDay);
        }
    }

    public double TotalElapsedSeconds
    {
        get => _totalElapsedSeconds;
        set
        {
            bool wasDay = IsDay;
            _totalElapsedSeconds = Math.Max(0.0, value);
            SyncHoursFromTotalSeconds();
            CheckDayNightTransition(wasDay);
        }
    }

    public bool IsDay
    {
        get
        {
            if (DayStartHour <= DayEndHour)
            {
                return _timeOfDayHours >= DayStartHour && _timeOfDayHours < DayEndHour;
            }
            return _timeOfDayHours >= DayStartHour || _timeOfDayHours < DayEndHour;
        }
    }

    public event Action<bool>? DayNightChanged;

    public WorldTime(
        float initialHour = DefaultInitialHour,
        float dayLengthSeconds = DefaultDayLengthSeconds,
        float dayStartHour = DefaultDayStartHour,
        float dayEndHour = DefaultDayEndHour)
    {
        DayStartHour = dayStartHour;
        DayEndHour = dayEndHour;
        _dayLengthSeconds = Math.Max(0.0f, dayLengthSeconds);
        _timeOfDayHours = NormalizeHours(initialHour);
        if (_dayLengthSeconds > 0)
        {
            _totalElapsedSeconds = (_timeOfDayHours / 24.0) * _dayLengthSeconds;
        }
    }

    public void Advance(float hours)
    {
        if (float.IsNaN(hours) || float.IsInfinity(hours))
        {
            return;
        }

        bool wasDay = IsDay;
        _timeOfDayHours = NormalizeHours(_timeOfDayHours + hours);
        if (_dayLengthSeconds > 0)
        {
            _totalElapsedSeconds += (hours / 24.0) * _dayLengthSeconds;
            if (_totalElapsedSeconds < 0.0)
            {
                _totalElapsedSeconds = 0.0;
            }
        }
        CheckDayNightTransition(wasDay);
    }

    public void AdvanceSeconds(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || _dayLengthSeconds <= 0f)
        {
            return;
        }

        bool wasDay = IsDay;
        _totalElapsedSeconds += deltaSeconds;
        float elapsedHours = (deltaSeconds / _dayLengthSeconds) * 24.0f;
        _timeOfDayHours = NormalizeHours(_timeOfDayHours + elapsedHours);
        CheckDayNightTransition(wasDay);
    }

    public void SetTime(float hours)
    {
        TimeOfDayHours = hours;
    }

    private void SyncTotalSecondsFromHours()
    {
        if (_dayLengthSeconds <= 0) return;
        long dayCount = (long)Math.Floor(_totalElapsedSeconds / _dayLengthSeconds);
        if (dayCount < 0) dayCount = 0;
        _totalElapsedSeconds = (dayCount + (_timeOfDayHours / 24.0)) * _dayLengthSeconds;
    }

    private void SyncHoursFromTotalSeconds()
    {
        if (_dayLengthSeconds <= 0) return;
        double dayProgress = (_totalElapsedSeconds / _dayLengthSeconds) % 1.0;
        if (dayProgress < 0) dayProgress += 1.0;
        _timeOfDayHours = (float)(dayProgress * 24.0);
    }

    private void CheckDayNightTransition(bool wasDay)
    {
        bool nowDay = IsDay;
        if (wasDay != nowDay)
        {
            DayNightChanged?.Invoke(nowDay);
        }
    }

    public static float NormalizeHours(float hours)
    {
        if (float.IsNaN(hours) || float.IsInfinity(hours)) return 0f;
        float mod = hours % 24.0f;
        return mod < 0f ? mod + 24.0f : mod;
    }
}
