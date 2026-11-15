namespace IncidentApp.Api.Domain;

public static class IncidentRules
{
    public static bool IsActive(IncidentStatus s) => s is IncidentStatus.Open or IncidentStatus.Acknowledged or IncidentStatus.Mitigated;

    public static string FormatNumber(long id) => "INC-" + id.ToString("D4");

    /// <summary>Returns the target status for an action, or null if the (status, action) pair is not a valid transition.</summary>
    public static IncidentStatus? NextStatus(IncidentStatus current, TransitionAction action) => (current, action) switch
    {
        (IncidentStatus.Open, TransitionAction.Acknowledge) => IncidentStatus.Acknowledged,
        (IncidentStatus.Acknowledged, TransitionAction.Mitigate) => IncidentStatus.Mitigated,
        (IncidentStatus.Mitigated, TransitionAction.Resolve) => IncidentStatus.Resolved,
        (IncidentStatus.Resolved, TransitionAction.Close) => IncidentStatus.Closed,
        (IncidentStatus.Resolved, TransitionAction.Reopen) => IncidentStatus.Acknowledged,
        _ => null,
    };

    /// <summary>Statuses in which assign / severity change are allowed.</summary>
    public static bool AllowsAssignmentAndSeverity(IncidentStatus s) => IsActive(s);

    public static bool AllowsEditing(IncidentStatus s) => s != IncidentStatus.Closed;

    public static string ActionName(TransitionAction a) => a.ToString().ToLowerInvariant();
}
