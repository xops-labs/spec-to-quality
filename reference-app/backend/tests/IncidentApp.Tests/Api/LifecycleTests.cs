using System.Net;
using System.Text.Json.Nodes;

namespace IncidentApp.Tests.Api;

public class LifecycleTests(ApiFixture api) : ApiTestBase(api)
{
    private static IEnumerable<string> Actions(JsonNode audit) => audit.AsArray().Select(e => e!["action"]!.GetValue<string>());

    [Fact]
    public async Task Happy_path_open_to_closed_with_audit_trail_and_timestamps()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Database failover", "SEV3");
        var cmd = await Api.As("cmd.platform");

        await Api.AdvanceAsync(1);
        var ack = await cmd.Post(Url(incident, "/acknowledge"));
        Assert.Equal("ACKNOWLEDGED", ack.Str("status"));
        Assert.Equal("2026-03-01T12:01:00.000Z", ack.Str("acknowledgedAt"));
        Assert.Equal(2, ack.Int("assigneeId")); // auto-assigned to the acting commander

        await Api.AdvanceAsync(10);
        var mit = await cmd.Post(Url(incident, "/mitigate"), new { message = "Failed over to replica" });
        Assert.Equal("MITIGATED", mit.Str("status"));
        Assert.Equal("2026-03-01T12:11:00.000Z", mit.Str("mitigatedAt"));

        await Api.AdvanceAsync(10);
        var res = await cmd.Post(Url(incident, "/resolve"), new { resolutionSummary = "Replica promoted and verified" });
        Assert.Equal("RESOLVED", res.Str("status"));
        Assert.Equal("Replica promoted and verified", res.Str("resolutionSummary"));
        Assert.Equal("MET", res.Json["sla"]!["resolve"]!["status"]!.GetValue<string>());

        var closed = await cmd.Post(Url(incident, "/close"));
        Assert.Equal("CLOSED", closed.Str("status"));
        Assert.NotNull(closed.Json["closedAt"]);

        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json;
        Assert.Equal(["INCIDENT_CREATED", "ASSIGNED", "ACKNOWLEDGED", "MITIGATED", "RESOLVED", "CLOSED"], Actions(audit));
        var ids = audit.AsArray().Select(e => e!["id"]!.GetValue<int>()).ToList();
        Assert.Equal(ids.OrderBy(x => x), ids);
        Assert.Equal("MITIGATED", audit[3]!["details"]!["to"]!.GetValue<string>());
        Assert.Equal("ACKNOWLEDGED", audit[3]!["details"]!["from"]!.GetValue<string>());

        // The mitigation message became a note by the actor, with no NOTE_ADDED audit event.
        var notes = (await cmd.Get(Url(incident, "/notes"))).Json.AsArray();
        Assert.Equal("Failed over to replica", notes[0]!["body"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("mitigate")]
    [InlineData("resolve")]
    [InlineData("close")]
    [InlineData("reopen")]
    public async Task Invalid_transitions_from_open_return_409_with_details(string action)
    {
        var incident = await CreateIncident();
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/" + action), new { message = "valid message", resolutionSummary = "a valid summary", reason = "valid reason" });
        Assert.Equal(HttpStatusCode.Conflict, res.Status);
        Assert.Equal("INVALID_TRANSITION", res.Code);
        Assert.Equal("OPEN", res.Json["details"]!["currentStatus"]!.GetValue<string>());
        Assert.Equal(action, res.Json["details"]!["action"]!.GetValue<string>());
    }

    [Fact]
    public async Task Acknowledged_cannot_resolve_directly_and_acknowledge_twice_fails()
    {
        var incident = await CreateIncident();
        await Drive(incident, "acknowledge");
        var cmd = await Api.As("cmd.platform");
        Assert.Equal("INVALID_TRANSITION", (await cmd.Post(Url(incident, "/resolve"), new { resolutionSummary = "directly resolving it" })).Code);
        Assert.Equal("INVALID_TRANSITION", (await cmd.Post(Url(incident, "/acknowledge"))).Code);
    }

    [Fact]
    public async Task Closed_is_terminal_and_blocks_edits_notes_and_action_items()
    {
        var incident = await CreateIncident("Terminal check", "SEV3");
        await Drive(incident, "acknowledge", "mitigate", "resolve", "close");
        var cmd = await Api.As("cmd.platform");
        Assert.Equal("INVALID_TRANSITION", (await cmd.Post(Url(incident, "/reopen"), new { reason = "reopening it" })).Code);
        Assert.Equal("INVALID_STATE", (await cmd.Patch(Url(incident), new { description = "late edit" })).Code);
        Assert.Equal("INVALID_STATE", (await cmd.Post(Url(incident, "/notes"), new { body = "late note" })).Code);
        Assert.Equal("INVALID_STATE", (await cmd.Post(Url(incident, "/action-items"), new { title = "Late item", ownerId = 2, dueDate = "2999-01-01" })).Code);
        Assert.Equal("INVALID_STATE", (await cmd.Post(Url(incident, "/assign"), new { assigneeId = 5 })).Code);
        Assert.Equal("INVALID_STATE", (await cmd.Post(Url(incident, "/severity"), new { severity = "SEV4", reason = "late change" })).Code);
    }

    [Fact]
    public async Task Rejected_operations_write_no_audit_events()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        await cmd.Post(Url(incident, "/resolve"), new { resolutionSummary = "should be rejected" });
        await cmd.Post(Url(incident, "/mitigate"), new { });
        Assert.Single((await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray());
    }

    [Fact]
    public async Task Reopen_clears_timestamps_counts_and_keeps_the_summary()
    {
        var incident = await CreateIncident();
        await Drive(incident, "acknowledge", "mitigate", "resolve");
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/reopen"), new { reason = "Symptoms returned" });
        Assert.Equal("ACKNOWLEDGED", res.Str("status"));
        Assert.Null(res.Json["resolvedAt"]);
        Assert.Null(res.Json["mitigatedAt"]);
        Assert.NotNull(res.Json["acknowledgedAt"]);
        Assert.Equal(1, res.Int("reopenCount"));
        Assert.Equal("Fixed by rollback of change", res.Str("resolutionSummary"));
        var notes = (await (await Api.As("cmd.platform")).Get(Url(incident, "/notes"))).Json.AsArray();
        Assert.Equal("Symptoms returned", notes.Last()!["body"]!.GetValue<string>());
    }

    [Fact]
    public async Task Reopen_requires_a_reason()
    {
        var incident = await CreateIncident();
        await Drive(incident, "acknowledge", "mitigate", "resolve");
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/reopen"), new { reason = "no" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.Status);
        Assert.Equal("reason", res.Json["errors"]![0]!["field"]!.GetValue<string>());
    }

    [Fact]
    public async Task Admin_cannot_acknowledge_an_unassigned_incident_but_can_once_assigned()
    {
        var incident = await CreateIncident();
        var admin = await Api.As("admin");
        Assert.Equal("ASSIGNEE_REQUIRED", (await admin.Post(Url(incident, "/acknowledge"))).Code);
        Assert.Equal(HttpStatusCode.OK, (await admin.Post(Url(incident, "/assign"), new { assigneeId = 5 })).Status);
        var res = await admin.Post(Url(incident, "/acknowledge"));
        Assert.Equal(HttpStatusCode.OK, res.Status);
        Assert.Equal(5, res.Int("assigneeId"));
    }

    [Fact]
    public async Task Responder_acknowledge_auto_assigns_and_audits_assigned_before_acknowledged()
    {
        var incident = await CreateIncident();
        var res = await (await Api.As("resp.platform2")).Post(Url(incident, "/acknowledge"));
        Assert.Equal(6, res.Int("assigneeId"));
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json;
        Assert.Equal(["INCIDENT_CREATED", "ASSIGNED", "ACKNOWLEDGED"], Actions(audit));
        Assert.Equal(6, audit[1]!["details"]!["to"]!.GetValue<int>());
        Assert.Null(audit[1]!["details"]!["from"]);
    }

    [Fact]
    public async Task Existing_assignee_is_kept_on_acknowledge()
    {
        var incident = await CreateIncident();
        await (await Api.As("cmd.platform")).Post(Url(incident, "/assign"), new { assigneeId = 5 });
        var res = await (await Api.As("resp.platform2")).Post(Url(incident, "/acknowledge"));
        Assert.Equal(5, res.Int("assigneeId"));
    }

    [Fact]
    public async Task Responder_needs_assignment_to_mitigate_and_resolve_but_commander_does_not()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        await cmd.Post(Url(incident, "/assign"), new { assigneeId = 5 });
        await cmd.Post(Url(incident, "/acknowledge"));
        var notAssigned = await Api.As("resp.platform2");
        Assert.Equal(HttpStatusCode.Forbidden, (await notAssigned.Post(Url(incident, "/mitigate"), new { message = "trying anyway" })).Status);
        var assigned = await Api.As("resp.platform1");
        Assert.Equal(HttpStatusCode.OK, (await assigned.Post(Url(incident, "/mitigate"), new { message = "mitigation applied" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await assigned.Post(Url(incident, "/resolve"), new { resolutionSummary = "Fully resolved by assignee" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await assigned.Post(Url(incident, "/close"))).Status);
    }

    [Fact]
    public async Task Other_teams_cannot_act_but_admin_can()
    {
        var incident = await CreateIncident();
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("resp.payments1")).Post(Url(incident, "/acknowledge"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("cmd.identity")).Post(Url(incident, "/assign"), new { assigneeId = 4 })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("reporter1")).Post(Url(incident, "/acknowledge"))).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.As("admin")).Post(Url(incident, "/assign"), new { assigneeId = 5 })).Status);
    }

    [Fact]
    public async Task Close_requires_all_action_items_done_checked_before_the_sev_rule()
    {
        var incident = await CreateIncident("Close rules", "SEV2");
        await Drive(incident, "acknowledge", "mitigate", "resolve");
        var cmd = await Api.As("cmd.platform");
        Assert.Equal("ACTION_ITEM_REQUIRED", (await cmd.Post(Url(incident, "/close"))).Code);

        var item = await cmd.Post(Url(incident, "/action-items"), new { title = "Write postmortem", ownerId = 5, dueDate = "2999-01-01" });
        Assert.Equal(HttpStatusCode.Created, item.Status);
        Assert.Equal("OPEN_ACTION_ITEMS", (await cmd.Post(Url(incident, "/close"))).Code);

        var done = await cmd.Patch(Url(incident, $"/action-items/{item.Int("id")}"), new { status = "DONE" });
        Assert.NotNull(done.Json["completedAt"]);
        Assert.Equal(HttpStatusCode.OK, (await cmd.Post(Url(incident, "/close"))).Status);
    }

    [Fact]
    public async Task Sev3_incident_closes_without_any_action_item()
    {
        var incident = await CreateIncident("No actions needed", "SEV3");
        await Drive(incident, "acknowledge", "mitigate", "resolve", "close");
    }

    [Fact]
    public async Task Resolve_and_close_notify_the_reporter_only()
    {
        var incident = await CreateIncident("Notify reporter", "SEV3");
        await Drive(incident, "acknowledge", "mitigate", "resolve", "close");
        var types = (await (await Api.As("reporter1")).Get("/api/v1/notifications")).Json["items"]!.AsArray().Select(n => n!["type"]!.GetValue<string>());
        Assert.Equal(["CLOSED", "RESOLVED"], types); // newest first
        Assert.Empty((await (await Api.As("viewer1")).Get("/api/v1/notifications")).Json["items"]!.AsArray());
    }

    [Fact]
    public async Task Assign_validates_the_assignee_and_is_idempotent()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        Assert.Equal("not_found", (await cmd.Post(Url(incident, "/assign"), new { assigneeId = 999 })).Json["errors"]![0]!["code"]!.GetValue<string>());
        foreach (var bad in new[] { 7, 9, 10, 1 }) // other team, reporter, viewer, admin
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Post(Url(incident, "/assign"), new { assigneeId = bad })).Status);
        }
        var first = await cmd.Post(Url(incident, "/assign"), new { assigneeId = 5 });
        var again = await cmd.Post(Url(incident, "/assign"), new { assigneeId = 5 });
        Assert.Equal(first.Int("version"), again.Int("version"));
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json;
        Assert.Equal(1, Actions(audit).Count(a => a == "ASSIGNED"));
        var resp = (await (await Api.As("resp.platform1")).Get("/api/v1/notifications")).Json["items"]!.AsArray();
        Assert.Single(resp);
        Assert.Equal("ASSIGNED", resp[0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Severity_change_requires_reason_adds_note_audits_and_recomputes_deadlines()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Severity change", "SEV4"); // ack target 240 min
        var cmd = await Api.As("cmd.platform");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Post(Url(incident, "/severity"), new { severity = "SEV4", reason = "same severity" })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Post(Url(incident, "/severity"), new { severity = "SEV2" })).Status);

        var res = await cmd.Post(Url(incident, "/severity"), new { severity = "SEV2", reason = "Customer impact grew" });
        Assert.Equal("SEV2", res.Str("severity"));
        Assert.Equal("2026-03-01T12:15:00.000Z", res.Json["sla"]!["ack"]!["deadline"]!.GetValue<string>());
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json;
        Assert.Equal("SEVERITY_CHANGED", audit[1]!["action"]!.GetValue<string>());
        Assert.Equal("Customer impact grew", audit[1]!["details"]!["reason"]!.GetValue<string>());
        Assert.Contains("Customer impact grew", (await cmd.Get(Url(incident, "/notes"))).Json.AsArray().Select(n => n!["body"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Sev1_severity_change_respects_the_tier_rule()
    {
        var incident = await CreateIncident("Pipeline slow", "SEV3", serviceId: 2); // tier 3
        var res = await (await Api.As("cmd.platform")).Post(Url(incident, "/severity"), new { severity = "SEV1", reason = "Looks critical" });
        Assert.Equal("BUSINESS_RULE_VIOLATION", res.Code);
        Assert.Equal("SEV1_REQUIRES_TIER_1_OR_2", res.Json["details"]!["rule"]!.GetValue<string>());
    }

    [Fact]
    public async Task Expected_version_mismatch_conflicts_and_match_succeeds()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        var stale = await cmd.Post(Url(incident, "/acknowledge"), new { expectedVersion = 7 });
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal("VERSION_CONFLICT", stale.Code);
        var ok = await cmd.Post(Url(incident, "/acknowledge"), new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.True(ok.Int("version") > 1);
    }

    [Fact]
    public async Task Simultaneous_identical_transitions_yield_exactly_one_success()
    {
        var incident = await CreateIncident();
        var a = await Api.As("cmd.platform");
        var b = await Api.As("admin");
        await a.Post(Url(incident, "/assign"), new { assigneeId = 5 });
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? a : b).Post(Url(incident, "/acknowledge"))));
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.Status != HttpStatusCode.OK), r => Assert.Contains(r.Code, new[] { "INVALID_TRANSITION", "VERSION_CONFLICT" }));
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json;
        Assert.Equal(1, Actions(audit).Count(x => x == "ACKNOWLEDGED"));
    }

    [Fact]
    public async Task Updated_at_changes_on_mutations()
    {
        await Api.SetClockAsync(T0);
        var incident = await CreateIncident("Updated at", "SEV3");
        await Api.AdvanceAsync(2);
        var cmd = await Api.As("cmd.platform");
        await cmd.Post(Url(incident, "/notes"), new { body = "A note" });
        Assert.Equal("2026-03-01T12:02:00.000Z", (await cmd.Get(Url(incident))).Str("updatedAt"));
        await Api.AdvanceAsync(2);
        await cmd.Post(Url(incident, "/action-items"), new { title = "Follow up", ownerId = 2, dueDate = "2999-01-01" });
        Assert.Equal("2026-03-01T12:04:00.000Z", (await cmd.Get(Url(incident))).Str("updatedAt"));
    }
}
