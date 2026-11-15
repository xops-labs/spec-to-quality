using System.Globalization;
using IncidentApp.Api.Infrastructure;

namespace IncidentApp.Api.Application;

/// <summary>Test-mode-only clock control and database reset (spec 7.5).</summary>
public sealed class TestSupportService(AppDbContext db, ControllableClock clock, WriteGate gate, SlaService sla)
{
    public ClockState GetClock() => clock.State;

    public Task<ClockState> SetClockAsync(RequestBody body) => gate.RunAsync(async () =>
    {
        var f = body.Parse();
        var text = f.String("now", 1, 100);
        DateTime parsed = default;
        if (text is not null && !(HasZone(text) && DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed)))
        {
            f.Add("now", "invalid_format", "now must be an ISO-8601 UTC timestamp.");
        }
        f.ThrowIfInvalid();
        clock.SetFixed(parsed);
        await sla.EvaluateAllCoreAsync(clock.UtcNow);
        return clock.State;
    });

    public Task<ClockState> AdvanceClockAsync(RequestBody body) => gate.RunAsync(async () =>
    {
        var f = body.Parse();
        var minutes = f.Integer("minutes", required: false, min: 0, max: 5_256_000);
        var seconds = f.Integer("seconds", required: false, min: 0, max: 315_360_000);
        if (!f.Present("minutes") && !f.Present("seconds")) f.Add("minutes", "required", "minutes or seconds is required.");
        f.ThrowIfInvalid();
        clock.Advance(TimeSpan.FromMinutes(minutes ?? 0) + TimeSpan.FromSeconds(seconds ?? 0));
        await sla.EvaluateAllCoreAsync(clock.UtcNow);
        return clock.State;
    });

    public Task ResetAsync() => gate.RunAsync(async () =>
    {
        await DatabaseInitializer.ResetAsync(db);
        clock.UseSystemTime();
    });

    // A timestamp must state its offset (Z or +hh:mm); a bare local time is ambiguous.
    private static bool HasZone(string s) => s.EndsWith('Z') || s.EndsWith('z') || s.LastIndexOfAny(['+', '-']) > 10;
}
