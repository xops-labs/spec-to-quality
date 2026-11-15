using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

/// <summary>EvaluateSla(now): idempotent escalation of incidents (spec 7.3).</summary>
public sealed class SlaService(AppDbContext db, EventRecorder recorder, IClock clock, WriteGate gate)
{
    /// <summary>Applies due escalations to one tracked incident (caller saves). Returns how many levels were applied.</summary>
    public async Task<int> ApplyEscalationsAsync(Incident incident, DateTime now)
    {
        var levels = SlaCalculator.EscalationsDue(incident, now);
        if (levels.Count == 0) return 0;

        var commanders = await recorder.CommanderIdsAsync(incident.TeamId);
        foreach (var level in levels)
        {
            incident.EscalationLevel = level;
            recorder.Audit(incident.Id, null, AuditAction.Escalated, EventRecorder.Details(("level", level)), now);
            var message = $"{incident.Number} escalated to level {level}: {incident.Title}";
            if (level == 1)
            {
                recorder.Notify(NotificationType.EscalationL1, incident, null, commanders, message, now);
            }
            else
            {
                recorder.Notify(NotificationType.EscalationL2, incident, null, commanders.Concat(await recorder.AdminIdsAsync()), message, now);
            }
        }
        incident.Touch(now);
        return levels.Count;
    }

    /// <summary>Evaluates every active incident at the clock's current time, serialised with other writers.</summary>
    public Task<int> EvaluateAllAsync() => gate.RunAsync(() => EvaluateAllCoreAsync(clock.UtcNow));

    /// <summary>Must be called while holding the write gate.</summary>
    public async Task<int> EvaluateAllCoreAsync(DateTime now)
    {
        var active = new[] { IncidentStatus.Open, IncidentStatus.Acknowledged, IncidentStatus.Mitigated };
        var incidents = await db.Incidents.Where(i => active.Contains(i.Status)).OrderBy(i => i.Id).ToListAsync();
        var applied = 0;
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var incident in incidents) applied += await ApplyEscalationsAsync(incident, now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        db.ChangeTracker.Clear();
        return applied;
    }
}

/// <summary>Real-time background evaluation (production behaviour; disabled in test mode).</summary>
public sealed class SlaBackgroundService(IServiceScopeFactory scopes, ILogger<SlaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SlaService>().EvaluateAllAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Periodic SLA evaluation failed.");
            }
        }
    }
}
