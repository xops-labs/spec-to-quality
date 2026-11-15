namespace IncidentApp.Api.Infrastructure;

/// <summary>The only source of business time (spec section 7.4).</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed record ClockState(DateTime Now, string Mode);

/// <summary>System clock that can be frozen/advanced in test mode.</summary>
public sealed class ControllableClock : IClock
{
    private readonly object _lock = new();
    private DateTime? _fixed;

    public DateTime UtcNow
    {
        get
        {
            lock (_lock) return TruncateToMillis(_fixed ?? DateTime.UtcNow);
        }
    }

    public ClockState State
    {
        get
        {
            lock (_lock) return new ClockState(TruncateToMillis(_fixed ?? DateTime.UtcNow), _fixed is null ? "system" : "fixed");
        }
    }

    public void SetFixed(DateTime utc)
    {
        lock (_lock) _fixed = TruncateToMillis(utc);
    }

    public void Advance(TimeSpan by)
    {
        lock (_lock) _fixed = TruncateToMillis(_fixed ?? DateTime.UtcNow) + by;
    }

    public void UseSystemTime()
    {
        lock (_lock) _fixed = null;
    }

    public static DateTime TruncateToMillis(DateTime t)
    {
        var utc = t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
