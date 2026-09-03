using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tenant.Domain.Entities;

namespace Tenant.Infrastructure.Persistence;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Columns are "timestamp without time zone" (Database-First from the existing schema), but
    // application code sets them with DateTime.UtcNow (Kind=Utc); Npgsql rejects that mismatch,
    // so strip/restore the Kind on the way in/out instead of touching every call site.
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

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<AchievementContent> AchievementContents { get; set; }

    public virtual DbSet<Article> Articles { get; set; }

    public virtual DbSet<ArticleAttachment> ArticleAttachments { get; set; }

    public virtual DbSet<ArticleCategory> ArticleCategories { get; set; }

    public virtual DbSet<ArticleCategoryAssignment> ArticleCategoryAssignments { get; set; }

    public virtual DbSet<ArticleCategoryTranslation> ArticleCategoryTranslations { get; set; }

    public virtual DbSet<ArticleTranslation> ArticleTranslations { get; set; }

    public virtual DbSet<DetectedDialect> DetectedDialects { get; set; }

    public virtual DbSet<Device> Devices { get; set; }

    public virtual DbSet<Dialect> Dialects { get; set; }

    public virtual DbSet<DialectsToDo> DialectsToDos { get; set; }

    public virtual DbSet<FilteredRecordingPart> FilteredRecordingParts { get; set; }

    public virtual DbSet<Photo> Photos { get; set; }

    public virtual DbSet<Recording> Recordings { get; set; }

    public virtual DbSet<RecordingPart> RecordingParts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("achievements_pkey");

            entity.ToTable("achievements");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ImagePath).HasColumnName("image_path");
            entity.Property(e => e.Sql)
                .HasMaxLength(255)
                .HasColumnName("sql");
        });

        modelBuilder.Entity<AchievementContent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("achievement_content_pkey");

            entity.ToTable("achievement_content");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .HasColumnName("language_code");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.Achievement).WithMany(p => p.AchievementContents)
                .HasForeignKey(d => d.AchievementId)
                .HasConstraintName("achievement_content_achievement_id_fkey");
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("articles_pkey");

            entity.ToTable("articles");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
        });

        modelBuilder.Entity<ArticleAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("article_attachments_pkey");

            entity.ToTable("article_attachments");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArticleId).HasColumnName("article_id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");

            entity.HasOne(d => d.Article).WithMany(p => p.ArticleAttachments)
                .HasForeignKey(d => d.ArticleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("article_attachments_article_fkey");
        });

        modelBuilder.Entity<ArticleCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("article_categories_pkey");

            entity.ToTable("article_categories");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Label)
                .HasMaxLength(255)
                .HasColumnName("label");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Order).HasColumnName("order");
        });

        modelBuilder.Entity<ArticleCategoryAssignment>(entity =>
        {
            entity.HasKey(e => new { e.ArticleId, e.CategoryId }).HasName("article_category_assignment_pkey");

            entity.ToTable("article_category_assignment");

            entity.Property(e => e.ArticleId).HasColumnName("article_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Order).HasColumnName("order");

            entity.HasOne(d => d.Article).WithMany(p => p.ArticleCategoryAssignments)
                .HasForeignKey(d => d.ArticleId)
                .HasConstraintName("article_category_assignment_article_id_fkey");

            entity.HasOne(d => d.Category).WithMany(p => p.ArticleCategoryAssignments)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("article_category_assignment_category_id_fkey");
        });

        modelBuilder.Entity<ArticleCategoryTranslation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("article_category_translations_pkey");

            entity.ToTable("article_category_translations");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArticleCategoryId).HasColumnName("article_category_id");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(5)
                .HasColumnName("language_code");
            entity.Property(e => e.NameValue).HasColumnName("name_value");
            entity.Property(e => e.DescriptionValue).HasColumnName("description_value");

            entity.HasOne(d => d.ArticleCategory).WithMany(p => p.ArticleCategoryTranslations)
                .HasForeignKey(d => d.ArticleCategoryId)
                .HasConstraintName("article_category_translations_article_category_id_fkey");
        });

        modelBuilder.Entity<ArticleTranslation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("article_translations_pkey");

            entity.ToTable("article_translations");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArticleId).HasColumnName("article_id");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(5)
                .HasColumnName("language_code");
            entity.Property(e => e.NameValue).HasColumnName("name_value");
            entity.Property(e => e.DescriptionValue).HasColumnName("description_value");

            entity.HasOne(d => d.Article).WithMany(p => p.ArticleTranslations)
                .HasForeignKey(d => d.ArticleId)
                .HasConstraintName("article_translations_article_id_fkey");
        });

        modelBuilder.Entity<DetectedDialect>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("detected_dialects_pkey");

            entity.ToTable("detected_dialects");

            entity.HasIndex(e => e.FilteredRecordingPartId, "detected_dialects_filtered_part_id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConfirmedDialectId).HasColumnName("confirmed_dialect_id");
            entity.Property(e => e.FilteredRecordingPartId).HasColumnName("filtered_recording_part_id");
            entity.Property(e => e.PredictedDialectId).HasColumnName("predicted_dialect_id");
            entity.Property(e => e.UserGuessDialectId).HasColumnName("user_guess_dialect_id");

            entity.HasOne(d => d.ConfirmedDialect).WithMany(p => p.DetectedDialectConfirmedDialects)
                .HasForeignKey(d => d.ConfirmedDialectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("detected_dialects_confirmed_dialect_id_fkey");

            entity.HasOne(d => d.FilteredRecordingPart).WithOne(p => p.DetectedDialect)
                .HasForeignKey<DetectedDialect>(d => d.FilteredRecordingPartId)
                .HasConstraintName("detected_dialects_filtered_recording_part_id_fkey");

            entity.HasOne(d => d.UserGuessDialect).WithMany(p => p.DetectedDialectUserGuessDialects)
                .HasForeignKey(d => d.UserGuessDialectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("detected_dialects_user_guess_dialect_id_fkey");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("devices_pkey");

            entity.ToTable("devices");

            entity.HasIndex(e => e.FcmToken, "devices_fcm_token_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DeviceModel)
                .HasMaxLength(255)
                .HasColumnName("device_model");
            entity.Property(e => e.DevicePlatform)
                .HasMaxLength(255)
                .HasColumnName("device_platform");
            entity.Property(e => e.FcmToken)
                .HasMaxLength(255)
                .HasColumnName("fcm_token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Devices)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_devices_user");
        });

        modelBuilder.Entity<Dialect>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("dialects_pkey");

            entity.ToTable("dialects");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Color)
                .HasMaxLength(7)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("color");
            entity.Property(e => e.DialectCode)
                .HasMaxLength(15)
                .HasColumnName("dialect_code");
            entity.Property(e => e.HintOrder).HasColumnName("hint_order");
        });

        modelBuilder.Entity<DialectsToDo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("dialects_to_do");

            entity.Property(e => e.ConfirmedDialect)
                .HasMaxLength(15)
                .HasColumnName("confirmed_dialect");
            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_date");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.FilteredEndDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("filtered_end_date");
            entity.Property(e => e.FilteredRecordingPartId).HasColumnName("filtered_recording_part_id");
            entity.Property(e => e.FilteredStartDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("filtered_start_date");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasColumnName("first_name");
            entity.Property(e => e.GpsLatitudeStart).HasColumnName("gps_latitude_start");
            entity.Property(e => e.GpsLongitudeStart).HasColumnName("gps_longitude_start");
            entity.Property(e => e.GuessDialect)
                .HasMaxLength(15)
                .HasColumnName("guess_dialect");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasColumnName("last_name");
            entity.Property(e => e.ProbabilityVector)
                .HasMaxLength(255)
                .HasColumnName("probability_vector");
            entity.Property(e => e.RecordingId).HasColumnName("recording_id");
            entity.Property(e => e.RecordingPartsId).HasColumnName("recording_parts_id");
            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_date");
        });

        modelBuilder.Entity<FilteredRecordingPart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("filtered_recording_parts_pkey");

            entity.ToTable("filtered_recording_parts");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EndDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_date");
            entity.Property(e => e.ProbabilityVector)
                .HasMaxLength(255)
                .HasColumnName("probability_vector");
            entity.Property(e => e.RecordingId).HasColumnName("recording_id");
            entity.Property(e => e.RepresentantFlag).HasColumnName("representant_flag");
            entity.Property(e => e.StartDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_date");
            entity.Property(e => e.State)
                .HasDefaultValue((short)0)
                .HasColumnName("state");

            entity.HasOne(d => d.Recording).WithMany(p => p.FilteredRecordingParts)
                .HasForeignKey(d => d.RecordingId)
                .HasConstraintName("filtered_recording_parts_recording_id_fkey");
        });

        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("photos_pkey");

            entity.ToTable("photos");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.Format)
                .HasMaxLength(10)
                .HasColumnName("format");
            entity.Property(e => e.RecordingId).HasColumnName("recording_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Recording).WithMany(p => p.Photos)
                .HasForeignKey(d => d.RecordingId)
                .HasConstraintName("photos_recording_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Photos)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_photos_user");
        });

        modelBuilder.Entity<Recording>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("recordings_pkey");

            entity.ToTable("recordings");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ByApp).HasColumnName("by_app");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Device)
                .HasMaxLength(255)
                .HasColumnName("device");
            entity.Property(e => e.EstimatedBirdsCount).HasColumnName("estimated_birds_count");
            entity.Property(e => e.ExpectedPartsCount).HasColumnName("expected_parts_count");
            entity.Property(e => e.Legacy).HasColumnName("legacy");
            entity.Property(e => e.Name)
                .HasMaxLength(49)
                .HasColumnName("name");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.NotePost).HasColumnName("note_post");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Recordings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_recordings_user");
        });

        modelBuilder.Entity<RecordingPart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("recording_parts_pkey");

            entity.ToTable("recording_parts");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_date");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.GpsLatitudeEnd).HasColumnName("gps_latitude_end");
            entity.Property(e => e.GpsLatitudeStart).HasColumnName("gps_latitude_start");
            entity.Property(e => e.GpsLongitudeEnd).HasColumnName("gps_longitude_end");
            entity.Property(e => e.GpsLongitudeStart).HasColumnName("gps_longitude_start");
            entity.Property(e => e.Length).HasColumnName("length");
            entity.Property(e => e.RecordingId).HasColumnName("recording_id");
            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_date");

            entity.HasOne(d => d.Recording).WithMany(p => p.RecordingParts)
                .HasForeignKey(d => d.RecordingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("recording_parts_recording_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Nickname, "users_nickname_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Appleid)
                .HasMaxLength(255)
                .HasColumnName("appleid");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Consent).HasColumnName("consent");
            entity.Property(e => e.CreationDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("creation_date");
            entity.Property(e => e.Deleted).HasColumnName("deleted");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasColumnName("first_name");
            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .HasColumnName("google_id");
            entity.Property(e => e.IsEmailVerified)
                .HasDefaultValue(false)
                .HasColumnName("is_email_verified");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasColumnName("last_name");
            entity.Property(e => e.Legacy).HasColumnName("legacy");
            entity.Property(e => e.Nickname)
                .HasMaxLength(50)
                .HasColumnName("nickname");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.PostCode).HasColumnName("post_code");
            entity.Property(e => e.Role)
                .HasMaxLength(32)
                .HasDefaultValueSql("'user'::character varying")
                .HasColumnName("role");
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_achievement_pkey");

            entity.ToTable("user_achievement");

            entity.HasIndex(e => new { e.UserId, e.AchievementId }, "user_achievement_unique").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Achievement).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.AchievementId)
                .HasConstraintName("user_achievement_achievement_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_achievement_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
