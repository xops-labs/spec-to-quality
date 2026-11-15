using System.Text.RegularExpressions;
using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Application;

public sealed partial class ServiceCatalog(AppDbContext db, IClock clock, WriteGate gate, EventRecorder recorder)
{
    [GeneratedRegex("^[A-Z][A-Z0-9-]{1,19}$")]
    private static partial Regex KeyPattern();

    public async Task<List<ServiceDto>> ListAsync(bool? active)
    {
        IQueryable<Service> q = db.Services.AsNoTracking();
        if (active is { } a) q = q.Where(s => s.Active == a);
        return (await q.OrderBy(s => s.Id).ToListAsync()).Select(ServiceDto.From).ToList();
    }

    public async Task<ServiceDto> GetAsync(string rawId)
    {
        var id = Ids.Parse(rawId);
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id) ?? throw ApiException.NotFound("Service not found.");
        return ServiceDto.From(service);
    }

    public Task<ServiceDto> CreateAsync(User actor, RequestBody body) => gate.RunAsync(async () =>
    {
        IncidentService.RequireRole(actor, Operation.WriteService);
        var input = await ReadAsync(body, defaultActive: true);
        await EnsureUniqueAsync(input, null);

        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = new Service { Key = input.Key, Name = input.Name, OwnerTeamId = input.OwnerTeamId, Tier = input.Tier, Active = input.Active };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        recorder.Audit(null, actor.Id, AuditAction.ServiceCreated, EventRecorder.Details(("serviceId", service.Id)), now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceDto.From(service);
    });

    public Task<ServiceDto> ReplaceAsync(User actor, string rawId, RequestBody body) => gate.RunAsync(async () =>
    {
        IncidentService.RequireRole(actor, Operation.WriteService);
        var id = Ids.Parse(rawId);
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id) ?? throw ApiException.NotFound("Service not found.");
        var input = await ReadAsync(body, defaultActive: null);
        await EnsureUniqueAsync(input, service.Id);

        var changed = service.Key != input.Key || service.Name != input.Name || service.OwnerTeamId != input.OwnerTeamId
            || service.Tier != input.Tier || service.Active != input.Active;
        service.Key = input.Key;
        service.Name = input.Name;
        service.OwnerTeamId = input.OwnerTeamId;
        service.Tier = input.Tier;
        service.Active = input.Active;
        if (changed) recorder.Audit(null, actor.Id, AuditAction.ServiceUpdated, EventRecorder.Details(("serviceId", service.Id)), clock.UtcNow);
        await db.SaveChangesAsync();
        return ServiceDto.From(service);
    });

    private sealed record ServiceInput(string Key, string Name, long OwnerTeamId, int Tier, bool Active);

    private async Task<ServiceInput> ReadAsync(RequestBody body, bool? defaultActive)
    {
        var f = body.Parse();
        var key = f.String("key", 1, 100);
        if (key is not null && !KeyPattern().IsMatch(key)) f.Add("key", "invalid_format", "key must match ^[A-Z][A-Z0-9-]{1,19}$.");
        var name = f.String("name", 2, 80);
        var ownerTeamId = f.Integer("ownerTeamId");
        var tier = f.Integer("tier", min: 1, max: 3);
        var active = f.Boolean("active", required: defaultActive is null) ?? defaultActive;
        if (ownerTeamId is { } t && !await db.Teams.AnyAsync(x => x.Id == t)) f.Add("ownerTeamId", "not_found", "Team does not exist.");
        f.ThrowIfInvalid();
        return new ServiceInput(key!, name!, ownerTeamId!.Value, (int)tier!.Value, active!.Value);
    }

    private async Task EnsureUniqueAsync(ServiceInput input, long? excludeId)
    {
        var others = await db.Services.AsNoTracking().Where(s => s.Id != excludeId).Select(s => new { s.Key, s.Name }).ToListAsync();
        if (others.Any(o => o.Key == input.Key) || others.Any(o => string.Equals(o.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw ApiException.Conflict("DUPLICATE_SERVICE", "A service with this key or name already exists.");
        }
    }
}
