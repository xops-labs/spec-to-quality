using IncidentApp.Api.Domain;

namespace IncidentApp.Tests.Domain;

public class PermissionsTests
{
    [Theory]
    [InlineData(Role.Viewer, Operation.CreateIncident, Scope.Denied)]
    [InlineData(Role.Reporter, Operation.CreateIncident, Scope.Any)]
    [InlineData(Role.Reporter, Operation.EditIncident, Scope.OwnWhileOpen)]
    [InlineData(Role.Responder, Operation.EditIncident, Scope.Denied)]
    [InlineData(Role.Commander, Operation.EditIncident, Scope.Team)]
    [InlineData(Role.Reporter, Operation.AddNote, Scope.Own)]
    [InlineData(Role.Responder, Operation.Acknowledge, Scope.Team)]
    [InlineData(Role.Reporter, Operation.Acknowledge, Scope.Denied)]
    [InlineData(Role.Responder, Operation.Mitigate, Scope.TeamAssignee)]
    [InlineData(Role.Responder, Operation.Resolve, Scope.TeamAssignee)]
    [InlineData(Role.Commander, Operation.Resolve, Scope.Team)]
    [InlineData(Role.Responder, Operation.Close, Scope.Denied)]
    [InlineData(Role.Commander, Operation.Close, Scope.Team)]
    [InlineData(Role.Responder, Operation.Assign, Scope.Denied)]
    [InlineData(Role.Commander, Operation.ChangeSeverity, Scope.Team)]
    [InlineData(Role.Responder, Operation.ReadIncidentAudit, Scope.Team)]
    [InlineData(Role.Reporter, Operation.ReadIncidentAudit, Scope.Denied)]
    [InlineData(Role.Commander, Operation.ReadGlobalAudit, Scope.Denied)]
    [InlineData(Role.Commander, Operation.WriteService, Scope.Denied)]
    [InlineData(Role.Responder, Operation.WriteActionItem, Scope.Team)]
    [InlineData(Role.Viewer, Operation.WriteActionItem, Scope.Denied)]
    public void Matrix_cells(Role role, Operation op, Scope expected) =>
        Assert.Equal(expected, Permissions.ScopeFor(role, op));

    [Theory]
    [InlineData(Operation.CreateIncident)]
    [InlineData(Operation.EditIncident)]
    [InlineData(Operation.Close)]
    [InlineData(Operation.ReadGlobalAudit)]
    [InlineData(Operation.EvaluateSla)]
    [InlineData(Operation.WriteService)]
    public void Admin_may_do_everything_anywhere(Operation op) =>
        Assert.Equal(Scope.Any, Permissions.ScopeFor(Role.Admin, op));

    [Fact]
    public void Viewer_has_no_write_permission_at_all()
    {
        foreach (var op in Enum.GetValues<Operation>())
        {
            Assert.Equal(Scope.Denied, Permissions.ScopeFor(Role.Viewer, op));
        }
    }

    private static Incident Inc(long team = 1, long reporter = 9, long? assignee = null, IncidentStatus status = IncidentStatus.Open) =>
        new() { TeamId = team, ReporterId = reporter, AssigneeId = assignee, Status = status };

    [Fact]
    public void Team_scope_requires_matching_team()
    {
        var resp = new User { Id = 5, Role = Role.Responder, TeamId = 1 };
        Assert.True(Permissions.InScope(Scope.Team, resp, Inc(team: 1)));
        Assert.False(Permissions.InScope(Scope.Team, resp, Inc(team: 2)));
        Assert.False(Permissions.InScope(Scope.Team, new User { Id = 9, Role = Role.Reporter, TeamId = null }, Inc()));
    }

    [Fact]
    public void Assignee_scope_requires_team_and_assignment()
    {
        var resp = new User { Id = 5, Role = Role.Responder, TeamId = 1 };
        Assert.False(Permissions.InScope(Scope.TeamAssignee, resp, Inc(assignee: null)));
        Assert.False(Permissions.InScope(Scope.TeamAssignee, resp, Inc(assignee: 6)));
        Assert.True(Permissions.InScope(Scope.TeamAssignee, resp, Inc(assignee: 5)));
    }

    [Fact]
    public void Own_scope_and_own_while_open()
    {
        var reporter = new User { Id = 9, Role = Role.Reporter };
        Assert.True(Permissions.InScope(Scope.Own, reporter, Inc(reporter: 9)));
        Assert.False(Permissions.InScope(Scope.Own, reporter, Inc(reporter: 3)));
        Assert.True(Permissions.InScope(Scope.OwnWhileOpen, reporter, Inc(reporter: 9)));
        Assert.False(Permissions.InScope(Scope.OwnWhileOpen, reporter, Inc(reporter: 9, status: IncidentStatus.Acknowledged)));
    }
}
