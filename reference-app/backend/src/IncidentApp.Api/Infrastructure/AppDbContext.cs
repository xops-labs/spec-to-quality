using IncidentApp.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IncidentApp.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
        configurationBuilder.Properties<Role>().HaveConversion<string>();
        configurationBuilder.Properties<Severity>().HaveConversion<string>();
        configurationBuilder.Properties<IncidentStatus>().HaveConversion<string>();
        configurationBuilder.Properties<ActionItemStatus>().HaveConversion<string>();
        configurationBuilder.Properties<AuditAction>().HaveConversion<string>();
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Team>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
        });
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
        });
        b.Entity<Service>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.OwnerTeamId);
        });
        b.Entity<Incident>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.ResolutionSummary).HasMaxLength(2000);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.ServiceId, x.Status });
            e.HasIndex(x => x.CreatedAt);
            e.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeId);
        });
        b.Entity<Note>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(2000);
            e.HasIndex(x => x.IncidentId);
            e.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId);
        });
        b.Entity<ActionItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => x.IncidentId);
            e.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId);
        });
        b.Entity<AuditEvent>(e =>
        {
            e.HasIndex(x => x.IncidentId);
            e.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId);
        });
        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasIndex(x => x.RecipientId);
            e.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientId);
        });
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v == null ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
        v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}
