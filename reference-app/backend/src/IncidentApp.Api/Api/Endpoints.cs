using System.Security.Claims;
using IncidentApp.Api.Application;
using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Api;

public static class Endpoints
{
    private static Task<RequestBody> Body(HttpContext ctx) => RequestBody.ReadAsync(ctx.Request);

    public static void MapApi(this WebApplication app, bool testMode)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok" }));
        var api = app.MapGroup("/api/v1");
        api.MapGet("/health", () => Results.Json(new { status = "ok" }));

        api.MapPost("/auth/login", async (HttpContext ctx, AuthService auth) =>
            Results.Json(await auth.LoginAsync(await Body(ctx))));

        if (testMode) MapTestEndpoints(api);

        var secured = api.MapGroup("").RequireAuthorization();
        secured.AddEndpointFilter(LoadCurrentUser);

        MapReferenceData(secured);
        MapIncidents(secured);
        MapSecondary(secured);
    }

    private static async ValueTask<object?> LoadCurrentUser(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        var sub = http.User.FindFirstValue("sub");
        if (!long.TryParse(sub, out var userId)) throw ApiException.Unauthenticated();
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId) ?? throw ApiException.Unauthenticated();
        http.RequestServices.GetRequiredService<CurrentUser>().Set(user);
        return await next(ctx);
    }

    private static void MapTestEndpoints(RouteGroupBuilder api)
    {
        var test = api.MapGroup("/test");
        test.MapGet("/clock", (TestSupportService t) => Results.Json(t.GetClock()));
        test.MapPut("/clock", async (HttpContext ctx, TestSupportService t) => Results.Json(await t.SetClockAsync(await Body(ctx))));
        test.MapPost("/clock/advance", async (HttpContext ctx, TestSupportService t) => Results.Json(await t.AdvanceClockAsync(await Body(ctx))));
        test.MapPost("/reset", async (TestSupportService t) =>
        {
            await t.ResetAsync();
            return Results.NoContent();
        });
    }

    private static void MapReferenceData(RouteGroupBuilder g)
    {
        g.MapGet("/auth/me", (CurrentUser me) => Results.Json(UserDto.From(me.User)));
        g.MapGet("/teams", async (ReadServices r) => Results.Json(await r.TeamsAsync()));
        g.MapGet("/users", async (ReadServices r) => Results.Json(await r.UsersAsync()));

        g.MapGet("/services", async (HttpContext ctx, ServiceCatalog s) =>
        {
            var q = new QueryReader(ctx.Request.Query);
            var active = q.Bool("active");
            q.ThrowIfInvalid();
            return Results.Json(await s.ListAsync(active));
        });
        g.MapGet("/services/{id}", async (string id, ServiceCatalog s) => Results.Json(await s.GetAsync(id)));
        g.MapPost("/services", async (HttpContext ctx, CurrentUser me, ServiceCatalog s) =>
        {
            var created = await s.CreateAsync(me.User, await Body(ctx));
            return Results.Json(created, statusCode: StatusCodes.Status201Created, contentType: null).WithLocation($"/api/v1/services/{created.Id}");
        });
        g.MapPut("/services/{id}", async (HttpContext ctx, string id, CurrentUser me, ServiceCatalog s) =>
            Results.Json(await s.ReplaceAsync(me.User, id, await Body(ctx))));
    }

    private static void MapIncidents(RouteGroupBuilder g)
    {
        g.MapPost("/incidents", async (HttpContext ctx, CurrentUser me, IncidentService svc) =>
        {
            var created = await svc.CreateAsync(me.User, await Body(ctx));
            return Results.Json(created, statusCode: StatusCodes.Status201Created).WithLocation($"/api/v1/incidents/{created.Id}");
        });
        g.MapGet("/incidents", async (HttpContext ctx, IncidentService svc) =>
            Results.Json(await svc.ListAsync(new QueryReader(ctx.Request.Query))));
        g.MapGet("/incidents/{id}", async (string id, IncidentService svc) => Results.Json(await svc.GetAsync(id)));
        g.MapPatch("/incidents/{id}", async (HttpContext ctx, string id, CurrentUser me, IncidentService svc) =>
            Results.Json(await svc.PatchAsync(me.User, id, await Body(ctx))));

        foreach (var (path, action) in new[]
        {
            ("acknowledge", TransitionAction.Acknowledge), ("mitigate", TransitionAction.Mitigate), ("resolve", TransitionAction.Resolve),
            ("close", TransitionAction.Close), ("reopen", TransitionAction.Reopen),
        })
        {
            var transition = action;
            g.MapPost($"/incidents/{{id}}/{path}", async (HttpContext ctx, string id, CurrentUser me, IncidentService svc) =>
                Results.Json(await svc.TransitionAsync(me.User, id, transition, await Body(ctx))));
        }

        g.MapPost("/incidents/{id}/assign", async (HttpContext ctx, string id, CurrentUser me, IncidentService svc) =>
            Results.Json(await svc.AssignAsync(me.User, id, await Body(ctx))));
        g.MapPost("/incidents/{id}/severity", async (HttpContext ctx, string id, CurrentUser me, IncidentService svc) =>
            Results.Json(await svc.ChangeSeverityAsync(me.User, id, await Body(ctx))));
    }

    private static void MapSecondary(RouteGroupBuilder g)
    {
        g.MapGet("/incidents/{id}/notes", async (string id, NoteAndActionItemService s) => Results.Json(await s.ListNotesAsync(id)));
        g.MapPost("/incidents/{id}/notes", async (HttpContext ctx, string id, CurrentUser me, NoteAndActionItemService s) =>
            Results.Json(await s.AddNoteAsync(me.User, id, await Body(ctx)), statusCode: StatusCodes.Status201Created));

        g.MapGet("/incidents/{id}/action-items", async (string id, NoteAndActionItemService s) => Results.Json(await s.ListActionItemsAsync(id)));
        g.MapPost("/incidents/{id}/action-items", async (HttpContext ctx, string id, CurrentUser me, NoteAndActionItemService s) =>
            Results.Json(await s.CreateActionItemAsync(me.User, id, await Body(ctx)), statusCode: StatusCodes.Status201Created));
        g.MapPatch("/incidents/{id}/action-items/{itemId}", async (HttpContext ctx, string id, string itemId, CurrentUser me, NoteAndActionItemService s) =>
            Results.Json(await s.UpdateActionItemAsync(me.User, id, itemId, await Body(ctx))));

        g.MapGet("/incidents/{id}/audit", async (string id, CurrentUser me, ReadServices r) => Results.Json(await r.IncidentAuditAsync(me.User, id)));
        g.MapGet("/audit", async (HttpContext ctx, CurrentUser me, ReadServices r) =>
            Results.Json(await r.GlobalAuditAsync(me.User, new QueryReader(ctx.Request.Query))));
        g.MapGet("/notifications", async (HttpContext ctx, CurrentUser me, ReadServices r) =>
            Results.Json(await r.NotificationsAsync(me.User, new QueryReader(ctx.Request.Query))));

        g.MapPost("/sla/evaluate", async (CurrentUser me, SlaService sla) =>
        {
            IncidentService.RequireRole(me.User, Operation.EvaluateSla);
            return Results.Json(new { escalationsApplied = await sla.EvaluateAllAsync() });
        });
        g.MapGet("/dashboard/summary", async (ReadServices r) => Results.Json(await r.DashboardAsync()));
    }

    private static IResult WithLocation(this IResult result, string location) => new LocationResult(result, location);

    private sealed class LocationResult(IResult inner, string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Location = location;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
