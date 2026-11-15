using IncidentApp.Api.Domain;

namespace IncidentApp.Tests.Domain;

public class IncidentRulesTests
{
    [Theory]
    [InlineData(IncidentStatus.Open, TransitionAction.Acknowledge, IncidentStatus.Acknowledged)]
    [InlineData(IncidentStatus.Acknowledged, TransitionAction.Mitigate, IncidentStatus.Mitigated)]
    [InlineData(IncidentStatus.Mitigated, TransitionAction.Resolve, IncidentStatus.Resolved)]
    [InlineData(IncidentStatus.Resolved, TransitionAction.Close, IncidentStatus.Closed)]
    [InlineData(IncidentStatus.Resolved, TransitionAction.Reopen, IncidentStatus.Acknowledged)]
    public void Valid_transitions(IncidentStatus from, TransitionAction action, IncidentStatus to) =>
        Assert.Equal(to, IncidentRules.NextStatus(from, action));

    [Fact]
    public void Exactly_five_transitions_exist()
    {
        var valid = from s in Enum.GetValues<IncidentStatus>()
                    from a in Enum.GetValues<TransitionAction>()
                    where IncidentRules.NextStatus(s, a) is not null
                    select (s, a);
        Assert.Equal(5, valid.Count());
    }

    [Fact]
    public void Acknowledged_cannot_resolve_directly() =>
        Assert.Null(IncidentRules.NextStatus(IncidentStatus.Acknowledged, TransitionAction.Resolve));

    [Theory]
    [InlineData(TransitionAction.Acknowledge)]
    [InlineData(TransitionAction.Mitigate)]
    [InlineData(TransitionAction.Resolve)]
    [InlineData(TransitionAction.Close)]
    [InlineData(TransitionAction.Reopen)]
    public void Closed_is_terminal(TransitionAction action) =>
        Assert.Null(IncidentRules.NextStatus(IncidentStatus.Closed, action));

    [Theory]
    [InlineData(1, "INC-0001")]
    [InlineData(42, "INC-0042")]
    [InlineData(9999, "INC-9999")]
    [InlineData(12345, "INC-12345")]
    public void Number_is_zero_padded_to_four_digits(long id, string expected) =>
        Assert.Equal(expected, IncidentRules.FormatNumber(id));

    [Fact]
    public void Touch_bumps_version_and_updated_at()
    {
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var incident = new Incident { Version = 1 };
        incident.Touch(t);
        Assert.Equal(2, incident.Version);
        Assert.Equal(t, incident.UpdatedAt);
    }
}
