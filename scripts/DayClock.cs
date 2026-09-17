using Godot;
using System;

namespace GameSet1.Time;

public enum Weather { Sunny, Cloudy, Rainy }

/// <summary>August 2000 calendar. Advancing time never ends a day or wraps the month.</summary>
public sealed class DayClock
{
    private static readonly string[] Weekdays = { "日", "月", "火", "水", "木", "金", "土" };
    private double _secondsPerHour = 20.0;
    public int Day { get; private set; } = 1;
    public double Hour { get; private set; } = 8.0;
    public double SecondsPerHour
    {
        get => _secondsPerHour;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "SecondsPerHour must be finite and positive.");
            _secondsPerHour = value;
        }
    }

    public Weather Today
    {
        get
        {
            int weatherDay = Day == 16 ? 17 : Day == 17 ? 16 : Day;
            int h = (weatherDay * 37 + 11) % 100;
            return h < 18 ? Weather.Rainy : h < 40 ? Weather.Cloudy : Weather.Sunny;
        }
    }

    public string Weekday => Weekdays[(Day + 1) % 7];
    public Vector3 SunRotationDegrees
    {
        get
        {
            float t = Mathf.Clamp((float)((Hour - 6.0) / 13.0), 0f, 1f);
            float arc = Mathf.Sin(t * Mathf.Pi);
            return new Vector3(-10f - 50f * arc, -150f + 120f * t, 0f);
        }
    }
    public float Warm => Mathf.Pow(Mathf.Clamp((float)((Hour - 14.0) / 5.0), 0f, 1f), 1.3f);

    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0)
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be finite and nonnegative.");
        Hour = Math.Min(19.0, Hour + delta / SecondsPerHour);
    }

    public void NextDay()
    {
        if (Day == 31)
            return; // Caller owns the ending; preserve the final day and hour.
        Day++;
        Hour = 8.0;
    }

    public static void SelfTest()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"[dayclock] {message}");
        }
        var clock = new DayClock();
        int rainyCount = 0;
        for (int day = 1; day <= 31; day++)
        {
            Check(clock.Day == day && clock.Hour == 8.0, $"day {day}: morning");
            Check(clock.Weekday == Weekdays[(int)new DateTime(2000, 8, day).DayOfWeek], $"day {day}: weekday");
            bool rainy = day is 8 or 17 or 19 or 27;
            Check((clock.Today == Weather.Rainy) == rainy, $"day {day}: rain");
            if (clock.Today == Weather.Rainy) rainyCount++;
            if (day == 16) Check(clock.Today == Weather.Sunny, "8/16 must be sunny");
            clock.Advance(220.0);
            Check(clock.Hour == 19.0 && clock.Day == day, "19:00 clamp without automatic day change");
            Check(clock.Warm == 1.0f, "evening warmth");
            clock.NextDay();
        }
        Check(rainyCount == 4 && clock.Day == 31 && clock.Hour == 19.0, "month end");
        GD.Print("[dayclock] ok");
    }
}
