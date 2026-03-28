using Microsoft.EntityFrameworkCore;

namespace Parking.Infrastructure.Persistence.Audit;

public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEventRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ParkingId, x.CreatedAt });
            e.Property(x => x.EntityType).IsRequired();
            e.Property(x => x.Action).IsRequired();
            e.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        });
    }
}
