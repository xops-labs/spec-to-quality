using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IncidentApp.Tests.Api;

/// <summary>Boots the API in test mode on a throw-away SQLite database. Shared by all API tests (run sequentially).</summary>
public sealed class ApiFixture : IDisposable
{
    public const string Password = "S2Q-Demo-2026!";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"incidentdesk-tests-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, string> _tokens = [];
    public WebApplicationFactory<Program> Factory { get; }

    public ApiFixture()
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("S2Q_TEST_MODE", "true");
            // Set S2Q_TEST_CONNECTION to a PostgreSQL connection string to run the same suite against PostgreSQL.
            b.UseSetting("ConnectionStrings:Default", Environment.GetEnvironmentVariable("S2Q_TEST_CONNECTION") ?? $"Data Source={_dbPath}");
        });
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task ResetAsync()
    {
        using var c = Anonymous();
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/v1/test/reset", null)).StatusCode);
    }

    public async Task<Client> As(string username)
    {
        if (!_tokens.TryGetValue(username, out var token))
        {
            using var c = Anonymous();
            var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { username, password = Password });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            token = (await res.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
            _tokens[username] = token;
        }
        var http = Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Client(http);
    }

    public async Task SetClockAsync(string iso) => Assert.Equal(HttpStatusCode.OK, (await new Client(Anonymous()).Put("/api/v1/test/clock", new { now = iso })).Status);

    public async Task AdvanceAsync(int minutes) => Assert.Equal(HttpStatusCode.OK, (await new Client(Anonymous()).Post("/api/v1/test/clock/advance", new { minutes })).Status);

    public void Dispose()
    {
        Factory.Dispose();
        foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_dbPath) + "*")) File.Delete(f);
    }
}

public sealed record Reply(HttpStatusCode Status, JsonNode? Body, HttpResponseHeaders Headers)
{
    public JsonNode Json => Body!;
    public string? Code => Body?["code"]?.GetValue<string>();
    public int Int(string name) => Body![name]!.GetValue<int>();
    public string Str(string name) => Body![name]!.GetValue<string>();
}

public sealed class Client(HttpClient http)
{
    public Task<Reply> Get(string url) => Send(HttpMethod.Get, url, null);
    public Task<Reply> Post(string url, object? body = null) => Send(HttpMethod.Post, url, body ?? new { });
    public Task<Reply> Put(string url, object body) => Send(HttpMethod.Put, url, body);
    public Task<Reply> Patch(string url, object body) => Send(HttpMethod.Patch, url, body);

    public async Task<Reply> PostRaw(string url, string rawJson)
    {
        var res = await http.PostAsync(url, new StringContent(rawJson, Encoding.UTF8, "application/json"));
        return await ToReply(res);
    }

    public async Task<Reply> Send(HttpMethod method, string url, object? body)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await ToReply(await http.SendAsync(req));
    }

    private static async Task<Reply> ToReply(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return new Reply(res.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), res.Headers);
    }
}

[CollectionDefinition("api", DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;

/// <summary>Base class: resets the database and clock before every test.</summary>
[Collection("api")]
public abstract class ApiTestBase(ApiFixture api) : IAsyncLifetime
{
    protected ApiFixture Api { get; } = api;

    public Task InitializeAsync() => Api.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected const string T0 = "2026-03-01T12:00:00.000Z";

    /// <summary>Creates an incident as reporter1 on a service (default API Gateway, tier 1) and returns its JSON.</summary>
    protected async Task<JsonNode> CreateIncident(string title = "Gateway latency spike", string severity = "SEV2", int serviceId = 1, string user = "reporter1")
    {
        var res = await (await Api.As(user)).Post("/api/v1/incidents", new { title, description = "details", serviceId, severity });
        Assert.Equal(HttpStatusCode.Created, res.Status);
        return res.Json;
    }

    protected static string Url(JsonNode incident, string suffix = "") => $"/api/v1/incidents/{incident["id"]!.GetValue<int>()}{suffix}";

    /// <summary>Applies lifecycle steps as the commander of the incident's team.</summary>
    protected async Task Drive(JsonNode incident, params string[] steps)
    {
        var cmd = await Api.As(incident["teamId"]!.GetValue<int>() switch { 1 => "cmd.platform", 2 => "cmd.payments", _ => "cmd.identity" });
        foreach (var step in steps)
        {
            object body = step switch
            {
                "mitigate" => new { message = "Rolled back the change" },
                "resolve" => new { resolutionSummary = "Fixed by rollback of change" },
                "reopen" => new { reason = "Issue came back" },
                _ => new { },
            };
            var res = await cmd.Post(Url(incident, "/" + step), body);
            Assert.True(res.Status == HttpStatusCode.OK, $"{step} failed: {res.Body}");
        }
    }
}

public static class JsonNodeExtensions
{
    public static int Int(this JsonNode node, string name) => node[name]!.GetValue<int>();
    public static string Str(this JsonNode node, string name) => node[name]!.GetValue<string>();
}
