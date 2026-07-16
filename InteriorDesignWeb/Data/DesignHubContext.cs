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
    public DbSet<UserSession> usersessions { get; set; }
    public DbSet<AdminAuditLog> adminauditlogs { get; set; }
    public DbSet<AssistantConversation> assistantconversations { get; set; }
    public DbSet<AssistantMessage> assistantmessages { get; set; }
    public DbSet<AssistantGenerationAction> assistantgenerationactions { get; set; }
    public DbSet<AssistantAgentRun> assistantagentruns { get; set; }
    public DbSet<AssistantAgentEvent> assistantagentevents { get; set; }
    public DbSet<AssistantAgentArtifact> assistantagentartifacts { get; set; }
    public DbSet<AssistantAttachment> assistantattachments { get; set; }
    public DbSet<AssistantPolicyVersion> assistantpolicyversions { get; set; }
    public DbSet<AiRolePolicy> airolepolicies { get; set; }
    public DbSet<AiUserPolicyOverride> aiuserpolicyoverrides { get; set; }

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
        modelBuilder.Entity<UserSession>().ToTable("usersessions");
        modelBuilder.Entity<AdminAuditLog>().ToTable("adminauditlogs");
        modelBuilder.Entity<AssistantConversation>().ToTable("assistantconversations");
        modelBuilder.Entity<AssistantMessage>().ToTable("assistantmessages");
        modelBuilder.Entity<AssistantGenerationAction>().ToTable("assistantgenerationactions");
        modelBuilder.Entity<AssistantAgentRun>().ToTable("assistantagentruns");
        modelBuilder.Entity<AssistantAgentEvent>().ToTable("assistantagentevents");
        modelBuilder.Entity<AssistantAgentArtifact>().ToTable("assistantagentartifacts");
        modelBuilder.Entity<AssistantAttachment>().ToTable("assistantattachments");
        modelBuilder.Entity<AssistantPolicyVersion>().ToTable("assistantpolicyversions");
        modelBuilder.Entity<AiRolePolicy>().ToTable("airolepolicies");
        modelBuilder.Entity<AiUserPolicyOverride>().ToTable("aiuserpolicyoverrides");

        modelBuilder.Entity<AssistantConversation>()
            .HasOne(conversation => conversation.User)
            .WithMany()
            .HasForeignKey(conversation => conversation.UserID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantConversation>()
            .HasOne(conversation => conversation.Project)
            .WithMany()
            .HasForeignKey(conversation => conversation.ProjectID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantConversation>()
            .HasOne(conversation => conversation.Room)
            .WithMany()
            .HasForeignKey(conversation => conversation.RoomID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantConversation>()
            .HasIndex(conversation => new { conversation.UserID, conversation.IsDeleted, conversation.UpdatedAt });

        modelBuilder.Entity<AssistantMessage>()
            .HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantMessage>()
            .HasIndex(message => new { message.ConversationID, message.CreatedAt });
        modelBuilder.Entity<AssistantMessage>()
            .HasIndex(message => new { message.ConversationID, message.ClientRequestID })
            .IsUnique();

        modelBuilder.Entity<AssistantGenerationAction>()
            .HasOne(action => action.Conversation)
            .WithMany(conversation => conversation.GenerationActions)
            .HasForeignKey(action => action.ConversationID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantGenerationAction>()
            .HasOne(action => action.Message)
            .WithMany()
            .HasForeignKey(action => action.MessageID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantGenerationAction>()
            .HasOne(action => action.Project)
            .WithMany()
            .HasForeignKey(action => action.ProjectID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantGenerationAction>()
            .HasOne(action => action.Room)
            .WithMany()
            .HasForeignKey(action => action.RoomID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantGenerationAction>()
            .HasIndex(action => new { action.ConversationID, action.IdempotencyKey })
            .IsUnique();
        modelBuilder.Entity<AssistantGenerationAction>()
            .HasIndex(action => action.JobID);

        modelBuilder.Entity<AssistantAgentRun>()
            .HasOne(run => run.Conversation)
            .WithMany(conversation => conversation.AgentRuns)
            .HasForeignKey(run => run.ConversationID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantAgentRun>()
            .HasIndex(run => new { run.ConversationID, run.ClientRequestID })
            .IsUnique();
        modelBuilder.Entity<AssistantAgentRun>()
            .HasIndex(run => new { run.UserID, run.StartedAt });

        modelBuilder.Entity<AssistantAgentEvent>()
            .HasOne(agentEvent => agentEvent.Run)
            .WithMany(run => run.Events)
            .HasForeignKey(agentEvent => agentEvent.RunID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantAgentEvent>()
            .HasIndex(agentEvent => new { agentEvent.RunID, agentEvent.Sequence })
            .IsUnique();

        modelBuilder.Entity<AssistantAgentArtifact>()
            .HasOne(artifact => artifact.Run)
            .WithMany(run => run.Artifacts)
            .HasForeignKey(artifact => artifact.RunID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantAgentArtifact>()
            .HasIndex(artifact => new { artifact.ConversationID, artifact.ArtifactType, artifact.Version });

        modelBuilder.Entity<AssistantAttachment>()
            .HasOne(attachment => attachment.Conversation)
            .WithMany(conversation => conversation.Attachments)
            .HasForeignKey(attachment => attachment.ConversationID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AssistantAttachment>()
            .HasOne(attachment => attachment.Message)
            .WithMany()
            .HasForeignKey(attachment => attachment.MessageID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantAttachment>()
            .HasOne(attachment => attachment.Room)
            .WithMany()
            .HasForeignKey(attachment => attachment.RoomID)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssistantAttachment>()
            .HasIndex(attachment => new { attachment.ConversationID, attachment.IsDeleted, attachment.CreatedAt });
        modelBuilder.Entity<AssistantAttachment>()
            .HasIndex(attachment => new { attachment.UserID, attachment.Sha256 });

        modelBuilder.Entity<Image>()
            .HasOne(image => image.DeletedByUser)
            .WithMany()
            .HasForeignKey(image => image.DeletedByUserID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Image>()
            .HasIndex(image => new { image.IsDeleted, image.UploadTime });

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion(
                v => v.ToString(),
                v => (UserRole)Enum.Parse(typeof(UserRole), v))
            .HasMaxLength(20);

        modelBuilder.Entity<AiRolePolicy>()
            .Property(item => item.Role)
            .HasConversion(
                value => value.ToString(),
                value => Enum.Parse<UserRole>(value))
            .HasMaxLength(20);

        modelBuilder.Entity<AiRolePolicy>()
            .HasIndex(item => item.Role)
            .IsUnique();

        modelBuilder.Entity<AiUserPolicyOverride>()
            .HasIndex(item => item.UserID)
            .IsUnique();

        modelBuilder.Entity<AssistantPolicyVersion>()
            .HasIndex(item => item.VersionNumber)
            .IsUnique();

        modelBuilder.Entity<AssistantPolicyVersion>()
            .HasIndex(item => new { item.IsPublished, item.PublishedAt });

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

        modelBuilder.Entity<UsageRecord>()
            .HasIndex(r => new { r.JobId, r.UsageType })
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasKey(session => session.UserSessionID);

        modelBuilder.Entity<UserSession>()
            .HasIndex(session => session.TokenHash)
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasIndex(session => new { session.UserID, session.ExpiresAt, session.RevokedAt });

        modelBuilder.Entity<UserSession>()
            .HasOne(session => session.User)
            .WithMany(user => user.Sessions)
            .HasForeignKey(session => session.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasIndex(user => new { user.IsEnabled, user.Role, user.RegisterTime });

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(log => log.CreatedAt);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(log => new { log.AdministratorUserID, log.CreatedAt });

        modelBuilder.Entity<AdminAuditLog>()
            .HasOne(log => log.Administrator)
            .WithMany()
            .HasForeignKey(log => log.AdministratorUserID)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
