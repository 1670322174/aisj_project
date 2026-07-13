using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services
{
    public interface IQuotaService
    {
        Task<bool> CanCreateProject(int userId);
        Task<bool> CanAddImage(int projectId);
    }
    /// <summary>  
    /// -----4/16/2025 -----
    /// QuotaService类提供与用户项目配额相关的服务。  
    /// </summary>  
    /// <remarks>  
    /// - CanCreateProject 方法用于检查用户是否可以创建新项目。  
    /// - 根据用户角色（FreeUser、Member、PremiumMember）限制项目数量。  
    /// </remarks>  
    public class QuotaService : IQuotaService
    {
        private readonly DesignHubContext _context;
        private readonly IRoleLimitService _roleLimitService;


        /// <summary>  
        /// 初始化 QuotaService 类的新实例。  
        /// </summary>  
        /// <param name="context">数据库上下文 DesignHubContext。</param>  
        public QuotaService(
            DesignHubContext context,
            IRoleLimitService roleLimitService)
        {
            _context = context;
            _roleLimitService = roleLimitService;
        }

        /// <summary>  
        /// 检查用户是否可以创建新项目。  
        /// </summary>  
        /// <param name="userId">用户的唯一标识符。</param>  
        /// <returns>如果用户可以创建项目，则返回 true；否则返回 false。</returns>  
        public async Task<bool> CanCreateProject(int userId)
        {
            var user = await _context.users
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return false;

            var limit = _roleLimitService.GetSchemeCreationLimit(user.Role);
            int currentCount = await _context.projects
                .CountAsync(p => p.UserID == userId && !p.IsDeleted);

            return currentCount < limit;
        }

        public async Task<bool> CanAddImage(int projectId)
        {
            var project = await _context.projects
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ProjectID == projectId && !p.IsDeleted);

            if (project?.User == null) return false;

            var limit = _roleLimitService.GetImagesPerSchemeLimit(project.User.Role);
            int imageCount = await _context.projectimages
                .CountAsync(pi => pi.ProjectID == projectId);

            return imageCount < limit;
        }

    }
}
