namespace IncidentApp.Api.Domain;

public sealed record TimerSla(int TargetMinutes, DateTime Deadline, TimerStatus Status);

public sealed record SlaInfo(TimerSla Ack, TimerSla Resolve)
{
    public bool Breached => Ack.Status == TimerStatus.Breached || Resolve.Status == TimerStatus.Breached;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool AtRisk => Ack.Status == TimerStatus.AtRisk || Resolve.Status == TimerStatus.AtRisk;
}

/// <summary>Pure SLA and escalation rules (spec section 7). All time comes from the caller.</summary>
public static class SlaCalculator
{
    public static (int Ack, int Resolve) Targets(Severity severity) => severity switch
    {
        Severity.Sev1 => (5, 60),
        Severity.Sev2 => (15, 240),
        Severity.Sev3 => (60, 1440),
        Severity.Sev4 => (240, 4320),
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    public static SlaInfo Compute(Incident incident, DateTime now)
    {
        var (ackTarget, resolveTarget) = Targets(incident.Severity);
        return new SlaInfo(
            Timer(incident.CreatedAt, ackTarget, incident.AcknowledgedAt, now),
            Timer(incident.CreatedAt, resolveTarget, incident.ResolvedAt, now));
    }

    public static TimerSla Timer(DateTime createdAt, int targetMinutes, DateTime? achievedAt, DateTime now)
    {
        var deadline = createdAt.AddMinutes(targetMinutes);
        // 0.8 x target expressed exactly in milliseconds (targetMinutes * 60000 * 0.8).
        var atRiskAt = createdAt.AddMilliseconds(targetMinutes * 48_000L);
        TimerStatus status;
        if (achievedAt is { } t) status = t <= deadline ? TimerStatus.Met : TimerStatus.Breached;
        else if (now > deadline) status = TimerStatus.Breached;
        else if (now >= atRiskAt) status = TimerStatus.AtRisk;
        else status = TimerStatus.Running;
        return new TimerSla(targetMinutes, deadline, status);
    }

    /// <summary>Escalation levels that an evaluation at <paramref name="now"/> would newly apply, in order (level 1 before level 2).</summary>
    public static IReadOnlyList<int> EscalationsDue(Incident incident, DateTime now)
    {
        var (ackTarget, resolveTarget) = Targets(incident.Severity);
        var levels = new List<int>();
        var level = incident.EscalationLevel;
        if (incident.Status == IncidentStatus.Open && now > incident.CreatedAt.AddMinutes(ackTarget) && level < 1)
        {
            levels.Add(1);
            level = 1;
        }
        if (incident.IsActive && now > incident.CreatedAt.AddMinutes(resolveTarget) && level < 2)
        {
            levels.Add(2);
        }
        return levels;
    }
}
