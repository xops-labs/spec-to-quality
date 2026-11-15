using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

/// <summary>Read-only queries: reference data, audit trail, notifications and the dashboard.</summary>
public sealed class ReadServices(AppDbContext db, IClock clock, IncidentService incidents)
{
    public async Task<List<TeamDto>> TeamsAsync() =>
        await db.Teams.AsNoTracking().OrderBy(t => t.Id).Select(t => new TeamDto(t.Id, t.Name)).ToListAsync();

    public async Task<List<UserDto>> UsersAsync() =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync()).Select(UserDto.From).ToList();

    // ------------------------------------------------------------ audit

    public async Task<List<AuditEventDto>> IncidentAuditAsync(User actor, string rawId)
    {
        var scope = IncidentService.RequireRole(actor, Operation.ReadIncidentAudit);
        var incident = await incidents.LoadAsync(rawId, actor, scope);
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.IncidentId == incident.Id).OrderBy(e => e.Id).ToListAsync();
        return events.Select(AuditEventDto.From).ToList();
    }

    public async Task<PagedResult<AuditEventDto>> GlobalAuditAsync(User actor, QueryReader query)
    {
        IncidentService.RequireRole(actor, Operation.ReadGlobalAudit);
        var incidentId = query.PositiveInt("incidentId");
        var action = query.Enum<AuditAction>("action");
        var (page, pageSize) = query.Paging();
        query.ThrowIfInvalid();

        IQueryable<AuditEvent> q = db.AuditEvents.AsNoTracking();
        if (incidentId is { } iid) q = q.Where(e => e.IncidentId == iid);
        if (action is { } a) q = q.Where(e => e.Action == a);
        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<AuditEventDto>.Of(items.Select(AuditEventDto.From).ToList(), page, pageSize, total);
    }

    // ------------------------------------------------------------ notifications

    public async Task<PagedResult<NotificationDto>> NotificationsAsync(User actor, QueryReader query)
    {
        var scope = query.OneOf("scope", "all");
        var (page, pageSize) = query.Paging();
        query.ThrowIfInvalid();
        if (scope == "all") IncidentService.RequireRole(actor, Operation.ReadAllNotifications);

        IQueryable<Notification> q = db.Notifications.AsNoTracking();
        if (scope != "all") q = q.Where(n => n.RecipientId == actor.Id);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<NotificationDto>.Of(items.Select(NotificationDto.From).ToList(), page, pageSize, total);
    }

    // ------------------------------------------------------------ dashboard

    public async Task<DashboardSummary> DashboardAsync()
    {
        var now = clock.UtcNow;
        var all = await db.Incidents.AsNoTracking().ToListAsync();
        var active = all.Where(i => i.IsActive).ToList();
        var slaOf = all.ToDictionary(i => i.Id, i => SlaCalculator.Compute(i, now));

        var byStatus = Enum.GetValues<IncidentStatus>().ToDictionary(s => Wire.ToWire(s), s => all.Count(i => i.Status == s));
        var bySeverity = Enum.GetValues<Severity>().ToDictionary(s => Wire.ToWire(s), s => active.Count(i => i.Severity == s));

        var acked = all.Where(i => i.AcknowledgedAt is not null).Select(i => (i.AcknowledgedAt!.Value - i.CreatedAt).TotalMilliseconds).ToList();
        var resolved = all.Where(i => i.ResolvedAt is not null).Select(i => (i.ResolvedAt!.Value - i.CreatedAt).TotalMilliseconds).ToList();
        var finished = all.Where(i => i.Status is IncidentStatus.Resolved or IncidentStatus.Closed).ToList();

        var serviceNames = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);
        var top = active.GroupBy(i => i.ServiceId)
            .Select(g => new ServiceCount(g.Key, serviceNames.GetValueOrDefault(g.Key, ""), g.Count()))
            .OrderByDescending(s => s.ActiveCount).ThenBy(s => s.ServiceName, StringComparer.Ordinal)
            .Take(5).ToList();

        return new DashboardSummary(
            now, all.Count, active.Count, byStatus, bySeverity,
            active.Count(i => slaOf[i.Id].Breached),
            active.Count(i => !slaOf[i.Id].Breached && slaOf[i.Id].AtRisk),
            active.Count(i => i.EscalationLevel >= 1),
            MeanMinutes(acked), MeanMinutes(resolved),
            finished.Count == 0 ? null : Round1(100m * finished.Count(i => !slaOf[i.Id].Breached) / finished.Count),
            top);
    }

    private static decimal? MeanMinutes(List<double> millis) =>
        millis.Count == 0 ? null : Round1((decimal)millis.Sum() / millis.Count / 60_000m);

    private static decimal Round1(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
