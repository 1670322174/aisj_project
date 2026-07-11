using Microsoft.EntityFrameworkCore;
using InteriorDesignWeb.Models.Entities;

namespace InteriorDesignWeb.Data;

public class DesignHubContext : DbContext
{
    public DesignHubContext(DbContextOptions<DesignHubContext> options) : base(options)
    {
    }

    public DbSet<User> users { get; set; }
    public DbSet<Image> images { get; set; }
    public DbSet<Project> projects { get; set; }
    public DbSet<ProjectRoom> projectrooms { get; set; }
    public DbSet<ProjectImage> projectimages { get; set; }
    public DbSet<AiGenerationJob> aigenerationjobs { get; set; }
    public DbSet<AiGenerationJobImage> aigenerationjobimages { get; set; }
    public DbSet<UserQuota> userquotas { get; set; }
    public DbSet<UsageRecord> usagerecords { get; set; }
    public DbSet<ProjectActivity> projectactivities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Image>().ToTable("images");
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectRoom>().ToTable("projectrooms");
        modelBuilder.Entity<ProjectImage>().ToTable("projectimages");
        modelBuilder.Entity<AiGenerationJob>().ToTable("aigenerationjobs");
        modelBuilder.Entity<AiGenerationJobImage>().ToTable("aigenerationjobimages");
        modelBuilder.Entity<UserQuota>().ToTable("userquotas");
        modelBuilder.Entity<UsageRecord>().ToTable("usagerecords");
        modelBuilder.Entity<ProjectActivity>().ToTable("projectactivities");

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion(
                v => v.ToString(),
                v => (UserRole)Enum.Parse(typeof(UserRole), v))
            .HasMaxLength(20);

        modelBuilder.Entity<Project>()
            .HasOne(p => p.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasOne(p => p.CoverImage)
            .WithMany()
            .HasForeignKey(p => p.CoverImageID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Project>()
            .HasOne(p => p.CoverAiImage)
            .WithMany()
            .HasForeignKey(p => p.CoverAiImageID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProjectRoom>()
            .HasOne(pr => pr.Project)
            .WithMany(p => p.Rooms)
            .HasForeignKey(pr => pr.ProjectID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectRoom>()
            .HasOne(pr => pr.ParentRoom)
            .WithMany(pr => pr.Children)
            .HasForeignKey(pr => pr.ParentRoomID)
            .OnDelete(DeleteBehavior.ClientSetNull);

        modelBuilder.Entity<ProjectImage>()
            .HasOne(pi => pi.Project)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.ProjectID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectImage>()
            .HasOne(pi => pi.Room)
            .WithMany(pr => pr.Images)
            .HasForeignKey(pi => pi.RoomID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectImage>()
            .HasOne(pi => pi.Image)
            .WithMany()
            .HasForeignKey(pi => pi.ImageID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectImage>()
            .HasOne(pi => pi.AiGenerationJobImage)
            .WithMany()
            .HasForeignKey(pi => pi.AiImageID)
            .HasPrincipalKey(a => a.AiImageID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProjectImage>()
            .HasOne(pi => pi.CreatedByUser)
            .WithMany()
            .HasForeignKey(pi => pi.CreatedByUserID)
            .OnDelete(DeleteBehavior.SetNull);

        // MySQL 唯一索引允许多行 NULL，因此两组索引可分别阻止普通图和 AI 图重复加入同一方案。
        modelBuilder.Entity<ProjectImage>()
            .HasIndex(pi => new { pi.ProjectID, pi.ImageID })
            .IsUnique();

        modelBuilder.Entity<ProjectImage>()
            .HasIndex(pi => new { pi.ProjectID, pi.AiImageID })
            .IsUnique();

        modelBuilder.Entity<AiGenerationJob>()
            .HasKey(e => e.JobId);

        modelBuilder.Entity<AiGenerationJob>()
            .HasIndex(job => new { job.UserID, job.IsDeleted, job.CreatedAt });

        modelBuilder.Entity<AiGenerationJob>()
            .HasOne(j => j.User)
            .WithMany()
            .HasForeignKey(j => j.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AiGenerationJobImage>()
            .HasOne(img => img.AiGenerationJob)
            .WithMany(job => job.Images)
            .HasForeignKey(img => img.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AiGenerationJobImage>()
            .HasIndex(img => new { img.RetentionStatus, img.CleanupEligibleAt });

        modelBuilder.Entity<AiGenerationJobImage>()
            .HasIndex(img => new { img.JobId, img.OutputKey })
            .IsUnique();

        modelBuilder.Entity<AiGenerationJobImage>()
            .HasOne(img => img.User)
            .WithMany()
            .HasForeignKey(img => img.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserQuota>()
            .HasIndex(q => q.UserID)
            .IsUnique();

        modelBuilder.Entity<UserQuota>()
            .HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UsageRecord>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UsageRecord>()
            .HasOne(r => r.AiGenerationJob)
            .WithMany()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProjectActivity>()
            .HasOne(a => a.Project)
            .WithMany()
            .HasForeignKey(a => a.ProjectID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectActivity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserID)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
