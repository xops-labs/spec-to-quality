using System.Net;
using System.Text.Json.Nodes;

namespace IncidentApp.Tests.Api;

public class SlaAndDashboardTests(ApiFixture api) : ApiTestBase(api)
{
    private async Task<JsonNode> Fetch(JsonNode incident) => (await (await Api.As("viewer1")).Get(Url(incident))).Json;

    private static string AckStatus(JsonNode i) => i["sla"]!["ack"]!["status"]!.GetValue<string>();
    private static string ResolveStatus(JsonNode i) => i["sla"]!["resolve"]!["status"]!.GetValue<string>();

    [Fact]
    public async Task Sla_status_follows_the_injected_clock_at_exact_boundaries()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Boundary check", "SEV1"); // ack 5 min, resolve 60 min
        Assert.Equal("RUNNING", AckStatus(await Fetch(incident)));
        await Api.AdvanceAsync(4); // 80% of 5 min
        Assert.Equal("AT_RISK", AckStatus(await Fetch(incident)));
        await Api.AdvanceAsync(1); // exactly at the deadline
        var atDeadline = await Fetch(incident);
        Assert.Equal("AT_RISK", AckStatus(atDeadline));
        Assert.False(atDeadline["sla"]!["breached"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK, (await new Client(Api.Anonymous()).Post("/api/v1/test/clock/advance", new { seconds = 1 })).Status);
        var late = await Fetch(incident);
        Assert.Equal("BREACHED", AckStatus(late));
        Assert.True(late["sla"]!["breached"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Acknowledging_exactly_at_the_deadline_is_met()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Ack on time", "SEV1");
        await Api.AdvanceAsync(5);
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/acknowledge"));
        Assert.Equal("MET", AckStatus(res.Json));
    }

    [Fact]
    public async Task Acknowledging_late_is_breached_and_the_resolve_timer_reflects_reopen()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Late ack", "SEV1");
        await Api.AdvanceAsync(6);
        var cmd = await Api.As("cmd.platform");
        Assert.Equal("BREACHED", AckStatus((await cmd.Post(Url(incident, "/acknowledge"))).Json));
        await cmd.Post(Url(incident, "/mitigate"), new { message = "mitigated quickly" });
        var resolved = await cmd.Post(Url(incident, "/resolve"), new { resolutionSummary = "Resolved well in time" });
        Assert.Equal("MET", ResolveStatus(resolved.Json));
        await Api.AdvanceAsync(100);
        var reopened = await cmd.Post(Url(incident, "/reopen"), new { reason = "Came back later" });
        Assert.Equal("BREACHED", ResolveStatus(reopened.Json)); // resolvedAt cleared; deadlines unchanged
    }

    [Fact]
    public async Task Escalation_level_1_is_applied_when_the_clock_passes_the_ack_deadline()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Escalate L1", "SEV1");
        await Api.AdvanceAsync(6);
        Assert.Equal(1, (await Fetch(incident)).Int("escalationLevel"));

        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray();
        Assert.Equal("ESCALATED", audit[1]!["action"]!.GetValue<string>());
        Assert.Null(audit[1]!["actorId"]);
        Assert.Equal(1, audit[1]!["details"]!["level"]!.GetValue<int>());

        // Level 1 goes to the team's commanders (no actor, so nobody is excluded).
        var notes = (await (await Api.As("cmd.platform")).Get("/api/v1/notifications")).Json["items"]!.AsArray();
        Assert.Contains(notes, n => n!["type"]!.GetValue<string>() == "ESCALATION_L1");
        Assert.DoesNotContain((await (await Api.As("admin")).Get("/api/v1/notifications")).Json["items"]!.AsArray(), n => n!["type"]!.GetValue<string>() == "ESCALATION_L1");
    }

    [Fact]
    public async Task Both_levels_in_one_evaluation_write_two_events_in_order_and_l2_also_notifies_admins()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Escalate both", "SEV1");
        await Api.AdvanceAsync(61);
        var fetched = await Fetch(incident);
        Assert.Equal(2, fetched.Int("escalationLevel"));
        var levels = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray()
            .Where(e => e!["action"]!.GetValue<string>() == "ESCALATED").Select(e => e!["details"]!["level"]!.GetValue<int>());
        Assert.Equal([1, 2], levels);
        var adminTypes = (await (await Api.As("admin")).Get("/api/v1/notifications")).Json["items"]!.AsArray().Select(n => n!["type"]!.GetValue<string>());
        Assert.Equal(["ESCALATION_L2"], adminTypes);
    }

    [Fact]
    public async Task Evaluation_is_idempotent_and_never_escalates_twice()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Idempotent", "SEV1");
        await Api.AdvanceAsync(10);
        var admin = await Api.As("admin");
        Assert.Equal(0, (await admin.Post("/api/v1/sla/evaluate")).Int("escalationsApplied"));
        await Api.AdvanceAsync(100);
        Assert.Equal(0, (await admin.Post("/api/v1/sla/evaluate")).Int("escalationsApplied"));
        Assert.Equal(2, (await admin.Get(Url(incident, "/audit"))).Json.AsArray().Count(e => e!["action"]!.GetValue<string>() == "ESCALATED"));
    }

    [Fact]
    public async Task Sla_evaluate_is_admin_only()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("cmd.platform")).Post("/api/v1/sla/evaluate")).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.As("admin")).Post("/api/v1/sla/evaluate")).Status);
    }

    [Fact]
    public async Task Acknowledged_incident_jumps_to_level_2_and_resolved_ones_never_escalate()
    {
        await Api.SetClockAsync(T0);
        var acked = await CreateIncident("Acked then slow", "SEV1");
        var resolved = await CreateIncident("Resolved quickly", "SEV1");
        await Drive(acked, "acknowledge");
        await Drive(resolved, "acknowledge", "mitigate", "resolve");
        await Api.AdvanceAsync(500);
        var a = await Fetch(acked);
        Assert.Equal(2, a.Int("escalationLevel"));
        Assert.Equal(1, (await (await Api.As("admin")).Get(Url(acked, "/audit"))).Json.AsArray().Count(e => e!["action"]!.GetValue<string>() == "ESCALATED"));
        Assert.Equal(0, (await Fetch(resolved)).Int("escalationLevel"));
    }

    [Fact]
    public async Task Request_time_evaluation_escalates_before_applying_the_action()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Request time", "SEV1");
        // The system clock is not advanced through the test endpoints here: set it and then act in the same instant.
        await Api.SetClockAsync("2026-03-01T12:07:00.000Z");
        // Level 1 was applied by the clock change; acknowledge now keeps it and records a BREACHED ack timer.
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/acknowledge"));
        Assert.Equal(1, res.Int("escalationLevel"));
        Assert.Equal("BREACHED", AckStatus(res.Json));
    }

    [Fact]
    public async Task Clock_endpoints_validate_and_report()
    {
        var c = new Client(Api.Anonymous());
        Assert.Equal("fixed", (await c.Put("/api/v1/test/clock", new { now = T0 })).Str("mode"));
        Assert.Equal(T0, (await c.Get("/api/v1/test/clock")).Str("now"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await c.Put("/api/v1/test/clock", new { now = "yesterday" })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await c.Post("/api/v1/test/clock/advance", new { minutes = -1 })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await c.Post("/api/v1/test/clock/advance", new { minutes = 1.5 })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await c.Post("/api/v1/test/clock/advance", new { })).Status);
        var advanced = await c.Post("/api/v1/test/clock/advance", new { minutes = 1, seconds = 30 });
        Assert.Equal("2026-03-01T12:01:30.000Z", advanced.Str("now"));
        Assert.Equal(HttpStatusCode.NoContent, (await c.Post("/api/v1/test/reset")).Status);
        Assert.Equal("system", (await c.Get("/api/v1/test/clock")).Str("mode"));
    }

    [Fact]
    public async Task Reset_restores_seed_and_id_sequences()
    {
        await CreateIncident("Before reset");
        await Api.ResetAsync();
        var again = await CreateIncident("After reset");
        Assert.Equal("INC-0001", again["number"]!.GetValue<string>());
        Assert.DoesNotContain((await (await Api.As("cmd.platform")).Get("/api/v1/notifications")).Json["items"]!.AsArray(), n => n!["message"]!.GetValue<string>().Contains("Before"));
    }

    [Fact]
    public async Task Empty_dashboard_has_zero_filled_maps_and_nulls()
    {
        var d = (await (await Api.As("viewer1")).Get("/api/v1/dashboard/summary")).Json;
        Assert.Equal(0, d.Int("totalIncidents"));
        Assert.Equal(5, d["byStatus"]!.AsObject().Count);
        Assert.Equal(0, d["byStatus"]!["CLOSED"]!.GetValue<int>());
        Assert.Equal(4, d["activeBySeverity"]!.AsObject().Count);
        Assert.Null(d["mttaMinutes"]);
        Assert.Null(d["mttrMinutes"]);
        Assert.Null(d["slaCompliancePercent"]);
        Assert.Empty(d["topServices"]!.AsArray());
    }

    [Fact]
    public async Task Dashboard_aggregates_follow_the_spec_definitions()
    {
        await Api.SetClockAsync(T0);
        var a = await CreateIncident("Dash A", "SEV1"); // service 1
        var b = await CreateIncident("Dash B", "SEV2"); // service 1
        var c = await CreateIncident("Dash C", "SEV3", serviceId: 3);
        var d = await CreateIncident("Dash D", "SEV3", serviceId: 4);
        await Api.AdvanceAsync(2);
        await Drive(c, "acknowledge");                         // ack after 2 min (met)
        await Drive(d, "acknowledge", "mitigate", "resolve");  // resolved after 2 min (met)
        await Api.AdvanceAsync(2);                             // now +4 min: A at risk (80% of 5), B running
        var summary = (await (await Api.As("viewer1")).Get("/api/v1/dashboard/summary")).Json;
        Assert.Equal(4, summary.Int("totalIncidents"));
        Assert.Equal(3, summary.Int("activeCount"));
        Assert.Equal(2, summary["byStatus"]!["OPEN"]!.GetValue<int>());
        Assert.Equal(1, summary["byStatus"]!["ACKNOWLEDGED"]!.GetValue<int>());
        Assert.Equal(1, summary["byStatus"]!["RESOLVED"]!.GetValue<int>());
        Assert.Equal(1, summary["activeBySeverity"]!["SEV1"]!.GetValue<int>());
        Assert.Equal(1, summary["activeBySeverity"]!["SEV2"]!.GetValue<int>());
        Assert.Equal(1, summary["activeBySeverity"]!["SEV3"]!.GetValue<int>());
        Assert.Equal(0, summary.Int("breachedActiveCount"));
        Assert.Equal(1, summary.Int("atRiskActiveCount"));
        Assert.Equal(0, summary.Int("escalatedActiveCount"));
        Assert.Equal(2.0m, summary["mttaMinutes"]!.GetValue<decimal>()); // C and D acked after 2 minutes
        Assert.Equal(2.0m, summary["mttrMinutes"]!.GetValue<decimal>());
        Assert.Equal(100.0m, summary["slaCompliancePercent"]!.GetValue<decimal>());
        var top = summary["topServices"]!.AsArray();
        Assert.Equal("API Gateway", top[0]!["serviceName"]!.GetValue<string>());
        Assert.Equal(2, top[0]!["activeCount"]!.GetValue<int>());
        Assert.Equal("Payments Core", top[1]!["serviceName"]!.GetValue<string>());

        await Api.AdvanceAsync(3); // A breached (>5 min) and escalated
        var later = (await (await Api.As("viewer1")).Get("/api/v1/dashboard/summary")).Json;
        Assert.Equal(1, later.Int("breachedActiveCount"));
        Assert.Equal(1, later.Int("escalatedActiveCount"));
    }

    [Fact]
    public async Task Mtta_rounds_half_away_from_zero_to_one_decimal()
    {
        await Api.SetClockAsync(T0);
        var a = await CreateIncident("Round A", "SEV3");
        await Api.SetClockAsync("2026-03-01T12:00:03.000Z");
        await Drive(a, "acknowledge"); // 3 s = 0.05 min -> 0.1
        var d = (await (await Api.As("viewer1")).Get("/api/v1/dashboard/summary")).Json;
        Assert.Equal(0.1m, d["mttaMinutes"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Compliance_percentage_counts_breached_finished_incidents()
    {
        await Api.SetClockAsync(T0);
        var good = await CreateIncident("Good one", "SEV3");
        var bad = await CreateIncident("Bad one", "SEV3");
        await Drive(good, "acknowledge", "mitigate", "resolve");
        await Api.AdvanceAsync(90); // SEV3 ack target is 60 min
        await Drive(bad, "acknowledge", "mitigate", "resolve");
        var d = (await (await Api.As("viewer1")).Get("/api/v1/dashboard/summary")).Json;
        Assert.Equal(50.0m, d["slaCompliancePercent"]!.GetValue<decimal>());
    }
}
