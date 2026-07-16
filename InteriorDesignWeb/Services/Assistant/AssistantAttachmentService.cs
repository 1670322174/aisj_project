using System.Security.Cryptography;
using System.Diagnostics;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.Assistant;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace InteriorDesignWeb.Services.Assistant;

public interface IAssistantAttachmentService
{
    Task<AssistantAttachmentDto> UploadAsync(int userId, long conversationId, int? roomId, IFormFile file, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType)> OpenAsync(int userId, long conversationId, long attachmentId, bool thumbnail, CancellationToken cancellationToken);
    Task DeleteAsync(int userId, long conversationId, long attachmentId, CancellationToken cancellationToken);
}

public sealed class AssistantAttachmentService : IAssistantAttachmentService
{
    private const long MaxFileBytes = 15 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly DesignHubContext _context;
    private readonly CosService _cosService;
    private readonly ILogger<AssistantAttachmentService> _logger;

    public AssistantAttachmentService(
        DesignHubContext context,
        CosService cosService,
        ILogger<AssistantAttachmentService> logger)
    {
        _context = context;
        _cosService = cosService;
        _logger = logger;
    }

    public async Task<AssistantAttachmentDto> UploadAsync(
        int userId,
        long conversationId,
        int? roomId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var stage = "validate_file";
        if (file.Length <= 0 || file.Length > MaxFileBytes)
            throw AppException.Validation("图片大小必须在 1 字节到 15 MB 之间。");
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw AppException.Validation("仅支持 JPEG、PNG 和 WebP 图片。");

        var conversation = await _context.assistantconversations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ConversationID == conversationId
                && item.UserID == userId && !item.IsDeleted, cancellationToken)
            ?? throw AppException.NotFound("设计助手对话不存在。");
        if (roomId.HasValue)
        {
            if (!conversation.ProjectID.HasValue)
                throw AppException.Validation("未绑定方案时不能为附件指定房间。");
            var validRoom = await _context.projectrooms.AsNoTracking().AnyAsync(
                item => item.RoomID == roomId && item.ProjectID == conversation.ProjectID,
                cancellationToken);
            if (!validRoom) throw AppException.Validation("附件房间不属于当前方案。");
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["ConversationId"] = conversationId,
            ["UserId"] = userId,
            ["RoomId"] = roomId ?? conversation.RoomID
        });
        _logger.LogInformation(
            "Assistant attachment upload started. ContentType={ContentType}, FileBytes={FileBytes}",
            file.ContentType,
            file.Length);

        stage = "read_image";
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        stage = "identify_image";
        using var identifyStream = new MemoryStream(bytes, writable: false);
        var imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(identifyStream, cancellationToken)
            ?? throw AppException.Validation("无法识别图片内容。");
        if (imageInfo.Width <= 0 || imageInfo.Height <= 0
            || imageInfo.Width > 12000 || imageInfo.Height > 12000
            || (long)imageInfo.Width * imageInfo.Height > 36_000_000)
            throw AppException.Validation("图片尺寸过大，最长边不能超过 12000 像素且总像素不能超过 3600 万。");

        string? cosPath = null;
        string? thumbnailPath = null;
        try
        {
            stage = "cos_upload_original";
            using (var originalStream = new MemoryStream(bytes, writable: false))
            {
                (cosPath, _) = await _cosService.UploadAIImageAsync(
                    originalStream,
                    SanitizeFileName(file.FileName),
                    $"Assistant-Uploads/{userId}");
            }
            stage = "cos_upload_thumbnail";
            using (var thumbnailStream = new MemoryStream(bytes, writable: false))
            {
                thumbnailPath = await _cosService.GenerateAndUploadThumbnailAsync(thumbnailStream, cosPath);
            }

            stage = "save_attachment_metadata";
            var entity = new AssistantAttachment
            {
                ConversationID = conversationId,
                UserID = userId,
                RoomID = roomId ?? conversation.RoomID,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                FileSize = file.Length,
                Width = imageInfo.Width,
                Height = imageInfo.Height,
                Sha256 = sha256,
                CosPath = cosPath,
                ThumbnailPath = thumbnailPath,
                CreatedAt = DateTime.UtcNow
            };
            _context.assistantattachments.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            timer.Stop();
            _logger.LogInformation(
                "Assistant attachment upload completed. AttachmentId={AttachmentId}, Width={Width}, Height={Height}, FileBytes={FileBytes}, DurationMs={DurationMs}",
                entity.AttachmentID,
                entity.Width,
                entity.Height,
                entity.FileSize,
                timer.ElapsedMilliseconds);
            return ToDto(entity);
        }
        catch (Exception exception)
        {
            timer.Stop();
            TryDelete(thumbnailPath);
            TryDelete(cosPath);
            var retryable = stage.StartsWith("cos_", StringComparison.Ordinal);
            _logger.LogWarning(
                exception,
                "Assistant attachment upload failed. Stage={Stage}, ContentType={ContentType}, FileBytes={FileBytes}, DurationMs={DurationMs}, Retryable={Retryable}",
                stage,
                file.ContentType,
                file.Length,
                timer.ElapsedMilliseconds,
                retryable);
            if (exception is AppException appException)
            {
                if (string.IsNullOrWhiteSpace(appException.DiagnosticStage))
                    appException.WithDiagnostic("assistant_attachment_failed", stage, "请根据失败阶段检查图片内容、COS 配置或数据库连接。", retryable: retryable);
                throw;
            }
            throw new AppException(ErrorCodes.ServerError, "图片附件上传失败。", StatusCodes.Status500InternalServerError, exception)
                .WithDiagnostic("assistant_attachment_failed", stage, "请检查 COS 连接、存储权限和数据库状态。", retryable: retryable);
        }
    }

    public async Task<(Stream Stream, string ContentType)> OpenAsync(
        int userId,
        long conversationId,
        long attachmentId,
        bool thumbnail,
        CancellationToken cancellationToken)
    {
        var attachment = await FindOwnedAsync(userId, conversationId, attachmentId, cancellationToken);
        var path = thumbnail && !string.IsNullOrWhiteSpace(attachment.ThumbnailPath)
            ? attachment.ThumbnailPath
            : attachment.CosPath;
        try
        {
            var result = await _cosService.GetAIObjectStreamAsync(path, cancellationToken);
            _logger.LogInformation(
                "Assistant attachment opened. AttachmentId={AttachmentId}, ConversationId={ConversationId}, UserId={UserId}, Thumbnail={Thumbnail}",
                attachmentId,
                conversationId,
                userId,
                thumbnail);
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Assistant attachment open failed. AttachmentId={AttachmentId}, ConversationId={ConversationId}, UserId={UserId}, Thumbnail={Thumbnail}",
                attachmentId,
                conversationId,
                userId,
                thumbnail);
            if (exception is AppException appException)
                throw appException.WithDiagnostic("assistant_attachment_read_failed", "cos_read_attachment", "请检查 AI COS 对象是否存在以及服务器 COS 读取权限。", retryable: true);
            throw new AppException(ErrorCodes.ServerError, "图片附件读取失败。", StatusCodes.Status500InternalServerError, exception)
                .WithDiagnostic("assistant_attachment_read_failed", "cos_read_attachment", "请检查 AI COS 对象和读取权限。", retryable: true);
        }
    }

    public async Task DeleteAsync(
        int userId,
        long conversationId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindOwnedAsync(userId, conversationId, attachmentId, cancellationToken);
        attachment.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
        TryDelete(attachment.ThumbnailPath);
        TryDelete(attachment.CosPath);
    }

    private async Task<AssistantAttachment> FindOwnedAsync(
        int userId,
        long conversationId,
        long attachmentId,
        CancellationToken cancellationToken) =>
        await _context.assistantattachments.FirstOrDefaultAsync(
            item => item.AttachmentID == attachmentId
                && item.ConversationID == conversationId
                && item.UserID == userId
                && !item.IsDeleted,
            cancellationToken) ?? throw AppException.NotFound("附件不存在或无权访问。");

    private void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { _cosService.DeleteAIObject(path); }
        catch (Exception exception) { _logger.LogWarning(exception, "Assistant attachment COS cleanup failed."); }
    }

    private static string SanitizeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp")) extension = ".jpg";
        return $"attachment{extension}";
    }

    internal static AssistantAttachmentDto ToDto(AssistantAttachment item) => new()
    {
        AttachmentId = item.AttachmentID,
        MessageId = item.MessageID,
        RoomId = item.RoomID,
        FileName = item.FileName,
        ContentType = item.ContentType,
        FileSize = item.FileSize,
        Width = item.Width,
        Height = item.Height,
        Kind = item.Kind,
        VisionStatus = item.VisionStatus,
        CreatedAt = item.CreatedAt
    };
}
