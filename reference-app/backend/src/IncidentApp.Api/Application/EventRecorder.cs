using System.Text.Json;
using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

/// <summary>Adds audit events and outbox notifications to the current unit of work (they are saved with the change).</summary>
public sealed class EventRecorder(AppDbContext db)
{
    public void Audit(long? incidentId, long? actorId, AuditAction action, IDictionary<string, object?> details, DateTime now)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            IncidentId = incidentId,
            ActorId = actorId,
            Action = action,
            CreatedAt = now,
            DetailsJson = JsonSerializer.Serialize(details),
        });
    }

    public Task<List<long>> CommanderIdsAsync(long teamId) =>
        db.Users.Where(u => u.Role == Role.Commander && u.TeamId == teamId).OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

    public Task<List<long>> AdminIdsAsync() =>
        db.Users.Where(u => u.Role == Role.Admin).OrderBy(u => u.Id).Select(u => u.Id).ToListAsync();

    /// <summary>Recipients are de-duplicated and the actor never notifies themself.</summary>
    public void Notify(NotificationType type, Incident incident, long? actorId, IEnumerable<long> recipients, string message, DateTime now)
    {
        foreach (var recipient in recipients.Distinct().Where(r => r != actorId))
        {
            db.Notifications.Add(new Notification
            {
                RecipientId = recipient, Type = type, IncidentId = incident.Id, Message = message, CreatedAt = now,
            });
        }
    }

    public static Dictionary<string, object?> Details(params (string Key, object? Value)[] items)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in items) d[k] = v is Enum e ? EnumToWire(e) : v;
        return d;
    }

    private static string EnumToWire(Enum e) => e switch
    {
        Severity s => Wire.ToWire(s),
        IncidentStatus s => Wire.ToWire(s),
        _ => e.ToString(),
    };
}
