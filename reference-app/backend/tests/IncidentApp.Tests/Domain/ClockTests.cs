using IncidentApp.Api.Infrastructure;

namespace IncidentApp.Tests.Domain;

public class ClockTests
{
    [Fact]
    public void System_mode_follows_real_time_truncated_to_milliseconds()
    {
        var clock = new ControllableClock();
        Assert.Equal("system", clock.State.Mode);
        Assert.Equal(0, clock.UtcNow.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Fact]
    public void Fixed_mode_stops_time_and_advance_moves_it()
    {
        var clock = new ControllableClock();
        var t = new DateTime(2026, 3, 1, 12, 0, 0, 123, DateTimeKind.Utc);
        clock.SetFixed(t);
        Assert.Equal(t, clock.UtcNow);
        Assert.Equal("fixed", clock.State.Mode);
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30));
        Assert.Equal(t.AddSeconds(330), clock.UtcNow);
    }

    [Fact]
    public void Advance_from_system_mode_fixes_the_clock()
    {
        var clock = new ControllableClock();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("fixed", clock.State.Mode);
    }

    [Fact]
    public void UseSystemTime_returns_to_system_mode()
    {
        var clock = new ControllableClock();
        clock.SetFixed(DateTime.UtcNow);
        clock.UseSystemTime();
        Assert.Equal("system", clock.State.Mode);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_is_salted_and_verifiable()
    {
        var a = PasswordHasher.Hash("secret-1");
        var b = PasswordHasher.Hash("secret-1");
        Assert.NotEqual(a, b);
        Assert.DoesNotContain("secret-1", a);
        Assert.True(PasswordHasher.Verify("secret-1", a));
        Assert.False(PasswordHasher.Verify("secret-2", a));
        Assert.False(PasswordHasher.Verify("secret-1", "garbage"));
    }
}
