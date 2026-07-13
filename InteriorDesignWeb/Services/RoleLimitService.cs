using InteriorDesignWeb.Config;
using InteriorDesignWeb.Models.Entities;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services
{
    public interface IRoleLimitService
    {
        int GetSchemeCreationLimit(IEnumerable<string> roles);
        int GetImagesPerSchemeLimit(IEnumerable<string> roles);
        int GetSchemeCreationLimit(UserRole role);
        int GetImagesPerSchemeLimit(UserRole role);
        int GetAIGenerationUnits(UserRole role);
        int GetAssistantTokens5Hours(UserRole role);
    }
    public class RoleLimitService : IRoleLimitService
    {
        private readonly RoleLimitsOptions _config;

        public RoleLimitService(IOptions<RoleLimitsOptions> options)
        {
            _config = options.Value;
        }

        public int GetSchemeCreationLimit(UserRole role)
            => _config.SchemeCreation.TryGetValue(role.ToString(), out var limit)
                ? limit
                : int.MaxValue;

        public int GetImagesPerSchemeLimit(UserRole role)
            => _config.ImagesPerScheme.TryGetValue(role.ToString(), out var limit)
                ? limit
                : int.MaxValue;

        public int GetAIGenerationUnits(UserRole role)
            => _config.AIGenerationUnits.TryGetValue(role.ToString(), out var limit)
                ? limit
                : role switch
                {
                    UserRole.FreeUser => 100,
                    UserRole.Member => 1000,
                    UserRole.PremiumMember => 10000,
                    _ => 1000000
                };

        public int GetAssistantTokens5Hours(UserRole role)
            => _config.AssistantTokens5Hours.TryGetValue(role.ToString(), out var limit)
                ? limit
                : role switch
                {
                    UserRole.FreeUser => 20000,
                    UserRole.Member => 50000,
                    UserRole.PremiumMember => 150000,
                    _ => 1000000
                };

        public int GetSchemeCreationLimit(IEnumerable<string> roles)
            => roles.Select(role => GetSchemeCreationLimit(Enum.Parse<UserRole>(role)))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();

        public int GetImagesPerSchemeLimit(IEnumerable<string> roles)
            => roles.Select(role => GetImagesPerSchemeLimit(Enum.Parse<UserRole>(role)))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
    }
    }
