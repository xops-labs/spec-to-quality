namespace IncidentApp.Api.Domain;

public enum Role { Viewer, Reporter, Responder, Commander, Admin }

public enum Severity { Sev1, Sev2, Sev3, Sev4 }

public enum IncidentStatus { Open, Acknowledged, Mitigated, Resolved, Closed }

public enum ActionItemStatus { Open, Done }

public enum TimerStatus { Running, AtRisk, Breached, Met }

public enum AuditAction
{
    IncidentCreated, IncidentUpdated, Acknowledged, Mitigated, Resolved, Closed, Reopened,
    Assigned, SeverityChanged, NoteAdded, ActionItemCreated, ActionItemUpdated, Escalated,
    ServiceCreated, ServiceUpdated
}

public enum NotificationType { IncidentCreated, Assigned, EscalationL1, EscalationL2, Resolved, Closed }

/// <summary>Lifecycle actions that move an incident between statuses.</summary>
public enum TransitionAction { Acknowledge, Mitigate, Resolve, Close, Reopen }
