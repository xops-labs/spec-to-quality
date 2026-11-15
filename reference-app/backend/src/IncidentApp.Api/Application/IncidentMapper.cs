using IncidentApp.Api.Domain;
using Microsoft.EntityFrameworkCore;
using IncidentApp.Api.Infrastructure;

namespace IncidentApp.Api.Application;

public sealed class IncidentMapper(AppDbContext db)
{
    public async Task<List<IncidentDto>> MapAsync(IReadOnlyCollection<Incident> incidents, DateTime now)
    {
        var ids = incidents.Select(i => i.Id).ToList();
        var serviceIds = incidents.Select(i => i.ServiceId).Distinct().ToList();
        var serviceNames = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
        var counts = await db.ActionItems.AsNoTracking().Where(a => ids.Contains(a.IncidentId))
            .GroupBy(a => a.IncidentId)
            .Select(g => new { g.Key, Total = g.Count(), Open = g.Count(a => a.Status == ActionItemStatus.Open) })
            .ToDictionaryAsync(x => x.Key);

        return incidents.Select(i =>
        {
            counts.TryGetValue(i.Id, out var c);
            return new IncidentDto(
                i.Id, i.Number, i.Title, i.Description, i.ServiceId, serviceNames.GetValueOrDefault(i.ServiceId, ""), i.TeamId,
                i.Severity, i.Status, i.ReporterId, i.AssigneeId,
                i.CreatedAt, i.UpdatedAt, i.AcknowledgedAt, i.MitigatedAt, i.ResolvedAt, i.ClosedAt,
                i.ResolutionSummary, i.EscalationLevel, i.ReopenCount, i.Version,
                c?.Total ?? 0, c?.Open ?? 0, SlaCalculator.Compute(i, now));
        }).ToList();
    }

    public async Task<IncidentDto> MapOneAsync(Incident incident, DateTime now) => (await MapAsync([incident], now))[0];
}
