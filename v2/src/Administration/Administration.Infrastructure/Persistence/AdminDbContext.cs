using Administration.Domain.Entities;
using Administration.Domain.Services;
using Administration.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Administration.Infrastructure.Persistence;

public class AdminDbContext(DbContextOptions<AdminDbContext> options, IEncryptionService encryption)
    : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    private readonly EncryptedStringConverter _encryptedString = new(encryption);

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectMembership> ProjectMemberships { get; set; }

    // No DateTime->Unspecified conversion here, unlike Tenant: Administration's columns are
    // `timestamp with time zone` (see new_schema), which - the opposite of Tenant's `timestamp
    // without time zone` - requires Kind=Utc and rejects Unspecified. DateTime.UtcNow writes
    // straight through with no converter needed.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasIndex(u => u.NormalizedEmail).IsUnique();

            entity.Property(e => e.FirstName).HasConversion(_encryptedString);
            entity.Property(e => e.LastName).HasConversion(_encryptedString);

#pragma warning disable CS8620
            entity.Property(e => e.City).HasConversion(_encryptedString);
#pragma warning restore CS8620
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasIndex(r => r.NormalizedName).IsUnique(false);
            entity.HasIndex(r => new { r.NormalizedName, r.ProjectId }).IsUnique();
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(256);
            entity.Property(p => p.State).HasConversion<string>().HasMaxLength(32);

            entity.Property(p => p.Domain).HasMaxLength(256);
            entity.HasIndex(p => p.Domain).IsUnique();
            entity.Property(p => p.PhotoPath).HasMaxLength(512);
        });

        modelBuilder.Entity<ProjectMembership>(entity =>
        {
            entity.HasOne(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pm => pm.Project)
                .WithMany()
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pm => new { pm.UserId, pm.ProjectId }).IsUnique();
        });
    }
}