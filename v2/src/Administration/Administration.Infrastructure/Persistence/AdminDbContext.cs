using Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Administration.Infrastructure.Persistence;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

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

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();
            entity.HasIndex(e => e.GoogleId, "users_google_id_key").IsUnique();
            entity.HasIndex(e => e.AppleId, "users_apple_id_key").IsUnique();

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

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");

            entity.HasMany(e => e.Roles)
                .WithMany(r => r.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "user_roles",
                    j => j.HasOne<Role>().WithMany().HasForeignKey("role_id").HasConstraintName("user_roles_role_id_fkey"),
                    j => j.HasOne<User>().WithMany().HasForeignKey("user_id").HasConstraintName("user_roles_user_id_fkey"),
                    j =>
                    {
                        j.ToTable("user_roles");
                        j.HasKey("user_id", "role_id").HasName("user_roles_pkey");
                    });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.HasIndex(e => e.Name, "roles_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasMany(e => e.Permissions)
                .WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "role_permissions",
                    j => j.HasOne<Permission>().WithMany().HasForeignKey("permission_id").HasConstraintName("role_permissions_permission_id_fkey"),
                    j => j.HasOne<Role>().WithMany().HasForeignKey("role_id").HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.ToTable("role_permissions");
                        j.HasKey("role_id", "permission_id").HasName("role_permissions_pkey");
                    });
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");

            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.HasIndex(e => e.Code, "permissions_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.Code)
                .HasMaxLength(128)
                .HasColumnName("code");

            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");

            // Fixed, code-defined catalog: each code corresponds to an actual authorization
            // check somewhere in the code, so new permissions arrive via migration, not via UI.
            entity.HasData(
                new Permission
                {
                    Id = 1,
                    Code = "administration.users.manage",
                    Description = "Create, update, and delete Administration user accounts."
                },
                new Permission
                {
                    Id = 2,
                    Code = "administration.roles.manage",
                    Description = "Create roles and assign permissions to them."
                });
        });
    }
}