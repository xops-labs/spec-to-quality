using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

/// <summary>Incident use cases. Every mutating method follows the normative check order of spec 5.4.</summary>
public sealed class IncidentService(
    AppDbContext db, IClock clock, WriteGate gate, EventRecorder recorder, SlaService sla, IncidentMapper mapper)
{
    private static readonly IncidentStatus[] ActiveStatuses = [IncidentStatus.Open, IncidentStatus.Acknowledged, IncidentStatus.Mitigated];

    // ---------------------------------------------------------------- shared helpers

    /// <summary>Step 2: does this role ever have the operation?</summary>
    internal static Scope RequireRole(User actor, Operation op)
    {
        var scope = Permissions.ScopeFor(actor.Role, op);
        if (scope == Scope.Denied) throw ApiException.Forbidden();
        return scope;
    }

    /// <summary>Steps 3-4: look the incident up (tracked), then check the scope.</summary>
    internal async Task<Incident> LoadAsync(string rawId, User actor, Scope scope)
    {
        var id = Ids.Parse(rawId);
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id) ?? throw ApiException.NotFound("Incident not found.");
        if (!Permissions.InScope(scope, actor, incident)) throw ApiException.Forbidden();
        return incident;
    }

    internal static void CheckVersion(int loadedVersion, long? expectedVersion)
    {
        if (expectedVersion is not null && expectedVersion != loadedVersion)
        {
            throw ApiException.Conflict("VERSION_CONFLICT", "The incident was modified by someone else.",
                new Dictionary<string, object?> { ["currentVersion"] = loadedVersion });
        }
    }

    private static ApiException InvalidState(Incident incident, string what) =>
        ApiException.Conflict("INVALID_STATE", $"{what} is not allowed while the incident is {Wire.ToWire(incident.Status)}.",
            new Dictionary<string, object?> { ["currentStatus"] = Wire.ToWire(incident.Status) });

    private async Task<IncidentDto> SaveAndMapAsync(Incident incident, DateTime now, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
    {
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await mapper.MapOneAsync(incident, now);
    }

    private async Task<long?> FindDuplicateAsync(long serviceId, string title, long? excludeId)
    {
        var candidates = await db.Incidents.AsNoTracking()
            .Where(i => i.ServiceId == serviceId && ActiveStatuses.Contains(i.Status))
            .Select(i => new { i.Id, i.Title }).ToListAsync();
        var match = candidates.FirstOrDefault(c => c.Id != excludeId && string.Equals(c.Title.Trim(), title, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private static ApiException Duplicate(long existingId) =>
        ApiException.Conflict("DUPLICATE_INCIDENT", "An open incident with the same title already exists for this service.",
            new Dictionary<string, object?> { ["existingIncidentId"] = existingId });

    // ---------------------------------------------------------------- reads

    public async Task<IncidentDto> GetAsync(string rawId)
    {
        var id = Ids.Parse(rawId);
        var incident = await db.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id) ?? throw ApiException.NotFound("Incident not found.");
        return await mapper.MapOneAsync(incident, clock.UtcNow);
    }

    public async Task<PagedResult<IncidentDto>> ListAsync(QueryReader query)
    {
        var statuses = query.EnumList<IncidentStatus>("status");
        var severities = query.EnumList<Severity>("severity");
        var serviceId = query.PositiveInt("serviceId");
        var teamId = query.PositiveInt("teamId");
        var assigneeId = query.PositiveInt("assigneeId");
        var reporterId = query.PositiveInt("reporterId");
        var unassigned = query.Bool("unassigned");
        var text = query.Raw("q");
        var slaBreached = query.Bool("slaBreached");
        var active = query.Bool("active");
        var sort = query.OneOf("sort", "createdAt", "updatedAt", "severity") ?? "createdAt";
        var order = query.OneOf("order", "asc", "desc") ?? "desc";
        var (page, pageSize) = query.Paging();
        query.ThrowIfInvalid();

        IQueryable<Incident> q = db.Incidents.AsNoTracking();
        if (statuses is { Count: > 0 }) q = q.Where(i => statuses.Contains(i.Status));
        if (severities is { Count: > 0 }) q = q.Where(i => severities.Contains(i.Severity));
        if (serviceId is { } sid) q = q.Where(i => i.ServiceId == sid);
        if (teamId is { } tid) q = q.Where(i => i.TeamId == tid);
        if (assigneeId is { } aid) q = q.Where(i => i.AssigneeId == aid);
        if (reporterId is { } rid) q = q.Where(i => i.ReporterId == rid);
        if (unassigned == true) q = q.Where(i => i.AssigneeId == null);
        if (text is not null)
        {
            var t = text.ToLowerInvariant();
            q = q.Where(i => i.Title.ToLower().Contains(t) || i.Number.ToLower().Contains(t));
        }
        if (active is { } isActive) q = isActive ? q.Where(i => ActiveStatuses.Contains(i.Status)) : q.Where(i => !ActiveStatuses.Contains(i.Status));

        var desc = order == "desc";
        q = (sort, desc) switch
        {
            ("updatedAt", true) => q.OrderByDescending(i => i.UpdatedAt).ThenByDescending(i => i.Id),
            ("updatedAt", false) => q.OrderBy(i => i.UpdatedAt).ThenBy(i => i.Id),
            ("severity", true) => q.OrderByDescending(i => i.Severity).ThenByDescending(i => i.Id),
            ("severity", false) => q.OrderBy(i => i.Severity).ThenBy(i => i.Id),
            (_, true) => q.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id),
            _ => q.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id),
        };

        var now = clock.UtcNow;
        List<Incident> items;
        int total;
        if (slaBreached is { } wantBreached)
        {
            // The SLA state is computed from the clock, so this filter is applied in memory.
            var all = (await q.ToListAsync()).Where(i => SlaCalculator.Compute(i, now).Breached == wantBreached).ToList();
            total = all.Count;
            items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            total = await q.CountAsync();
            items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        return PagedResult<IncidentDto>.Of(await mapper.MapAsync(items, now), page, pageSize, total);
    }

    // ---------------------------------------------------------------- create

    public Task<IncidentDto> CreateAsync(User actor, RequestBody body) => gate.RunAsync(async () =>
    {
        RequireRole(actor, Operation.CreateIncident);
        var f = body.Parse();
        var title = f.String("title", 5, 120);
        var description = f.String("description", 0, 4000, required: false) ?? "";
        var serviceId = f.Integer("serviceId");
        var severity = f.Enum<Severity>("severity");
        Service? service = null;
        if (serviceId is { } sid)
        {
            service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid);
            if (service is null) f.Add("serviceId", "not_found", "Service does not exist.");
        }
        f.ThrowIfInvalid();

        if (!service!.Active) throw ApiException.BusinessRule("SERVICE_INACTIVE", "Incidents cannot be created for an inactive service.");
        if (severity == Severity.Sev1 && service.Tier > 2)
        {
            throw ApiException.BusinessRule("SEV1_REQUIRES_TIER_1_OR_2", "SEV1 is only allowed for services of tier 1 or 2.");
        }
        if (await FindDuplicateAsync(service.Id, title!, null) is { } existing) throw Duplicate(existing);

        var now = clock.UtcNow;
        var incident = new Incident
        {
            Title = title!, Description = description, ServiceId = service.Id, TeamId = service.OwnerTeamId,
            Severity = severity!.Value, Status = IncidentStatus.Open, ReporterId = actor.Id,
            CreatedAt = now, UpdatedAt = now, Version = 1,
        };
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(); // assigns the id
        incident.Number = IncidentRules.FormatNumber(incident.Id);
        recorder.Audit(incident.Id, actor.Id, AuditAction.IncidentCreated, EventRecorder.Details(("severity", Wire.ToWire(incident.Severity)), ("serviceId", incident.ServiceId)), now);
        recorder.Notify(NotificationType.IncidentCreated, incident, actor.Id, await recorder.CommanderIdsAsync(incident.TeamId),
            $"{incident.Number} was reported: {incident.Title}", now);
        return await SaveAndMapAsync(incident, now, tx);
    });

    // ---------------------------------------------------------------- edit

    public Task<IncidentDto> PatchAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = RequireRole(actor, Operation.EditIncident);
        var incident = await LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var title = f.String("title", 5, 120, required: false);
        var description = f.String("description", 0, 4000, required: false);
        var expected = f.Integer("expectedVersion", required: false);
        var hasTitle = f.Present("title");
        var hasDescription = f.Present("description");
        if (!hasTitle && !hasDescription) f.Add("title", "required", "At least one of title or description is required.");
        f.ThrowIfInvalid();

        var now = clock.UtcNow;
        var loadedVersion = incident.Version;
        CheckVersion(loadedVersion, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsEditing(incident.Status)) throw InvalidState(incident, "Editing");

        var changed = new List<string>();
        if (hasTitle && title != incident.Title) changed.Add("title");
        if (hasDescription && description != incident.Description) changed.Add("description");
        if (changed.Contains("title") && await FindDuplicateAsync(incident.ServiceId, title!, incident.Id) is { } existing) throw Duplicate(existing);

        if (changed.Count > 0)
        {
            if (changed.Contains("title")) incident.Title = title!;
            if (changed.Contains("description")) incident.Description = description!;
            incident.Touch(now);
            recorder.Audit(incident.Id, actor.Id, AuditAction.IncidentUpdated, EventRecorder.Details(("changedFields", changed)), now);
        }
        return await SaveAndMapAsync(incident, now, tx);
    });

    // ---------------------------------------------------------------- lifecycle transitions

    public Task<IncidentDto> TransitionAsync(User actor, string rawId, TransitionAction action, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = RequireRole(actor, Permissions.For(action));
        var incident = await LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var expected = f.Integer("expectedVersion", required: false);
        string? message = action switch
        {
            TransitionAction.Mitigate => f.String("message", 1, 2000),
            TransitionAction.Resolve => f.String("resolutionSummary", 10, 2000),
            TransitionAction.Reopen => f.String("reason", 5, 500),
            _ => null,
        };
        f.ThrowIfInvalid();

        var now = clock.UtcNow;
        var loadedVersion = incident.Version;
        CheckVersion(loadedVersion, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);

        var from = incident.Status;
        var to = IncidentRules.NextStatus(from, action) ?? throw ApiException.Conflict("INVALID_TRANSITION",
            $"Cannot {IncidentRules.ActionName(action)} an incident that is {Wire.ToWire(from)}.",
            new Dictionary<string, object?> { ["currentStatus"] = Wire.ToWire(from), ["action"] = IncidentRules.ActionName(action) });

        switch (action)
        {
            case TransitionAction.Acknowledge:
                Acknowledge(incident, actor, now);
                break;
            case TransitionAction.Mitigate:
                incident.MitigatedAt = now;
                db.Notes.Add(new Note { IncidentId = incident.Id, AuthorId = actor.Id, Body = message!, CreatedAt = now });
                break;
            case TransitionAction.Resolve:
                incident.ResolvedAt = now;
                incident.ResolutionSummary = message;
                break;
            case TransitionAction.Close:
                await CheckClosureRulesAsync(incident);
                incident.ClosedAt = now;
                break;
            case TransitionAction.Reopen:
                incident.ResolvedAt = null;
                incident.MitigatedAt = null;
                incident.ReopenCount++;
                db.Notes.Add(new Note { IncidentId = incident.Id, AuthorId = actor.Id, Body = message!, CreatedAt = now });
                break;
        }
        incident.Status = to;
        incident.Touch(now);

        var auditAction = action switch
        {
            TransitionAction.Acknowledge => AuditAction.Acknowledged,
            TransitionAction.Mitigate => AuditAction.Mitigated,
            TransitionAction.Resolve => AuditAction.Resolved,
            TransitionAction.Close => AuditAction.Closed,
            _ => AuditAction.Reopened,
        };
        recorder.Audit(incident.Id, actor.Id, auditAction, EventRecorder.Details(("from", Wire.ToWire(from)), ("to", Wire.ToWire(to))), now);
        if (action == TransitionAction.Resolve)
        {
            recorder.Notify(NotificationType.Resolved, incident, actor.Id, [incident.ReporterId], $"{incident.Number} was resolved.", now);
        }
        else if (action == TransitionAction.Close)
        {
            recorder.Notify(NotificationType.Closed, incident, actor.Id, [incident.ReporterId], $"{incident.Number} was closed.", now);
        }
        return await SaveAndMapAsync(incident, now, tx);
    });

    private void Acknowledge(Incident incident, User actor, DateTime now)
    {
        incident.AcknowledgedAt ??= now;
        if (incident.AssigneeId is not null) return;
        if (actor.Role is not (Role.Responder or Role.Commander))
        {
            throw ApiException.Conflict("ASSIGNEE_REQUIRED", "An assignee is required before an administrator can acknowledge.");
        }
        incident.AssigneeId = actor.Id;
        recorder.Audit(incident.Id, actor.Id, AuditAction.Assigned, EventRecorder.Details(("from", null), ("to", actor.Id)), now);
        recorder.Notify(NotificationType.Assigned, incident, actor.Id, [actor.Id], $"You were assigned {incident.Number}.", now);
    }

    private async Task CheckClosureRulesAsync(Incident incident)
    {
        var items = await db.ActionItems.AsNoTracking().Where(a => a.IncidentId == incident.Id).Select(a => a.Status).ToListAsync();
        if (items.Any(s => s != ActionItemStatus.Done))
        {
            throw ApiException.Conflict("OPEN_ACTION_ITEMS", "All action items must be done before closing.");
        }
        if (incident.Severity is Severity.Sev1 or Severity.Sev2 && items.Count == 0)
        {
            throw ApiException.Conflict("ACTION_ITEM_REQUIRED", "SEV1 and SEV2 incidents need at least one action item before closing.");
        }
    }

    // ---------------------------------------------------------------- assign / severity

    public Task<IncidentDto> AssignAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = RequireRole(actor, Operation.Assign);
        var incident = await LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var assigneeId = f.Integer("assigneeId");
        var expected = f.Integer("expectedVersion", required: false);
        User? assignee = null;
        if (assigneeId is { } aid)
        {
            assignee = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == aid);
            if (assignee is null) f.Add("assigneeId", "not_found", "User does not exist.");
            else if (assignee.Role is not (Role.Responder or Role.Commander) || assignee.TeamId != incident.TeamId)
            {
                f.Add("assigneeId", "invalid_value", "Assignee must be a responder or commander of the incident's team.");
            }
        }
        f.ThrowIfInvalid();

        var now = clock.UtcNow;
        CheckVersion(incident.Version, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsAssignmentAndSeverity(incident.Status)) throw InvalidState(incident, "Assigning");

        if (incident.AssigneeId != assignee!.Id)
        {
            var previous = incident.AssigneeId;
            incident.AssigneeId = assignee.Id;
            incident.Touch(now);
            recorder.Audit(incident.Id, actor.Id, AuditAction.Assigned, EventRecorder.Details(("from", previous), ("to", assignee.Id)), now);
            recorder.Notify(NotificationType.Assigned, incident, actor.Id, [assignee.Id], $"You were assigned {incident.Number}.", now);
        }
        return await SaveAndMapAsync(incident, now, tx);
    });

    public Task<IncidentDto> ChangeSeverityAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = RequireRole(actor, Operation.ChangeSeverity);
        var incident = await LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var severity = f.Enum<Severity>("severity");
        var reason = f.String("reason", 5, 500);
        var expected = f.Integer("expectedVersion", required: false);
        if (severity == incident.Severity) f.Add("severity", "invalid_value", "The incident already has this severity.");
        f.ThrowIfInvalid();

        var service = await db.Services.AsNoTracking().FirstAsync(s => s.Id == incident.ServiceId);
        if (severity == Severity.Sev1 && service.Tier > 2)
        {
            throw ApiException.BusinessRule("SEV1_REQUIRES_TIER_1_OR_2", "SEV1 is only allowed for services of tier 1 or 2.");
        }

        var now = clock.UtcNow;
        CheckVersion(incident.Version, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsAssignmentAndSeverity(incident.Status)) throw InvalidState(incident, "Changing severity");

        var previous = incident.Severity;
        incident.Severity = severity!.Value;
        incident.Touch(now);
        db.Notes.Add(new Note { IncidentId = incident.Id, AuthorId = actor.Id, Body = reason!, CreatedAt = now });
        recorder.Audit(incident.Id, actor.Id, AuditAction.SeverityChanged,
            EventRecorder.Details(("from", Wire.ToWire(previous)), ("to", Wire.ToWire(incident.Severity)), ("reason", reason)), now);
        return await SaveAndMapAsync(incident, now, tx);
    });
}
