namespace VoxelKing.Core.Tests.Npc.Perception;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Npc.Perception;
using Xunit;

public class WorldTimeTests
{
    [Fact]
    public void Constructor_DefaultValues_AreCorrect()
    {
        var time = new WorldTime();

        Assert.Equal(12.0f, time.TimeOfDayHours);
        Assert.True(time.IsDay);
        Assert.Equal(6.0f, time.DayStartHour);
        Assert.Equal(18.0f, time.DayEndHour);
        Assert.Equal(600.0f, time.DayLengthSeconds);
        Assert.Equal(300.0, time.TotalElapsedSeconds, precision: 3);
    }

    [Fact]
    public void Constructor_CustomValues_SetsProperties()
    {
        var time = new WorldTime(initialHour: 8.0f, dayLengthSeconds: 1200.0f, dayStartHour: 7.0f, dayEndHour: 19.0f);

        Assert.Equal(8.0f, time.TimeOfDayHours);
        Assert.True(time.IsDay);
        Assert.Equal(7.0f, time.DayStartHour);
        Assert.Equal(19.0f, time.DayEndHour);
        Assert.Equal(1200.0f, time.DayLengthSeconds);
        Assert.Equal(400.0, time.TotalElapsedSeconds, precision: 3);
    }

    [Fact]
    public void Advance_Hours_UpdatesTimeAndTotalSeconds()
    {
        var time = new WorldTime(initialHour: 12.0f, dayLengthSeconds: 600.0f);

        time.Advance(6.0f);

        Assert.Equal(18.0f, time.TimeOfDayHours);
        Assert.False(time.IsDay);
        Assert.Equal(450.0, time.TotalElapsedSeconds, precision: 3);

        time.Advance(8.0f);

        Assert.Equal(2.0f, time.TimeOfDayHours); // 26 % 24 = 2
        Assert.False(time.IsDay);
        Assert.Equal(650.0, time.TotalElapsedSeconds, precision: 3);
    }

    [Fact]
    public void AdvanceSeconds_AdvancesTimeProportionally()
    {
        var time = new WorldTime(initialHour: 12.0f, dayLengthSeconds: 600.0f);

        // 150 seconds = 1/4 of day cycle = 6 hours
        time.AdvanceSeconds(150.0f);

        Assert.Equal(18.0f, time.TimeOfDayHours, tolerance: 0.001f);
        Assert.False(time.IsDay);
        Assert.Equal(450.0, time.TotalElapsedSeconds, precision: 3);

        // Another 150 seconds = another 6 hours -> midnight (0.0h)
        time.AdvanceSeconds(150.0f);

        Assert.Equal(0.0f, time.TimeOfDayHours, tolerance: 0.001f);
        Assert.False(time.IsDay);
        Assert.Equal(600.0, time.TotalElapsedSeconds, precision: 3);

        // 300 seconds = 12 hours -> noon (12.0h)
        time.AdvanceSeconds(300.0f);

        Assert.Equal(12.0f, time.TimeOfDayHours, tolerance: 0.001f);
        Assert.True(time.IsDay);
        Assert.Equal(900.0, time.TotalElapsedSeconds, precision: 3);
    }

    [Fact]
    public void TimeOfDayHours_Normalization_WrapsCorrectly()
    {
        var time = new WorldTime();

        time.TimeOfDayHours = 25.5f;
        Assert.Equal(1.5f, time.TimeOfDayHours, tolerance: 0.001f);

        time.TimeOfDayHours = -3.0f;
        Assert.Equal(21.0f, time.TimeOfDayHours, tolerance: 0.001f);

        time.TimeOfDayHours = 48.0f;
        Assert.Equal(0.0f, time.TimeOfDayHours, tolerance: 0.001f);
    }

    [Fact]
    public void TotalElapsedSeconds_SyncsWithTimeOfDayHours()
    {
        var time = new WorldTime(initialHour: 12.0f, dayLengthSeconds: 600.0f);

        // Setting TotalElapsedSeconds directly (as in save restoration)
        time.TotalElapsedSeconds = 150.0; // 150/600 * 24 = 6.0h (dawn)
        Assert.Equal(6.0f, time.TimeOfDayHours, tolerance: 0.001f);
        Assert.True(time.IsDay);

        // Advancing over multiple days
        time.TotalElapsedSeconds = 1350.0; // 2 days + 150s -> 6.0h
        Assert.Equal(6.0f, time.TimeOfDayHours, tolerance: 0.001f);

        // Setting TimeOfDayHours preserves day index
        time.TimeOfDayHours = 18.0f;
        Assert.Equal(18.0f, time.TimeOfDayHours, tolerance: 0.001f);
        Assert.Equal(1650.0, time.TotalElapsedSeconds, precision: 3); // 2 days (1200) + 18/24*600 (450)
    }

    [Fact]
    public void IsDay_CustomBoundaries_HandlesWrapAround()
    {
        // Nocturnal perception: Day from 20.0 to 6.0
        var nocturnal = new WorldTime(initialHour: 22.0f, dayLengthSeconds: 600.0f, dayStartHour: 20.0f, dayEndHour: 6.0f);

        Assert.True(nocturnal.IsDay);

        nocturnal.TimeOfDayHours = 2.0f;
        Assert.True(nocturnal.IsDay);

        nocturnal.TimeOfDayHours = 12.0f;
        Assert.False(nocturnal.IsDay);
    }

    [Fact]
    public void DayNightChanged_TriggersOnTransitions()
    {
        var time = new WorldTime(initialHour: 17.0f, dayLengthSeconds: 600.0f);
        var events = new List<bool>();

        time.DayNightChanged += isDay => events.Add(isDay);

        // Within day: 17.0 -> 17.5
        time.Advance(0.5f);
        Assert.Empty(events);

        // Across dusk: 17.5 -> 18.5 (IsDay becomes false)
        time.Advance(1.0f);
        Assert.Single(events);
        Assert.False(events[0]);

        // Within night: 18.5 -> 23.0
        time.Advance(4.5f);
        Assert.Single(events);

        // Across dawn: 23.0 -> 7.0 (IsDay becomes true)
        time.Advance(8.0f);
        Assert.Equal(2, events.Count);
        Assert.True(events[1]);
    }

    [Fact]
    public void TimeFreeze_ZeroDayLength_DoesNotAdvanceOnSeconds()
    {
        var time = new WorldTime(initialHour: 12.0f, dayLengthSeconds: 0.0f);

        time.AdvanceSeconds(100.0f);

        Assert.Equal(12.0f, time.TimeOfDayHours);
        Assert.True(time.IsDay);
    }
}
