using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Platform.Shared.Infrastructure.Security;
using Platform.Shared.Kernel.Services;

namespace Administration.Infrastructure.Persistence;

public class AdminDbContext(DbContextOptions<AdminDbContext> options, IEncryptionService encryption)
    : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    private readonly EncryptedStringConverter _encryptedString = new(encryption);

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectMembership> ProjectMemberships { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentAcceptance> DocumentAcceptances { get; set; }

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
            entity.Property(e => e.PostCode).HasConversion(
                i => i == null ? null : encryption.Encrypt(i.Value.ToString()),
                s => s == null ? null : int.Parse(encryption.Decrypt(s)));

            entity.Property(e => e.PreferredLanguage).HasMaxLength(8);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasIndex(r => r.NormalizedName).IsUnique(false);
            entity.HasIndex(r => new { r.NormalizedName, r.ProjectId }).IsUnique();

            // The composite index above doesn't stop duplicate global-role names - Postgres treats
            // each NULL project_id as distinct from every other NULL in a unique index, so two
            // global roles named e.g. "SUPERADMIN" wouldn't collide there. This partial index closes
            // that gap for the ProjectId == null (global) case specifically.
            entity.HasIndex(r => r.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ix_roles_normalized_name_global")
                .HasFilter("project_id IS NULL");
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
            entity.Property(p => p.ApiDomain).HasMaxLength(256);
            // A rejected project's domain is reusable by a fresh submission instead of being
            // squatted forever - see Program.cs's CORS/OpenIddict sync for the other half of why
            // Draft/Rejected projects must never be treated as "real" (that query filters them
            // out entirely, this index just governs uniqueness).
            entity.HasIndex(p => p.Domain).IsUnique().HasFilter("state <> 'Rejected'");
            entity.Property(p => p.PhotoPath).HasMaxLength(512);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<Document>(entity =>
        {
            entity.Property(d => d.Type).HasConversion<string>().HasMaxLength(64);
            entity.Property(d => d.Title).HasMaxLength(256);
            entity.Property(d => d.IsRequired).HasDefaultValue(true);

            entity.HasOne(d => d.Project)
                .WithMany()
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.Type, d.Version, d.ProjectId }).IsUnique();

            entity.HasIndex(d => new { d.Type, d.ProjectId })
                .IsUnique()
                .HasFilter("is_active");
        });

        modelBuilder.Entity<DocumentAcceptance>(entity =>
        {
            entity.HasOne(da => da.User)
                .WithMany()
                .HasForeignKey(da => da.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(da => da.Document)
                .WithMany()
                .HasForeignKey(da => da.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(da => da.IpAddress).HasMaxLength(45);

            entity.HasIndex(da => new { da.UserId, da.DocumentId })
                .IsUnique()
                .HasFilter("revoked_at IS NULL");
        });
    }
}