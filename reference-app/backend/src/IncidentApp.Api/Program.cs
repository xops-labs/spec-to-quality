using System.Security.Claims;
using System.Text.Json;
using IncidentApp.Api.Api;
using IncidentApp.Api.Application;
using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var testMode = string.Equals(builder.Configuration["S2Q_TEST_MODE"], "true", StringComparison.OrdinalIgnoreCase);
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]) && string.IsNullOrEmpty(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

// ---- configuration (environment variables only, spec section 2)
var connectionString = builder.Configuration.GetConnectionString("Default");
var usePostgres = connectionString is not null &&
    (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase));
connectionString ??= "Data Source=incidentdesk.db"; // SQLite fallback for local development/tests

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    if (!testMode) throw new InvalidOperationException("Jwt__SigningKey must be set when S2Q_TEST_MODE is not 'true'.");
    signingKey = "s2q-development-signing-key-test-mode-only-0123456789";
}

// ---- services
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new WireEnumConverterFactory());
    o.SerializerOptions.Converters.Add(new UtcMillisConverter());
    o.SerializerOptions.Converters.Add(new NullableUtcMillisConverter());
});
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (usePostgres) o.UseNpgsql(connectionString);
    else o.UseSqlite(connectionString);
});
builder.Services.AddSingleton<ControllableClock>();
builder.Services.AddSingleton<IClock>(sp => sp.GetRequiredService<ControllableClock>());
builder.Services.AddSingleton<WriteGate>();
builder.Services.AddSingleton(new JwtSettings(signingKey));
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<EventRecorder>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<IncidentMapper>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<NoteAndActionItemService>();
builder.Services.AddScoped<ServiceCatalog>();
builder.Services.AddScoped<ReadServices>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TestSupportService>();
if (!testMode) builder.Services.AddHostedService<SlaBackgroundService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new JwtSettings(signingKey).Key,
        ClockSkew = TimeSpan.FromSeconds(5),
    };
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            await ErrorWriter.WriteAsync(ctx.HttpContext, 401, "UNAUTHENTICATED", "A valid bearer token is required.");
        },
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

// ---- database
using (var scope = app.Services.CreateScope())
{
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

// ---- pipeline
app.UseMiddleware<ErrorMiddleware>();
app.UseStatusCodePages(async ctx =>
{
    var response = ctx.HttpContext.Response;
    if (!ctx.HttpContext.Request.Path.StartsWithSegments("/api") || response.ContentLength is > 0 || response.ContentType is not null) return;
    if (ErrorWriter.ForBodylessStatus(response.StatusCode) is { } e)
    {
        await ErrorWriter.WriteAsync(ctx.HttpContext, response.StatusCode, e.Code, e.Message);
    }
});
// SPA fallback: extension-less GET routes outside /api (e.g. /incidents/5) are served index.html. Done as a path
// rewrite rather than an endpoint so that unknown /api routes keep answering 404 and wrong methods 405.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    if (HttpMethods.IsGet(ctx.Request.Method) && !path.StartsWithSegments("/api") && !path.StartsWithSegments("/health")
        && string.IsNullOrEmpty(Path.GetExtension(path.Value)))
    {
        ctx.Request.Path = "/index.html";
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapApi(testMode);
app.Run();

public partial class Program;
