using IncidentApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Infrastructure;

#pragma warning disable EF1002 // SQL below only interpolates compile-time constant table names.
public static class DatabaseInitializer
{
    public const string FixturePassword = "S2Q-Demo-2026!";

    private static readonly Lazy<string[]> SeedHashes = new(() =>
        Enumerable.Range(0, 10).Select(_ => PasswordHasher.Hash(FixturePassword)).ToArray());

    private static readonly string[] Tables =
        ["Notifications", "AuditEvents", "ActionItems", "Notes", "Incidents", "Services", "Users", "Teams"];

    /// <summary>Creates/migrates the schema and seeds if the database has no users.</summary>
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (db.Database.IsNpgsql()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync(); // SQLite fallback (local dev/tests) builds the schema from the model.

        if (!await db.Users.AnyAsync()) await SeedAsync(db);
    }

    public static async Task ResetAsync(AppDbContext db)
    {
        if (db.Database.IsNpgsql())
        {
            var list = string.Join(", ", Tables.Select(t => $"\"{t}\""));
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {list} RESTART IDENTITY CASCADE");
        }
        else
        {
            foreach (var t in Tables) await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{t}\"");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence");
        }
        db.ChangeTracker.Clear();
        await SeedAsync(db);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Teams.AddRange(
            new Team { Id = 1, Name = "Platform" },
            new Team { Id = 2, Name = "Payments" },
            new Team { Id = 3, Name = "Identity" });
        db.Services.AddRange(
            new Service { Id = 1, Key = "API-GATEWAY", Name = "API Gateway", OwnerTeamId = 1, Tier = 1, Active = true },
            new Service { Id = 2, Key = "DATA-PIPELINE", Name = "Data Pipeline", OwnerTeamId = 1, Tier = 3, Active = true },
            new Service { Id = 3, Key = "PAYMENTS-CORE", Name = "Payments Core", OwnerTeamId = 2, Tier = 1, Active = true },
            new Service { Id = 4, Key = "BILLING-EXPORT", Name = "Billing Export", OwnerTeamId = 2, Tier = 3, Active = true },
            new Service { Id = 5, Key = "AUTH-SERVICE", Name = "Auth Service", OwnerTeamId = 3, Tier = 2, Active = true },
            new Service { Id = 6, Key = "LEGACY-REPORTS", Name = "Legacy Reports", OwnerTeamId = 3, Tier = 3, Active = false });

        (long Id, string Username, string Display, Role Role, long? Team)[] users =
        [
            (1, "admin", "Ada Admin", Role.Admin, null),
            (2, "cmd.platform", "Casey Platform", Role.Commander, 1),
            (3, "cmd.payments", "Chris Payments", Role.Commander, 2),
            (4, "cmd.identity", "Cam Identity", Role.Commander, 3),
            (5, "resp.platform1", "Robin Platform", Role.Responder, 1),
            (6, "resp.platform2", "Riley Platform", Role.Responder, 1),
            (7, "resp.payments1", "Rene Payments", Role.Responder, 2),
            (8, "resp.identity1", "Remy Identity", Role.Responder, 3),
            (9, "reporter1", "Rory Reporter", Role.Reporter, null),
            (10, "viewer1", "Val Viewer", Role.Viewer, null),
        ];
        for (var i = 0; i < users.Length; i++)
        {
            var u = users[i];
            db.Users.Add(new User
            {
                Id = u.Id, Username = u.Username, DisplayName = u.Display, Role = u.Role, TeamId = u.Team,
                PasswordHash = SeedHashes.Value[i],
            });
        }
        await db.SaveChangesAsync();

        if (db.Database.IsNpgsql())
        {
            // Explicit ids were inserted; move the identity sequences past them.
            foreach (var t in new[] { "Teams", "Users", "Services" })
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT setval(pg_get_serial_sequence('\"{t}\"', 'Id'), (SELECT MAX(\"Id\") FROM \"{t}\"))");
            }
        }
        db.ChangeTracker.Clear();
    }
}
