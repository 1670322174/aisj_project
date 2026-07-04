
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using InteriorDesignWeb.Models.Entities;

namespace InteriorDesignWeb.Data
{
    public class DesignHubContext : DbContext
    {

        public DesignHubContext(DbContextOptions<DesignHubContext> options)
        : base(options)
        {
        }

        // 数据库表集合
        public DbSet<User> users { get; set; }
        public DbSet<Image> images { get; set; }
        /*4/6*/
        public DbSet<Project> projects { get; set; }
        public DbSet<ProjectRoom> projectrooms { get; set; }
        public DbSet<ProjectImage> projectimages { get; set; }

        // AI生成任务表
        public DbSet<AiGenerationJob> aigenerationjobs { get; set; }
        public DbSet<AiGenerationJobImage> aigenerationjobimages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 配置实体关系等
            /*4/6*/
            // Project配置

            // ProjectImage配置
            modelBuilder.Entity<ProjectImage>()
                .HasOne(pi => pi.Project)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProjectID);

            modelBuilder.Entity<ProjectImage>()
                .HasOne(pi => pi.Room)
                .WithMany(pr => pr.Images)
                .HasForeignKey(pi => pi.RoomID);

            modelBuilder.Entity<ProjectImage>()
                .HasOne(pi => pi.Image)
                .WithMany() // 这里不需要反向导航属性，所以不指定集合
                .HasForeignKey(pi => pi.ImageID);
            // ProjectRoom层级配置
            modelBuilder.Entity<ProjectRoom>()
                .HasOne(pr => pr.ParentRoom)
                .WithMany(pr => pr.Children)
                .HasForeignKey(pr => pr.ParentRoomID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // UserRole枚举配置----4/18-----//
            modelBuilder.Entity<User>( )
                .Property(u => u.Role)
                .HasConversion(
                    v => v.ToString(),        // 将枚举转换为字符串存储
                    v => (UserRole)Enum.Parse(typeof(UserRole), v) // 从字符串转回枚举
                )
                .HasMaxLength(20);

            // 用户-项目关系配置
            // 保留正确的关系配置
            modelBuilder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.UserID)
                .OnDelete(DeleteBehavior.Cascade); // 添加级联删除
                                                   // 其他实体配置...

            modelBuilder.Entity<AiGenerationJob>(entity =>
            {
                entity.HasKey(e => e.JobId);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
            });

            modelBuilder.Entity<ProjectImage>()
    .HasOne(p => p.AiGenerationJobImage)
    .WithMany()
    .HasForeignKey(p => p.AiImageID)
    .HasPrincipalKey(a => a.AiImageID)  // 显式指定主键
    .OnDelete(DeleteBehavior.Restrict); // 避免外键删除联动

        }
    }
}
