namespace Shop.Domain.Common;

/// <summary>Iran has had no daylight saving since 2022: every "today"/"this day" in the admin is UTC+03:30, fixed.</summary>
public static class TehranTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromMinutes(210);

    /// <summary>The UTC instant of the start (midnight) of the Tehran-local day a UTC instant falls in.</summary>
    public static DateTime LocalMidnightUtc(DateTimeOffset utcNow) =>
        new DateTimeOffset(utcNow.ToOffset(Offset).Date, Offset).UtcDateTime;

    /// <summary>The Tehran-local calendar date (no time) a UTC instant falls in, for grouping by day.</summary>
    public static DateTime LocalDate(DateTime utc) => (utc + Offset).Date;
}
