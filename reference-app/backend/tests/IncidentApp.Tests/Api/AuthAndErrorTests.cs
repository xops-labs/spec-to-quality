using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace IncidentApp.Tests.Api;

public class AuthAndErrorTests(ApiFixture api) : ApiTestBase(api)
{
    [Fact]
    public async Task Health_is_public()
    {
        var res = await new Client(Api.Anonymous()).Get("/health");
        Assert.Equal(HttpStatusCode.OK, res.Status);
        Assert.Equal("ok", res.Str("status"));
    }

    [Fact]
    public async Task Login_returns_token_and_user_without_password_material()
    {
        var res = await new Client(Api.Anonymous()).Post("/api/v1/auth/login", new { username = "cmd.platform", password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, res.Status);
        Assert.False(string.IsNullOrEmpty(res.Str("token")));
        Assert.Equal("COMMANDER", res.Json["user"]!["role"]!.GetValue<string>());
        Assert.Equal(1, res.Json["user"]!["teamId"]!.GetValue<int>());
        Assert.DoesNotContain("password", res.Body!.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_user_and_wrong_password_are_indistinguishable()
    {
        var c = new Client(Api.Anonymous());
        var unknown = await c.Post("/api/v1/auth/login", new { username = "nobody", password = "x" });
        var wrong = await c.Post("/api/v1/auth/login", new { username = "admin", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
        Assert.Equal("INVALID_CREDENTIALS", unknown.Code);
        Assert.Equal(unknown.Code, wrong.Code);
        Assert.Equal(unknown.Str("message"), wrong.Str("message"));
    }

    [Fact]
    public async Task Login_requires_both_fields()
    {
        var res = await new Client(Api.Anonymous()).Post("/api/v1/auth/login", new { username = "admin" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.Status);
        Assert.Equal("password", res.Json["errors"]![0]!["field"]!.GetValue<string>());
    }

    [Fact]
    public async Task Username_is_case_insensitive()
    {
        var res = await new Client(Api.Anonymous()).Post("/api/v1/auth/login", new { username = "ADMIN", password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, res.Status);
    }

    [Theory]
    [InlineData("/api/v1/teams")]
    [InlineData("/api/v1/incidents")]
    [InlineData("/api/v1/dashboard/summary")]
    [InlineData("/api/v1/auth/me")]
    public async Task Protected_endpoints_require_a_token(string url)
    {
        var res = await new Client(Api.Anonymous()).Get(url);
        Assert.Equal(HttpStatusCode.Unauthorized, res.Status);
        Assert.Equal("UNAUTHENTICATED", res.Code);
    }

    [Fact]
    public async Task Garbage_token_is_unauthenticated()
    {
        var http = Api.Anonymous();
        http.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");
        Assert.Equal("UNAUTHENTICATED", (await new Client(http).Get("/api/v1/teams")).Code);
    }

    [Fact]
    public async Task Me_returns_the_current_user()
    {
        var res = await (await Api.As("resp.payments1")).Get("/api/v1/auth/me");
        Assert.Equal("resp.payments1", res.Str("username"));
    }

    [Fact]
    public async Task Error_shape_is_uniform_for_unknown_routes_and_wrong_methods()
    {
        var c = await Api.As("admin");
        var notFound = await c.Get("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, notFound.Status);
        Assert.Equal("NOT_FOUND", notFound.Code);
        Assert.False(string.IsNullOrEmpty(notFound.Str("traceId")));
        Assert.Equal(404, notFound.Int("status"));

        var wrongMethod = await c.Send(HttpMethod.Delete, "/api/v1/teams", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.Status);
        Assert.Equal("METHOD_NOT_ALLOWED", wrongMethod.Code);
    }

    [Fact]
    public async Task Malformed_json_is_400()
    {
        var res = await (await Api.As("reporter1")).PostRaw("/api/v1/incidents", "{not json");
        Assert.Equal(HttpStatusCode.BadRequest, res.Status);
        Assert.Equal("MALFORMED_REQUEST", res.Code);
    }

    [Fact]
    public async Task Check_order_role_before_lookup_before_scope_before_body()
    {
        var viewer = await Api.As("viewer1");
        // 403 (role) beats 404 (missing resource) and 400 (bad body).
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostRaw("/api/v1/incidents/9999/acknowledge", "{bad")).Status);
        // A role that may act gets 404 for a missing resource before the body is looked at.
        var cmd = await Api.As("cmd.platform");
        Assert.Equal(HttpStatusCode.NotFound, (await cmd.PostRaw("/api/v1/incidents/9999/acknowledge", "{bad")).Status);

        var incident = await CreateIncident();
        // Scope (other team) beats malformed body.
        var other = await Api.As("cmd.payments");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostRaw(Url(incident, "/acknowledge"), "{bad")).Status);
        // Own team: malformed body now surfaces as 400, then 422 for validation.
        Assert.Equal(HttpStatusCode.BadRequest, (await cmd.PostRaw(Url(incident, "/mitigate"), "{bad")).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cmd.Post(Url(incident, "/mitigate"), new { })).Status);
    }

    [Theory]
    [InlineData("/api/v1/incidents/abc")]
    [InlineData("/api/v1/incidents/0")]
    [InlineData("/api/v1/incidents/-3")]
    [InlineData("/api/v1/incidents/777")]
    public async Task Bad_or_missing_path_ids_are_404(string url) =>
        Assert.Equal(HttpStatusCode.NotFound, (await (await Api.As("admin")).Get(url)).Status);

    [Fact]
    public async Task Reference_data_is_seeded_as_specified()
    {
        var c = await Api.As("viewer1");
        Assert.Equal(3, (await c.Get("/api/v1/teams")).Json.AsArray().Count);
        Assert.Equal(10, (await c.Get("/api/v1/users")).Json.AsArray().Count);
        Assert.Equal(6, (await c.Get("/api/v1/services")).Json.AsArray().Count);
        var active = (await c.Get("/api/v1/services?active=true")).Json.AsArray();
        Assert.Equal(5, active.Count);
        Assert.Equal(422, (int)(await c.Get("/api/v1/services?active=maybe")).Status);
    }
}

public class TestModeTests
{
    [Fact]
    public async Task Test_endpoints_are_inert_unless_test_mode_is_enabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"incidentdesk-prod-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("S2Q_TEST_MODE", "false");
                b.UseSetting("Jwt:SigningKey", "a-production-style-signing-key-with-enough-length-123456");
                b.UseSetting("ConnectionStrings:Default", $"Data Source={path}");
            });
            var c = new Client(factory.CreateClient());
            Assert.Equal(HttpStatusCode.NotFound, (await c.Get("/api/v1/test/clock")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await c.Post("/api/v1/test/reset")).Status);
            Assert.Equal("NOT_FOUND", (await c.Post("/api/v1/test/clock/advance", new { minutes = 1 })).Code);
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")) File.Delete(f);
        }
    }

    [Fact]
    public void Startup_fails_without_a_signing_key_outside_test_mode()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("S2Q_TEST_MODE", "false");
            b.UseSetting("Jwt:SigningKey", "");
            b.UseSetting("ConnectionStrings:Default", "Data Source=" + Path.Combine(Path.GetTempPath(), $"x-{Guid.NewGuid():N}.db"));
        });
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
