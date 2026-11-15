using System.Net;
using System.Text.Json.Nodes;

namespace IncidentApp.Tests.Api;

public class ListingTests(ApiFixture api) : ApiTestBase(api)
{
    private async Task<JsonNode> List(string query = "") => (await (await Api.As("viewer1")).Get("/api/v1/incidents" + query)).Json;
    private static List<int> Ids(JsonNode page) => page["items"]!.AsArray().Select(i => i!["id"]!.GetValue<int>()).ToList();

    private async Task Seed()
    {
        await Api.SetClockAsync(T0);
        await CreateIncident("Alpha gateway", "SEV3");              // 1 svc1
        await Api.AdvanceAsync(1);
        await CreateIncident("Beta payments", "SEV1", serviceId: 3); // 2 svc3
        await Api.AdvanceAsync(1);
        await CreateIncident("Gamma auth", "SEV2", serviceId: 5);    // 3 svc5
        await Api.AdvanceAsync(1);
        var d = await CreateIncident("Delta billing", "SEV4", serviceId: 4); // 4 svc4
        await Drive(d, "acknowledge");
    }

    [Fact]
    public async Task Default_order_is_newest_first_with_paging_metadata()
    {
        await Seed();
        var page = await List();
        Assert.Equal([4, 3, 2, 1], Ids(page));
        Assert.Equal(1, page.Int("page"));
        Assert.Equal(20, page.Int("pageSize"));
        Assert.Equal(4, page.Int("totalItems"));
        Assert.Equal(1, page.Int("totalPages"));
    }

    [Fact]
    public async Task Paging_and_page_beyond_the_end()
    {
        await Seed();
        var p2 = await List("?pageSize=3&page=2");
        Assert.Equal([1], Ids(p2));
        Assert.Equal(2, p2.Int("totalPages"));
        var beyond = await List("?pageSize=3&page=9");
        Assert.Empty(Ids(beyond));
        Assert.Equal(0, (await List("?q=nomatch")).Int("totalPages"));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=abc")]
    [InlineData("?status=BOGUS")]
    [InlineData("?sort=title")]
    [InlineData("?order=sideways")]
    [InlineData("?serviceId=-1")]
    [InlineData("?slaBreached=maybe")]
    public async Task Bad_query_parameters_are_422(string query)
    {
        var res = await (await Api.As("viewer1")).Get("/api/v1/incidents" + query);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.Status);
        Assert.Equal("VALIDATION_ERROR", res.Code);
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        await Seed();
        Assert.Equal([2], Ids(await List("?severity=SEV1")));
        Assert.Equal([3, 2], Ids(await List("?severity=SEV1,SEV2")));
        Assert.Equal([4], Ids(await List("?status=ACKNOWLEDGED")));
        Assert.Equal([3, 2, 1], Ids(await List("?active=true&status=OPEN")));
        Assert.Equal([4], Ids(await List("?serviceId=4")));
        Assert.Equal([2], Ids(await List("?teamId=2&severity=SEV1")));
        Assert.Equal([3, 2, 1], Ids(await List("?unassigned=true")));
        Assert.Equal([4], Ids(await List("?assigneeId=3")));
        Assert.Equal([4, 3, 2, 1], Ids(await List("?reporterId=9")));
        Assert.Empty(Ids(await List("?active=false")));
    }

    [Fact]
    public async Task Search_matches_title_or_number_case_insensitively()
    {
        await Seed();
        Assert.Equal([3], Ids(await List("?q=GAMMA")));
        Assert.Equal([2], Ids(await List("?q=inc-0002")));
        Assert.Equal([4, 3, 2, 1], Ids(await List("?q=a")).OrderByDescending(x => x).ToList());
    }

    [Fact]
    public async Task Sorting_by_severity_puts_sev1_first_when_ascending_and_ties_break_by_id()
    {
        await Seed();
        Assert.Equal([2, 3, 1, 4], Ids(await List("?sort=severity&order=asc")));
        Assert.Equal([4, 1, 3, 2], Ids(await List("?sort=severity&order=desc")));
        Assert.Equal([1, 2, 3, 4], Ids(await List("?sort=createdAt&order=asc")));
        Assert.Equal(4, Ids(await List("?sort=updatedAt"))[0]);
    }

    [Fact]
    public async Task Sla_breached_filter_uses_the_clock()
    {
        await Seed(); // now = T0 + 3 min
        Assert.Empty(Ids(await List("?slaBreached=true")));
        await Api.AdvanceAsync(3); // SEV1 (#2, created T0+1) breaches at T0+6 -> now T0+6 is not breached yet
        Assert.Empty(Ids(await List("?slaBreached=true")));
        await Api.AdvanceAsync(1);
        Assert.Equal([2], Ids(await List("?slaBreached=true")));
        Assert.Equal(3, (await List("?slaBreached=false"))["totalItems"]!.GetValue<int>());
    }
}

public class NotesActionItemsAndServicesTests(ApiFixture api) : ApiTestBase(api)
{
    [Fact]
    public async Task Notes_are_listed_in_order_and_audited()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        var n1 = await cmd.Post(Url(incident, "/notes"), new { body = "  first  " });
        Assert.Equal(HttpStatusCode.Created, n1.Status);
        Assert.Equal("first", n1.Str("body"));
        await (await Api.As("resp.platform1")).Post(Url(incident, "/notes"), new { body = "second" });
        var notes = (await (await Api.As("viewer1")).Get(Url(incident, "/notes"))).Json.AsArray();
        Assert.Equal(["first", "second"], notes.Select(n => n!["body"]!.GetValue<string>()));
        var audit = (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Json.AsArray();
        Assert.Equal(2, audit.Count(e => e!["action"]!.GetValue<string>() == "NOTE_ADDED"));
        Assert.Equal(n1.Int("id"), audit[1]!["details"]!["noteId"]!.GetValue<int>());
    }

    [Fact]
    public async Task Note_permissions_follow_the_matrix()
    {
        var incident = await CreateIncident();
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("viewer1")).Post(Url(incident, "/notes"), new { body = "x" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("resp.payments1")).Post(Url(incident, "/notes"), new { body = "x" })).Status);
        Assert.Equal(HttpStatusCode.Created, (await (await Api.As("reporter1")).Post(Url(incident, "/notes"), new { body = "mine" })).Status);
        var other = await CreateIncident("Someone else's", "SEV3", user: "cmd.platform");
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("reporter1")).Post(Url(other, "/notes"), new { body = "not mine" })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await (await Api.As("admin")).Post(Url(incident, "/notes"), new { body = "   " })).Status);
    }

    [Fact]
    public async Task Action_item_creation_validates_owner_and_due_date()
    {
        await Api.SetClockAsync("2026-03-01T12:00:00.000Z");
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        string Code(Reply r, string field) => r.Json["errors"]!.AsArray().First(e => e!["field"]!.GetValue<string>() == field)!["code"]!.GetValue<string>();
        var bad = await cmd.Post(Url(incident, "/action-items"), new { title = "ab", ownerId = 99, dueDate = "2026-02-28" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.Status);
        Assert.Equal("too_short", Code(bad, "title"));
        Assert.Equal("not_found", Code(bad, "ownerId"));
        Assert.Equal("out_of_range", Code(bad, "dueDate"));
        Assert.Equal("invalid_value", Code(await cmd.Post(Url(incident, "/action-items"), new { title = "Valid", ownerId = 9, dueDate = "2026-03-01" }), "ownerId"));
        Assert.Equal("invalid_format", Code(await cmd.Post(Url(incident, "/action-items"), new { title = "Valid", ownerId = 5, dueDate = "03/01/2026" }), "dueDate"));

        var ok = await cmd.Post(Url(incident, "/action-items"), new { title = "Due today is fine", ownerId = 5, dueDate = "2026-03-01" });
        Assert.Equal(HttpStatusCode.Created, ok.Status);
        Assert.Equal("OPEN", ok.Str("status"));
        Assert.Null(ok.Json["completedAt"]);
        var fetched = await cmd.Get(Url(incident));
        Assert.Equal(1, fetched.Int("actionItemCount"));
        Assert.Equal(1, fetched.Int("openActionItemCount"));
    }

    [Fact]
    public async Task Action_item_update_toggles_status_and_only_validates_due_date_when_changed()
    {
        await Api.SetClockAsync("2026-03-01T12:00:00.000Z");
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        var item = await cmd.Post(Url(incident, "/action-items"), new { title = "Patch it", ownerId = 5, dueDate = "2026-03-01" });
        var path = Url(incident, $"/action-items/{item.Int("id")}");
        await Api.SetClockAsync("2026-03-10T12:00:00.000Z"); // due date is now in the past
        var done = await cmd.Patch(path, new { status = "DONE", dueDate = "2026-03-01" });
        Assert.Equal(HttpStatusCode.OK, done.Status);
        Assert.Equal("2026-03-10T12:00:00.000Z", done.Str("completedAt"));
        var reopened = await cmd.Patch(path, new { status = "OPEN" });
        Assert.Null(reopened.Json["completedAt"]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Patch(path, new { dueDate = "2026-03-02" })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Patch(path, new { })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await cmd.Patch(Url(incident, "/action-items/999"), new { status = "DONE" })).Status);
    }

    [Fact]
    public async Task Expected_version_is_honoured_by_notes_and_action_items_and_a_conflict_changes_nothing()
    {
        var incident = await CreateIncident();
        var cmd = await Api.As("cmd.platform");
        var stale = await cmd.Post(Url(incident, "/notes"), new { body = "stale write", expectedVersion = 5 });
        Assert.Equal("VERSION_CONFLICT", stale.Code);
        Assert.Equal("VERSION_CONFLICT", (await cmd.Post(Url(incident, "/action-items"), new { title = "Stale item", ownerId = 5, dueDate = "2999-01-01", expectedVersion = 5 })).Code);
        Assert.Empty((await cmd.Get(Url(incident, "/notes"))).Json.AsArray());
        Assert.Equal(1, (await cmd.Get(Url(incident))).Int("version"));
        Assert.Equal(HttpStatusCode.Created, (await cmd.Post(Url(incident, "/notes"), new { body = "fresh write", expectedVersion = 1 })).Status);
    }

    [Fact]
    public async Task Rejected_requests_leave_audit_notes_notifications_and_version_unchanged()
    {
        var incident = await CreateIncident("Atomicity", "SEV2");
        var admin = await Api.As("admin");
        async Task<(int Audit, int Notifications, int Notes, int Version)> Snapshot() => (
            (await admin.Get("/api/v1/audit")).Int("totalItems"),
            (await admin.Get("/api/v1/notifications?scope=all")).Int("totalItems"),
            (await admin.Get(Url(incident, "/notes"))).Json.AsArray().Count,
            (await admin.Get(Url(incident))).Int("version"));
        var before = await Snapshot();
        var cmd = await Api.As("cmd.platform");
        await cmd.Post(Url(incident, "/resolve"), new { resolutionSummary = "not allowed from OPEN" }); // 409
        await cmd.Post(Url(incident, "/mitigate"), new { });                                           // 422
        await cmd.Post(Url(incident, "/severity"), new { severity = "SEV2", reason = "same severity" }); // 422
        await (await Api.As("cmd.payments")).Post(Url(incident, "/acknowledge"));                      // 403
        await cmd.Post("/api/v1/incidents", new { title = "Atomicity", serviceId = 1, severity = "SEV2" }); // 409 duplicate
        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public async Task Action_item_belonging_to_another_incident_is_404()
    {
        var a = await CreateIncident("Incident A");
        var b = await CreateIncident("Incident B");
        var cmd = await Api.As("cmd.platform");
        var item = await cmd.Post(Url(a, "/action-items"), new { title = "Item of A", ownerId = 5, dueDate = "2999-01-01" });
        Assert.Equal(HttpStatusCode.NotFound, (await cmd.Patch(Url(b, $"/action-items/{item.Int("id")}"), new { status = "DONE" })).Status);
    }

    [Fact]
    public async Task Action_item_permissions()
    {
        var incident = await CreateIncident();
        var body = new { title = "Some item", ownerId = 5, dueDate = "2999-01-01" };
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("reporter1")).Post(Url(incident, "/action-items"), body)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("resp.payments1")).Post(Url(incident, "/action-items"), body)).Status);
        Assert.Equal(HttpStatusCode.Created, (await (await Api.As("resp.platform1")).Post(Url(incident, "/action-items"), body)).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.As("viewer1")).Get(Url(incident, "/action-items"))).Status);
    }

    [Fact]
    public async Task Audit_and_notification_read_permissions()
    {
        var incident = await CreateIncident();
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("viewer1")).Get(Url(incident, "/audit"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("reporter1")).Get(Url(incident, "/audit"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("resp.payments1")).Get(Url(incident, "/audit"))).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.As("resp.platform1")).Get(Url(incident, "/audit"))).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await Api.As("admin")).Get(Url(incident, "/audit"))).Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("cmd.platform")).Get("/api/v1/audit")).Status);
        var global = await (await Api.As("admin")).Get("/api/v1/audit?incidentId=1&action=INCIDENT_CREATED");
        Assert.Equal(1, global.Int("totalItems"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await (await Api.As("admin")).Get("/api/v1/audit?action=NOPE")).Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("cmd.platform")).Get("/api/v1/notifications?scope=all")).Status);
        Assert.Equal(1, (await (await Api.As("admin")).Get("/api/v1/notifications?scope=all")).Int("totalItems"));
    }

    [Fact]
    public async Task Service_admin_create_update_and_uniqueness()
    {
        var admin = await Api.As("admin");
        var created = await admin.Post("/api/v1/services", new { key = "SEARCH-API", name = "Search API", ownerTeamId = 1, tier = 2 });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.True(created.Json["active"]!.GetValue<bool>());
        Assert.EndsWith($"/api/v1/services/{created.Int("id")}", created.Headers.Location!.ToString());

        Assert.Equal("DUPLICATE_SERVICE", (await admin.Post("/api/v1/services", new { key = "SEARCH-API", name = "Other name", ownerTeamId = 1, tier = 2 })).Code);
        Assert.Equal("DUPLICATE_SERVICE", (await admin.Post("/api/v1/services", new { key = "OTHER-KEY", name = "search api", ownerTeamId = 1, tier = 2 })).Code);

        var put = await admin.Put($"/api/v1/services/{created.Int("id")}", new { key = "SEARCH-API", name = "Search API v2", ownerTeamId = 2, tier = 1, active = false });
        Assert.Equal(HttpStatusCode.OK, put.Status);
        Assert.False(put.Json["active"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.Put($"/api/v1/services/{created.Int("id")}", new { key = "x", name = "Search API v2", ownerTeamId = 2, tier = 1 })).Status);

        var audit = (await admin.Get("/api/v1/audit")).Json["items"]!.AsArray().Select(e => e!["action"]!.GetValue<string>());
        Assert.Equal(["SERVICE_CREATED", "SERVICE_UPDATED"], audit);
    }

    [Fact]
    public async Task Service_validation_and_permissions()
    {
        var admin = await Api.As("admin");
        var bad = await admin.Post("/api/v1/services", new { key = "lowercase", name = "x", ownerTeamId = 9, tier = 7 });
        var fields = bad.Json["errors"]!.AsArray().Select(e => e!["field"]!.GetValue<string>()).ToHashSet();
        Assert.Superset(new HashSet<string> { "key", "name", "ownerTeamId", "tier" }, fields);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.As("cmd.platform")).Post("/api/v1/services", new { key = "OK-KEY", name = "Okay", ownerTeamId = 1, tier = 1 })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Get("/api/v1/services/999")).Status);
        Assert.Equal("API-GATEWAY", (await admin.Get("/api/v1/services/1")).Str("key"));
    }

    [Fact]
    public async Task Incident_keeps_its_owning_team_when_the_service_is_moved()
    {
        var incident = await CreateIncident();
        await (await Api.As("admin")).Put("/api/v1/services/1", new { key = "API-GATEWAY", name = "API Gateway", ownerTeamId = 2, tier = 1, active = true });
        Assert.Equal(1, (await (await Api.As("viewer1")).Get(Url(incident))).Int("teamId"));
    }
}
