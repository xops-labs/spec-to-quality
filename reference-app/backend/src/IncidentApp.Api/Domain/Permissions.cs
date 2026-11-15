namespace IncidentApp.Api.Domain;

public enum Operation
{
    CreateIncident, EditIncident, AddNote, Acknowledge, Mitigate, Resolve, Close, Reopen, Assign, ChangeSeverity,
    WriteActionItem, ReadIncidentAudit, ReadGlobalAudit, ReadAllNotifications, WriteService, EvaluateSla,
}

/// <summary>How far an operation reaches for a role (spec section 5.3).</summary>
public enum Scope { Denied, Any, Own, OwnWhileOpen, Team, TeamAssignee }

public static class Permissions
{
    public static Operation For(TransitionAction a) => a switch
    {
        TransitionAction.Acknowledge => Operation.Acknowledge,
        TransitionAction.Mitigate => Operation.Mitigate,
        TransitionAction.Resolve => Operation.Resolve,
        TransitionAction.Close => Operation.Close,
        TransitionAction.Reopen => Operation.Reopen,
        _ => throw new ArgumentOutOfRangeException(nameof(a)),
    };

    public static Scope ScopeFor(Role role, Operation op)
    {
        if (role == Role.Admin) return Scope.Any;
        return op switch
        {
            Operation.CreateIncident => role >= Role.Reporter ? Scope.Any : Scope.Denied,
            Operation.EditIncident => role switch { Role.Reporter => Scope.OwnWhileOpen, Role.Commander => Scope.Team, _ => Scope.Denied },
            Operation.AddNote => role switch { Role.Reporter => Scope.Own, Role.Responder or Role.Commander => Scope.Team, _ => Scope.Denied },
            Operation.Acknowledge or Operation.WriteActionItem or Operation.ReadIncidentAudit =>
                role is Role.Responder or Role.Commander ? Scope.Team : Scope.Denied,
            Operation.Mitigate or Operation.Resolve => role switch { Role.Responder => Scope.TeamAssignee, Role.Commander => Scope.Team, _ => Scope.Denied },
            Operation.Close or Operation.Reopen or Operation.Assign or Operation.ChangeSeverity =>
                role == Role.Commander ? Scope.Team : Scope.Denied,
            _ => Scope.Denied, // admin-only operations
        };
    }

    public static bool InScope(Scope scope, User user, Incident incident) => scope switch
    {
        Scope.Any => true,
        Scope.Own => incident.ReporterId == user.Id,
        Scope.OwnWhileOpen => incident.ReporterId == user.Id && incident.Status == IncidentStatus.Open,
        Scope.Team => user.TeamId is not null && user.TeamId == incident.TeamId,
        Scope.TeamAssignee => user.TeamId is not null && user.TeamId == incident.TeamId && incident.AssigneeId == user.Id,
        _ => false,
    };
}
