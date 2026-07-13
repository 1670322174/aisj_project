using InteriorDesignWeb.Config;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Services;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Tests;

public sealed class RoleLimitServiceTests
{
    [Theory]
    [InlineData(UserRole.FreeUser, 100, 20_000)]
    [InlineData(UserRole.Member, 1_000, 50_000)]
    [InlineData(UserRole.PremiumMember, 10_000, 150_000)]
    [InlineData(UserRole.Administrator, 1_000_000, 1_000_000)]
    public void AiQuotaDefaults_AreAppliedByRole(UserRole role, int generationUnits, int assistantTokens)
    {
        var service = new RoleLimitService(Options.Create(new RoleLimitsOptions()));

        Assert.Equal(generationUnits, service.GetAIGenerationUnits(role));
        Assert.Equal(assistantTokens, service.GetAssistantTokens5Hours(role));
    }

    [Fact]
    public void ConfiguredAiQuota_OverridesFallback()
    {
        var options = new RoleLimitsOptions
        {
            AIGenerationUnits = new Dictionary<string, int> { [nameof(UserRole.Member)] = 321 },
            AssistantTokens5Hours = new Dictionary<string, int> { [nameof(UserRole.Member)] = 654 }
        };
        var service = new RoleLimitService(Options.Create(options));

        Assert.Equal(321, service.GetAIGenerationUnits(UserRole.Member));
        Assert.Equal(654, service.GetAssistantTokens5Hours(UserRole.Member));
    }
}
