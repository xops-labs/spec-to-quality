using System.Net;

namespace IncidentApp.Tests.Api;

public class IncidentCreationTests(ApiFixture api) : ApiTestBase(api)
{
    [Fact]
    public async Task Create_returns_201_with_location_and_initial_state()
    {
        await Api.SetClockAsync(T0);
        var res = await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "  Gateway 5xx  ", serviceId = 1, severity = "SEV2" });
        Assert.Equal(HttpStatusCode.Created, res.Status);
        Assert.EndsWith("/api/v1/incidents/1", res.Headers.Location!.ToString());
        Assert.Equal("INC-0001", res.Str("number"));
        Assert.Equal("Gateway 5xx", res.Str("title"));
        Assert.Equal("", res.Str("description"));
        Assert.Equal("OPEN", res.Str("status"));
        Assert.Equal(9, res.Int("reporterId"));
        Assert.Null(res.Json["assigneeId"]);
        Assert.Equal(1, res.Int("teamId"));
        Assert.Equal(1, res.Int("version"));
        Assert.Equal("API Gateway", res.Str("serviceName"));
        Assert.Equal(T0, res.Str("createdAt"));
        Assert.Equal("2026-03-01T12:15:00.000Z", res.Json["sla"]!["ack"]!["deadline"]!.GetValue<string>());
        Assert.Equal("RUNNING", res.Json["sla"]!["ack"]!["status"]!.GetValue<string>());
        Assert.False(res.Json["sla"]!["breached"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Viewer_cannot_create() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("viewer1")).Post("/api/v1/incidents", new { title = "Valid title", serviceId = 1, severity = "SEV3" })).Status);

    [Fact]
    public async Task Validation_errors_name_each_field()
    {
        var res = await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "abc", description = new string('x', 4001), serviceId = 99, severity = "SEV9" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.Status);
        Assert.Equal("VALIDATION_ERROR", res.Code);
        var errors = res.Json["errors"]!.AsArray().ToDictionary(e => e!["field"]!.GetValue<string>(), e => e!["code"]!.GetValue<string>());
        Assert.Equal("too_short", errors["title"]);
        Assert.Equal("too_long", errors["description"]);
        Assert.Equal("not_found", errors["serviceId"]);
        Assert.Equal("invalid_value", errors["severity"]);
    }

    [Fact]
    public async Task Missing_fields_are_required_errors()
    {
        var res = await (await Api.As("reporter1")).Post("/api/v1/incidents", new { });
        var fields = res.Json["errors"]!.AsArray().Select(e => e!["field"]!.GetValue<string>()).ToHashSet();
        Assert.Superset(new HashSet<string> { "title", "serviceId", "severity" }, fields);
    }

    [Fact]
    public async Task Inactive_service_is_a_business_rule_violation()
    {
        var res = await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "Legacy report broken", serviceId = 6, severity = "SEV3" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.Status);
        Assert.Equal("BUSINESS_RULE_VIOLATION", res.Code);
        Assert.Equal("SERVICE_INACTIVE", res.Json["details"]!["rule"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sev1_is_rejected_for_tier_3_services_but_allowed_for_tier_2()
    {
        var reporter = await Api.As("reporter1");
        var bad = await reporter.Post("/api/v1/incidents", new { title = "Pipeline is down", serviceId = 2, severity = "SEV1" });
        Assert.Equal("BUSINESS_RULE_VIOLATION", bad.Code);
        Assert.Equal("SEV1_REQUIRES_TIER_1_OR_2", bad.Json["details"]!["rule"]!.GetValue<string>());
        var ok = await reporter.Post("/api/v1/incidents", new { title = "Auth is down", serviceId = 5, severity = "SEV1" });
        Assert.Equal(HttpStatusCode.Created, ok.Status);
    }

    [Fact]
    public async Task Duplicate_open_title_on_same_service_conflicts_case_insensitively()
    {
        var first = await CreateIncident("Checkout failing");
        var res = await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "  CHECKOUT FAILING ", serviceId = 1, severity = "SEV3" });
        Assert.Equal(HttpStatusCode.Conflict, res.Status);
        Assert.Equal("DUPLICATE_INCIDENT", res.Code);
        Assert.Equal(first["id"]!.GetValue<int>(), res.Json["details"]!["existingIncidentId"]!.GetValue<int>());
        // Another service is fine.
        Assert.Equal(HttpStatusCode.Created, (await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "Checkout failing", serviceId = 3, severity = "SEV3" })).Status);
    }

    [Fact]
    public async Task Resolved_incidents_do_not_count_for_duplicates()
    {
        var first = await CreateIncident("Queue backlog");
        await Drive(first, "acknowledge", "mitigate", "resolve");
        Assert.Equal(HttpStatusCode.Created, (await (await Api.As("reporter1")).Post("/api/v1/incidents", new { title = "Queue backlog", serviceId = 1, severity = "SEV2" })).Status);
    }

    [Fact]
    public async Task Creation_notifies_team_commanders_but_not_the_actor()
    {
        await CreateIncident();
        var cmd = (await (await Api.As("cmd.platform")).Get("/api/v1/notifications")).Json["items"]!.AsArray();
        Assert.Single(cmd);
        Assert.Equal("INCIDENT_CREATED", cmd[0]!["type"]!.GetValue<string>());
        Assert.Empty((await (await Api.As("cmd.payments")).Get("/api/v1/notifications")).Json["items"]!.AsArray());

        // A commander reporting their own team's incident gets no notification for it.
        await CreateIncident("Second problem", user: "cmd.platform");
        Assert.Single((await (await Api.As("cmd.platform")).Get("/api/v1/notifications")).Json["items"]!.AsArray());
    }

    [Fact]
    public async Task Creation_writes_exactly_one_audit_event()
    {
        var incident = await CreateIncident();
        var audit = await (await Api.As("admin")).Get(Url(incident, "/audit"));
        var events = audit.Json.AsArray();
        Assert.Single(events);
        Assert.Equal("INCIDENT_CREATED", events[0]!["action"]!.GetValue<string>());
        Assert.Equal(9, events[0]!["actorId"]!.GetValue<int>());
        Assert.Equal("SEV2", events[0]!["details"]!["severity"]!.GetValue<string>());
    }

    [Fact]
    public async Task Patch_updates_fields_and_audits_changed_fields_only()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        var res = await cmd.Patch(Url(incident), new { title = "Gateway latency (updated)", description = "details" });
        Assert.Equal(HttpStatusCode.OK, res.Status);
        Assert.Equal(2, res.Int("version"));
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray();
        Assert.Equal("INCIDENT_UPDATED", audit[1]!["action"]!.GetValue<string>());
        Assert.Equal(["title"], audit[1]!["details"]!["changedFields"]!.AsArray().Select(x => x!.GetValue<string>()));

        // No-op patch writes no audit event and keeps the version.
        var noop = await cmd.Patch(Url(incident), new { title = "Gateway latency (updated)" });
        Assert.Equal(2, noop.Int("version"));
        Assert.Equal(2, (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray().Count);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Patch(Url(incident), new { })).Status);
    }

    [Fact]
    public async Task Reporter_may_edit_only_own_open_incident()
    {
        var incident = await CreateIncident();
        var reporter = await Api.As("reporter1");
        Assert.Equal(HttpStatusCode.OK, (await reporter.Patch(Url(incident), new { description = "more info" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("viewer1")).Patch(Url(incident), new { description = "x" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("resp.platform1")).Patch(Url(incident), new { description = "x" })).Status);
        await Drive(incident, "acknowledge");
        Assert.Equal(HttpStatusCode.Forbidden, (await reporter.Patch(Url(incident), new { description = "later" })).Status);
    }

    [Fact]
    public async Task Retitling_into_a_duplicate_conflicts()
    {
        await CreateIncident("Alpha outage");
        var second = await CreateIncident("Beta outage");
        var res = await (await Api.As("cmd.platform")).Patch(Url(second), new { title = "alpha OUTAGE" });
        Assert.Equal("DUPLICATE_INCIDENT", res.Code);
    }
}
