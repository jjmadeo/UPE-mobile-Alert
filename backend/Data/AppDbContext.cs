using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Domain;

namespace MobileAlert.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Firefighter> Firefighters => Set<Firefighter>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AlertRecord> Alerts => Set<AlertRecord>();
    public DbSet<AlertResponseRecord> AlertResponses => Set<AlertResponseRecord>();
    public DbSet<ApiKeyRecord> ApiKeys => Set<ApiKeyRecord>();
    public DbSet<WebhookSubscription> Webhooks => Set<WebhookSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Institution>(e =>
        {
            e.HasIndex(i => i.Code).IsUnique();
        });

        modelBuilder.Entity<Firefighter>(e =>
        {
            // Username único DENTRO de la institución, no global — dos
            // instituciones pueden tener cada una su propio "juan".
            e.HasIndex(f => new { f.InstitutionId, f.Username }).IsUnique();
        });

        modelBuilder.Entity<DeviceToken>(e =>
        {
            e.HasIndex(d => d.FcmToken).IsUnique();
        });

        modelBuilder.Entity<AlertResponseRecord>(e =>
        {
            // Un bombero responde una vez por alerta — si vuelve a
            // contestar, se actualiza (ver AlertService.RespondAsync), no
            // se apila una fila nueva.
            e.HasIndex(r => new { r.AlertId, r.FirefighterId }).IsUnique();

            e.Property(r => r.WebhookRequestPayload).HasColumnType("jsonb");
            // WebhookResponsePayload queda texto plano a propósito — ver
            // el comentario en AlertResponseRecord (no controlamos qué
            // devuelve el servidor del cuartel).
        });

        modelBuilder.Entity<ApiKeyRecord>(e =>
        {
            e.HasIndex(k => k.KeyHash).IsUnique();
        });

        modelBuilder.Entity<AlertRecord>(e =>
        {
            // Global, no por institución: el cuartel lo genera como UUID
            // random, así que ya es único de por sí — el índice es para
            // poder detectar rápido un replay (ver AlertService.CreateAsync)
            // y para que la constraint de la base lo garantice también si
            // dos requests concurrentes llegaran con el mismo valor.
            e.HasIndex(a => a.CorrelationId).IsUnique();

            // jsonb (no text plano): son JSON de verdad que armamos
            // nosotros mismos, Postgres los valida y los deja queryables
            // con operadores nativos si algún día hace falta.
            e.Property(a => a.RequestPayload).HasColumnType("jsonb");
            e.Property(a => a.ResponsePayload).HasColumnType("jsonb");
        });
    }

    /// <summary>Completa CreatedAt/UpdatedAt (ver IAuditable) en TODAS las
    /// entidades que lo implementan, en cada save — centralizado acá para
    /// que ningún servicio se tenga que acordar de hacerlo a mano.</summary>
    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
