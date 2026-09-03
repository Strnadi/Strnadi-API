using Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Administration.Infrastructure.Persistence;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public virtual DbSet<AdminUser> Users { get; set; }

    // Columns are "timestamp without time zone", but application code sets them with
    // DateTime.UtcNow (Kind=Utc); Npgsql rejects that mismatch, so strip/restore the Kind
    // on the way in/out instead of touching every call site.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseOpenIddict();
        
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");

            entity.HasKey(e => e.Id).HasName("admin_users_pkey");

            entity.HasIndex(e => e.Email, "admin_users_email_key").IsUnique();
            entity.HasIndex(e => e.GoogleId, "admin_users_google_id_key").IsUnique();
            entity.HasIndex(e => e.AppleId, "admin_users_apple_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");

            entity.Property(e => e.IsEmailConfirmed)
                .HasDefaultValue(false)
                .HasColumnName("is_email_confirmed");

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");

            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .HasColumnName("google_id");

            entity.Property(e => e.AppleId)
                .HasMaxLength(255)
                .HasColumnName("apple_id");

            entity.Property(e => e.Role)
                .HasMaxLength(32)
                .HasDefaultValue("user")
                .HasColumnName("role");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
        });
    }
}
