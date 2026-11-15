using System.Text.Json;
using IncidentApp.Api.Domain;

namespace IncidentApp.Api.Application;

public sealed record TeamDto(long Id, string Name);

public sealed record UserDto(long Id, string Username, string DisplayName, Role Role, long? TeamId)
{
    public static UserDto From(User u) => new(u.Id, u.Username, u.DisplayName, u.Role, u.TeamId);
}

public sealed record ServiceDto(long Id, string Key, string Name, long OwnerTeamId, int Tier, bool Active)
{
    public static ServiceDto From(Service s) => new(s.Id, s.Key, s.Name, s.OwnerTeamId, s.Tier, s.Active);
}

public sealed record IncidentDto(
    long Id, string Number, string Title, string Description, long ServiceId, string ServiceName, long TeamId,
    Severity Severity, IncidentStatus Status, long ReporterId, long? AssigneeId,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? AcknowledgedAt, DateTime? MitigatedAt, DateTime? ResolvedAt, DateTime? ClosedAt,
    string? ResolutionSummary, int EscalationLevel, int ReopenCount, int Version,
    int ActionItemCount, int OpenActionItemCount, SlaInfo Sla);

public sealed record NoteDto(long Id, long IncidentId, long AuthorId, string Body, DateTime CreatedAt)
{
    public static NoteDto From(Note n) => new(n.Id, n.IncidentId, n.AuthorId, n.Body, n.CreatedAt);
}

public sealed record ActionItemDto(long Id, long IncidentId, string Title, long OwnerId, string DueDate, ActionItemStatus Status, DateTime CreatedAt, DateTime? CompletedAt)
{
    public static ActionItemDto From(ActionItem a) => new(a.Id, a.IncidentId, a.Title, a.OwnerId, a.DueDate.ToString("yyyy-MM-dd"), a.Status, a.CreatedAt, a.CompletedAt);
}

public sealed record AuditEventDto(long Id, long? IncidentId, long? ActorId, AuditAction Action, DateTime CreatedAt, JsonElement Details)
{
    public static AuditEventDto From(AuditEvent e) => new(e.Id, e.IncidentId, e.ActorId, e.Action, e.CreatedAt, JsonDocument.Parse(e.DetailsJson).RootElement.Clone());
}

public sealed record NotificationDto(long Id, long RecipientId, NotificationType Type, long IncidentId, string Message, DateTime CreatedAt)
{
    public static NotificationDto From(Notification n) => new(n.Id, n.RecipientId, n.Type, n.IncidentId, n.Message, n.CreatedAt);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PagedResult<T> Of(IReadOnlyList<T> items, int page, int pageSize, int total) =>
        new(items, page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
}

public sealed record LoginResponse(string Token, UserDto User);

public sealed record ServiceCount(long ServiceId, string ServiceName, int ActiveCount);

public sealed record DashboardSummary(
    DateTime GeneratedAt, int TotalIncidents, int ActiveCount,
    Dictionary<string, int> ByStatus, Dictionary<string, int> ActiveBySeverity,
    int BreachedActiveCount, int AtRiskActiveCount, int EscalatedActiveCount,
    decimal? MttaMinutes, decimal? MttrMinutes, decimal? SlaCompliancePercent,
    IReadOnlyList<ServiceCount> TopServices);
