using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

public sealed class NoteAndActionItemService(
    AppDbContext db, IClock clock, WriteGate gate, EventRecorder recorder, SlaService sla, IncidentService incidents)
{
    // ------------------------------------------------------------ notes

    public async Task<List<NoteDto>> ListNotesAsync(string rawId)
    {
        var id = Ids.Parse(rawId);
        if (!await db.Incidents.AnyAsync(i => i.Id == id)) throw ApiException.NotFound("Incident not found.");
        return (await db.Notes.AsNoTracking().Where(n => n.IncidentId == id).OrderBy(n => n.Id).ToListAsync()).Select(NoteDto.From).ToList();
    }

    public Task<NoteDto> AddNoteAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = IncidentService.RequireRole(actor, Operation.AddNote);
        var incident = await incidents.LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var text = f.String("body", 1, 2000);
        var expected = f.Integer("expectedVersion", required: false);
        f.ThrowIfInvalid();

        var now = clock.UtcNow;
        IncidentService.CheckVersion(incident.Version, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsEditing(incident.Status)) throw StateError(incident, "Adding a note");

        var note = new Note { IncidentId = incident.Id, AuthorId = actor.Id, Body = text!, CreatedAt = now };
        db.Notes.Add(note);
        incident.Touch(now);
        await db.SaveChangesAsync(); // assigns the note id
        recorder.Audit(incident.Id, actor.Id, AuditAction.NoteAdded, EventRecorder.Details(("noteId", note.Id)), now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoteDto.From(note);
    });

    // ------------------------------------------------------------ action items

    public async Task<List<ActionItemDto>> ListActionItemsAsync(string rawId)
    {
        var id = Ids.Parse(rawId);
        if (!await db.Incidents.AnyAsync(i => i.Id == id)) throw ApiException.NotFound("Incident not found.");
        return (await db.ActionItems.AsNoTracking().Where(a => a.IncidentId == id).OrderBy(a => a.Id).ToListAsync()).Select(ActionItemDto.From).ToList();
    }

    public Task<ActionItemDto> CreateActionItemAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = IncidentService.RequireRole(actor, Operation.WriteActionItem);
        var incident = await incidents.LoadAsync(rawId, actor, scope);
        var f = body.Parse();
        var title = f.String("title", 3, 200);
        var ownerId = f.Integer("ownerId");
        var dueDate = f.Date("dueDate");
        var expected = f.Integer("expectedVersion", required: false);
        var now = clock.UtcNow;
        await ValidateOwnerAsync(f, ownerId);
        if (dueDate is { } d && d < DateOnly.FromDateTime(now)) f.Add("dueDate", "out_of_range", "dueDate must not be in the past.");
        f.ThrowIfInvalid();

        IncidentService.CheckVersion(incident.Version, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsEditing(incident.Status)) throw StateError(incident, "Adding an action item");

        var item = new ActionItem
        {
            IncidentId = incident.Id, Title = title!, OwnerId = ownerId!.Value, DueDate = dueDate!.Value,
            Status = ActionItemStatus.Open, CreatedAt = now,
        };
        db.ActionItems.Add(item);
        incident.Touch(now);
        await db.SaveChangesAsync();
        recorder.Audit(incident.Id, actor.Id, AuditAction.ActionItemCreated, EventRecorder.Details(("actionItemId", item.Id)), now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ActionItemDto.From(item);
    });

    public Task<ActionItemDto> UpdateActionItemAsync(User actor, string rawId, string rawItemId, RequestBody body) => gate.RunAsync(async () =>
    {
        var scope = IncidentService.RequireRole(actor, Operation.WriteActionItem);
        var incident = await incidents.LoadAsync(rawId, actor, scope);
        var itemId = Ids.Parse(rawItemId);
        var item = await db.ActionItems.FirstOrDefaultAsync(a => a.Id == itemId && a.IncidentId == incident.Id)
            ?? throw ApiException.NotFound("Action item not found.");

        var f = body.Parse();
        var title = f.String("title", 3, 200, required: false);
        var ownerId = f.Integer("ownerId", required: false);
        var dueDate = f.Date("dueDate", required: false);
        var status = f.Enum<ActionItemStatus>("status", required: false);
        var expected = f.Integer("expectedVersion", required: false);
        if (!f.Present("title") && !f.Present("ownerId") && !f.Present("dueDate") && !f.Present("status"))
        {
            f.Add("title", "required", "At least one of title, ownerId, dueDate or status is required.");
        }
        var now = clock.UtcNow;
        await ValidateOwnerAsync(f, ownerId);
        if (dueDate is { } d && d != item.DueDate && d < DateOnly.FromDateTime(now)) f.Add("dueDate", "out_of_range", "dueDate must not be in the past.");
        f.ThrowIfInvalid();

        IncidentService.CheckVersion(incident.Version, expected);
        await using var tx = await db.Database.BeginTransactionAsync();
        await sla.ApplyEscalationsAsync(incident, now);
        if (!IncidentRules.AllowsEditing(incident.Status)) throw StateError(incident, "Updating an action item");

        var changed = false;
        if (title is not null && title != item.Title) { item.Title = title; changed = true; }
        if (ownerId is { } o && o != item.OwnerId) { item.OwnerId = o; changed = true; }
        if (dueDate is { } nd && nd != item.DueDate) { item.DueDate = nd; changed = true; }
        if (status is { } st && st != item.Status)
        {
            item.Status = st;
            item.CompletedAt = st == ActionItemStatus.Done ? now : null;
            changed = true;
        }
        if (changed)
        {
            incident.Touch(now);
            recorder.Audit(incident.Id, actor.Id, AuditAction.ActionItemUpdated, EventRecorder.Details(("actionItemId", item.Id)), now);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ActionItemDto.From(item);
    });

    private async Task ValidateOwnerAsync(Fields f, long? ownerId)
    {
        if (ownerId is not { } id) return;
        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (owner is null) f.Add("ownerId", "not_found", "User does not exist.");
        else if (owner.Role is not (Role.Responder or Role.Commander or Role.Admin)) f.Add("ownerId", "invalid_value", "Owner must be a responder, commander or admin.");
    }

    private static ApiException StateError(Incident incident, string what) =>
        ApiException.Conflict("INVALID_STATE", $"{what} is not allowed while the incident is {Wire.ToWire(incident.Status)}.",
            new Dictionary<string, object?> { ["currentStatus"] = Wire.ToWire(incident.Status) });
}
