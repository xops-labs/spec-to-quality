namespace IncidentApp.Api.Domain;

public class Team
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Role Role { get; set; }
    public long? TeamId { get; set; }
    public string PasswordHash { get; set; } = "";
}

public class Service
{
    public long Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public long OwnerTeamId { get; set; }
    public int Tier { get; set; }
    public bool Active { get; set; } = true;
}

public class Incident
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public long ServiceId { get; set; }
    public long TeamId { get; set; }
    public Severity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public long ReporterId { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? MitigatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ResolutionSummary { get; set; }
    public int EscalationLevel { get; set; }
    public int ReopenCount { get; set; }
    public int Version { get; set; } = 1;

    public bool IsActive => IncidentRules.IsActive(Status);

    /// <summary>Marks the incident row as changed: bumps updatedAt and version once.</summary>
    public void Touch(DateTime now)
    {
        UpdatedAt = now;
        Version++;
    }
}

public class Note
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public long AuthorId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ActionItem
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public string Title { get; set; } = "";
    public long OwnerId { get; set; }
    public DateOnly DueDate { get; set; }
    public ActionItemStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AuditEvent
{
    public long Id { get; set; }
    public long? IncidentId { get; set; }
    public long? ActorId { get; set; }
    public AuditAction Action { get; set; }
    public DateTime CreatedAt { get; set; }
    public string DetailsJson { get; set; } = "{}";
}

public class Notification
{
    public long Id { get; set; }
    public long RecipientId { get; set; }
    public NotificationType Type { get; set; }
    public long IncidentId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
