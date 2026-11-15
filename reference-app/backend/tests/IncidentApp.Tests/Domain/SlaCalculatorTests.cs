using IncidentApp.Api.Domain;

namespace IncidentApp.Tests.Domain;

public class SlaCalculatorTests
{
    private static readonly DateTime Created = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Incident Make(Severity sev = Severity.Sev1, IncidentStatus status = IncidentStatus.Open) =>
        new() { Severity = sev, Status = status, CreatedAt = Created, UpdatedAt = Created };

    [Theory]
    [InlineData(Severity.Sev1, 5, 60)]
    [InlineData(Severity.Sev2, 15, 240)]
    [InlineData(Severity.Sev3, 60, 1440)]
    [InlineData(Severity.Sev4, 240, 4320)]
    public void Targets_match_spec(Severity sev, int ack, int resolve) =>
        Assert.Equal((ack, resolve), SlaCalculator.Targets(sev));

    [Fact]
    public void Fresh_incident_is_running()
    {
        var sla = SlaCalculator.Compute(Make(), Created);
        Assert.Equal(TimerStatus.Running, sla.Ack.Status);
        Assert.Equal(TimerStatus.Running, sla.Resolve.Status);
        Assert.False(sla.Breached);
        Assert.Equal(Created.AddMinutes(5), sla.Ack.Deadline);
    }

    [Fact]
    public void At_risk_starts_exactly_at_80_percent_of_target()
    {
        // SEV1 ack target 5 min -> 80% = 4 min exactly.
        Assert.Equal(TimerStatus.Running, SlaCalculator.Compute(Make(), Created.AddMinutes(4).AddMilliseconds(-1)).Ack.Status);
        Assert.Equal(TimerStatus.AtRisk, SlaCalculator.Compute(Make(), Created.AddMinutes(4)).Ack.Status);
    }

    [Fact]
    public void Deadline_itself_is_not_breached_but_one_millisecond_later_is()
    {
        Assert.Equal(TimerStatus.AtRisk, SlaCalculator.Compute(Make(), Created.AddMinutes(5)).Ack.Status);
        var late = SlaCalculator.Compute(Make(), Created.AddMinutes(5).AddMilliseconds(1));
        Assert.Equal(TimerStatus.Breached, late.Ack.Status);
        Assert.True(late.Breached);
    }

    [Fact]
    public void Acknowledging_exactly_at_the_deadline_is_met()
    {
        var incident = Make();
        incident.AcknowledgedAt = Created.AddMinutes(5);
        Assert.Equal(TimerStatus.Met, SlaCalculator.Compute(incident, Created.AddDays(3)).Ack.Status);
    }

    [Fact]
    public void Acknowledging_late_stays_breached_forever()
    {
        var incident = Make();
        incident.AcknowledgedAt = Created.AddMinutes(5).AddMilliseconds(1);
        var sla = SlaCalculator.Compute(incident, incident.AcknowledgedAt.Value);
        Assert.Equal(TimerStatus.Breached, sla.Ack.Status);
        Assert.True(sla.Breached);
    }

    [Fact]
    public void Reopened_incident_resolve_timer_runs_again_against_now()
    {
        var incident = Make(Severity.Sev1, IncidentStatus.Acknowledged);
        incident.AcknowledgedAt = Created.AddMinutes(1);
        incident.ResolvedAt = null; // cleared by reopen
        Assert.Equal(TimerStatus.Breached, SlaCalculator.Compute(incident, Created.AddMinutes(61)).Resolve.Status);
    }

    [Fact]
    public void No_escalation_before_ack_deadline() =>
        Assert.Empty(SlaCalculator.EscalationsDue(Make(), Created.AddMinutes(5)));

    [Fact]
    public void Open_incident_past_ack_deadline_reaches_level_1()
    {
        Assert.Equal([1], SlaCalculator.EscalationsDue(Make(), Created.AddMinutes(6)));
    }

    [Fact]
    public void Both_levels_are_applied_in_order_when_both_deadlines_passed()
    {
        Assert.Equal([1, 2], SlaCalculator.EscalationsDue(Make(), Created.AddMinutes(61)));
    }

    [Fact]
    public void Acknowledged_incident_can_jump_straight_to_level_2()
    {
        var incident = Make(Severity.Sev1, IncidentStatus.Acknowledged);
        Assert.Equal([2], SlaCalculator.EscalationsDue(incident, Created.AddMinutes(61)));
    }

    [Fact]
    public void Level_never_decreases_or_repeats()
    {
        var incident = Make();
        incident.EscalationLevel = 1;
        Assert.Equal([2], SlaCalculator.EscalationsDue(incident, Created.AddMinutes(61)));
        incident.EscalationLevel = 2;
        Assert.Empty(SlaCalculator.EscalationsDue(incident, Created.AddMinutes(600)));
    }

    [Theory]
    [InlineData(IncidentStatus.Resolved)]
    [InlineData(IncidentStatus.Closed)]
    public void Finished_incidents_are_never_newly_escalated(IncidentStatus status) =>
        Assert.Empty(SlaCalculator.EscalationsDue(Make(Severity.Sev1, status), Created.AddDays(30)));
}
