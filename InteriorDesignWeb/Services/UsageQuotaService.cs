using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services;

public sealed record UsageQuotaSnapshot(
    int TotalUnits,
    int UsedUnits,
    int RemainingUnits,
    int AssistantTokenLimit5Hours,
    int AssistantTokensUsed5Hours,
    DateTime AssistantTokenWindowStartedAt,
    DateTime AssistantTokenWindowEndsAt);

public sealed record AssistantTokenReservation(int ReservedTokens, DateTime WindowEndsAt);

public interface IUsageQuotaService
{
    Task<UsageQuotaSnapshot> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task ReserveGenerationAsync(int userId, string jobId, int units, string workflowCode, string? modelCode, string providerType, string requestJson, CancellationToken cancellationToken = default);
    Task MarkGenerationSubmittedAsync(string jobId, CancellationToken cancellationToken = default);
    Task RefundGenerationAsync(int userId, string jobId, CancellationToken cancellationToken = default);
    Task<AssistantTokenReservation> ReserveAssistantTokensAsync(int userId, int tokens, CancellationToken cancellationToken = default);
    Task SettleAssistantTokensAsync(int userId, int reservedTokens, int actualTokens, CancellationToken cancellationToken = default);
    Task ReleaseAssistantTokensAsync(int userId, int reservedTokens, CancellationToken cancellationToken = default);
    Task<UsageQuotaSnapshot> UpdateAsync(int userId, int totalUnits, int remainingUnits, int assistantLimit, int assistantUsed, bool resetAssistantWindow, CancellationToken cancellationToken = default);
}

public sealed class UsageQuotaService : IUsageQuotaService
{
    private static readonly TimeSpan AssistantWindow = TimeSpan.FromHours(5);
    private readonly DesignHubContext _context;
    private readonly IRoleLimitService _roleLimits;

    public UsageQuotaService(DesignHubContext context, IRoleLimitService roleLimits)
    {
        _context = context;
        _roleLimits = roleLimits;
    }

    public async Task<UsageQuotaSnapshot> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureAsync(userId, cancellationToken);
        await ResetAssistantWindowIfExpiredAsync(userId, cancellationToken);
        var quota = await _context.userquotas.AsNoTracking()
            .FirstAsync(item => item.UserID == userId, cancellationToken);
        return ToSnapshot(quota);
    }

    public async Task ReserveGenerationAsync(
        int userId,
        string jobId,
        int units,
        string workflowCode,
        string? modelCode,
        string providerType,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        units = Math.Max(1, units);
        if (await _context.usagerecords.AsNoTracking().AnyAsync(
            item => item.JobId == jobId && item.UsageType == "ai_generation",
            cancellationToken)) return;

        await EnsureAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await _context.userquotas
            .Where(item => item.UserID == userId && item.RemainingUnits >= units)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RemainingUnits, item => item.RemainingUnits - units)
                .SetProperty(item => item.UsedUnits, item => item.UsedUnits + units)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (affected == 0)
        {
            var snapshot = await GetAsync(userId, cancellationToken);
            throw AppException.QuotaExceeded($"AI 生成额度不足：本次需要 {units}，当前剩余 {snapshot.RemainingUnits}。");
        }

        try
        {
            _context.usagerecords.Add(new UsageRecord
            {
                UserID = userId,
                JobId = jobId,
                UsageType = "ai_generation",
                WorkflowCode = workflowCode,
                ModelCode = modelCode,
                ProviderType = providerType,
                Units = units,
                Status = "reserved",
                RequestJson = requestJson,
                CreatedAt = now
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _context.userquotas.Where(item => item.UserID == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.RemainingUnits, item => item.RemainingUnits + units)
                    .SetProperty(item => item.UsedUnits, item => item.UsedUnits - units)
                    .SetProperty(item => item.UpdatedAt, DateTime.UtcNow), cancellationToken);
            throw;
        }
    }

    public Task MarkGenerationSubmittedAsync(string jobId, CancellationToken cancellationToken = default) =>
        _context.usagerecords
            .Where(item => item.JobId == jobId && item.UsageType == "ai_generation" && item.Status == "reserved")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "submitted"), cancellationToken);

    public async Task RefundGenerationAsync(int userId, string jobId, CancellationToken cancellationToken = default)
    {
        var record = await _context.usagerecords.FirstOrDefaultAsync(
            item => item.JobId == jobId && item.UsageType == "ai_generation" && item.Status != "refunded",
            cancellationToken);
        if (record == null) return;
        var quota = await _context.userquotas.FirstOrDefaultAsync(item => item.UserID == userId, cancellationToken);
        if (quota == null) return;
        record.Status = "refunded";
        quota.UsedUnits = Math.Max(0, quota.UsedUnits - record.Units);
        quota.RemainingUnits = Math.Min(quota.TotalUnits, quota.RemainingUnits + record.Units);
        quota.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssistantTokenReservation> ReserveAssistantTokensAsync(int userId, int tokens, CancellationToken cancellationToken = default)
    {
        tokens = Math.Max(1, tokens);
        await EnsureAsync(userId, cancellationToken);
        await ResetAssistantWindowIfExpiredAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await _context.userquotas
            .Where(item => item.UserID == userId
                && item.AssistantTokenLimit5Hours > 0
                && item.AssistantTokensUsed5Hours <= item.AssistantTokenLimit5Hours - tokens)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AssistantTokensUsed5Hours, item => item.AssistantTokensUsed5Hours + tokens)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (affected == 0)
        {
            var snapshot = await GetAsync(userId, cancellationToken);
            throw AppException.QuotaExceeded(
                $"AI 助手 5 小时 Token 额度不足：预计需要 {tokens}，当前剩余 {Math.Max(0, snapshot.AssistantTokenLimit5Hours - snapshot.AssistantTokensUsed5Hours)}，窗口将在 {snapshot.AssistantTokenWindowEndsAt.ToLocalTime():HH:mm} 重置。");
        }

        var windowStart = await _context.userquotas.AsNoTracking()
            .Where(item => item.UserID == userId)
            .Select(item => item.AssistantTokenWindowStartedAt)
            .FirstAsync(cancellationToken) ?? now;
        return new AssistantTokenReservation(tokens, windowStart.Add(AssistantWindow));
    }

    public Task SettleAssistantTokensAsync(int userId, int reservedTokens, int actualTokens, CancellationToken cancellationToken = default)
    {
        if (actualTokens <= 0 || actualTokens == reservedTokens) return Task.CompletedTask;
        var delta = actualTokens - reservedTokens;
        return _context.userquotas.Where(item => item.UserID == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    item => item.AssistantTokensUsed5Hours,
                    item => item.AssistantTokensUsed5Hours + delta < 0
                        ? 0
                        : item.AssistantTokensUsed5Hours + delta > item.AssistantTokenLimit5Hours
                            ? item.AssistantTokenLimit5Hours
                            : item.AssistantTokensUsed5Hours + delta)
                .SetProperty(item => item.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public Task ReleaseAssistantTokensAsync(int userId, int reservedTokens, CancellationToken cancellationToken = default)
    {
        if (reservedTokens <= 0) return Task.CompletedTask;
        return _context.userquotas.Where(item => item.UserID == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    item => item.AssistantTokensUsed5Hours,
                    item => item.AssistantTokensUsed5Hours < reservedTokens
                        ? 0
                        : item.AssistantTokensUsed5Hours - reservedTokens)
                .SetProperty(item => item.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<UsageQuotaSnapshot> UpdateAsync(
        int userId,
        int totalUnits,
        int remainingUnits,
        int assistantLimit,
        int assistantUsed,
        bool resetAssistantWindow,
        CancellationToken cancellationToken = default)
    {
        if (totalUnits < 0 || remainingUnits < 0 || remainingUnits > totalUnits || assistantLimit < 0 || assistantUsed < 0)
            throw AppException.Validation("额度必须为非负数，且生图剩余额度不能大于总额度。");
        await EnsureAsync(userId, cancellationToken);
        var quota = await _context.userquotas.FirstAsync(item => item.UserID == userId, cancellationToken);
        quota.TotalUnits = totalUnits;
        quota.RemainingUnits = remainingUnits;
        quota.UsedUnits = totalUnits - remainingUnits;
        quota.AssistantTokenLimit5Hours = assistantLimit;
        quota.AssistantTokensUsed5Hours = Math.Min(assistantUsed, assistantLimit);
        if (resetAssistantWindow || quota.AssistantTokenWindowStartedAt == null)
            quota.AssistantTokenWindowStartedAt = DateTime.UtcNow;
        quota.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToSnapshot(quota);
    }

    private async Task EnsureAsync(int userId, CancellationToken cancellationToken)
    {
        if (await _context.userquotas.AsNoTracking().AnyAsync(item => item.UserID == userId, cancellationToken)) return;
        var user = await _context.users.AsNoTracking()
            .Where(item => item.UserID == userId)
            .Select(item => new { item.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("用户不存在。");
        var role = user.Role;
        var generationUnits = _roleLimits.GetAIGenerationUnits(role);
        var assistantTokens = _roleLimits.GetAssistantTokens5Hours(role);
        _context.userquotas.Add(new UserQuota
        {
            UserID = userId,
            TotalUnits = generationUnits,
            RemainingUnits = generationUnits,
            AssistantTokenLimit5Hours = assistantTokens,
            AssistantTokenWindowStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Task ResetAssistantWindowIfExpiredAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredBefore = now.Subtract(AssistantWindow);
        return _context.userquotas
            .Where(item => item.UserID == userId
                && (item.AssistantTokenWindowStartedAt == null || item.AssistantTokenWindowStartedAt <= expiredBefore))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AssistantTokensUsed5Hours, 0)
                .SetProperty(item => item.AssistantTokenWindowStartedAt, now)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
    }

    private static UsageQuotaSnapshot ToSnapshot(UserQuota quota)
    {
        var start = quota.AssistantTokenWindowStartedAt ?? DateTime.UtcNow;
        return new UsageQuotaSnapshot(
            quota.TotalUnits,
            quota.UsedUnits,
            quota.RemainingUnits,
            quota.AssistantTokenLimit5Hours,
            quota.AssistantTokensUsed5Hours,
            start,
            start.Add(AssistantWindow));
    }
}
