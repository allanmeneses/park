using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Parking.Domain;

namespace Parking.Infrastructure.Persistence.Identity;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    private static readonly NpgsqlNullNameTranslator EnumNames = new();

    public DbSet<ParkingIdentityUser> Users => Set<ParkingIdentityUser>();
    public DbSet<RefreshTokenRow> RefreshTokens => Set<RefreshTokenRow>();
    public DbSet<LojistaInviteRow> LojistaInvites => Set<LojistaInviteRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<UserRole>("public", "user_role", EnumNames);
        modelBuilder.Entity<ParkingIdentityUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasColumnType("user_role");
        });
        modelBuilder.Entity<RefreshTokenRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.TokenHash).IsRequired();
        });

        modelBuilder.Entity<LojistaInviteRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MerchantCode).HasMaxLength(10).IsRequired();
            e.HasIndex(x => x.MerchantCode).IsUnique();
            e.Property(x => x.ActivationCodeHash).IsRequired();
        });
    }
}
