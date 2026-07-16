using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.AI;

/// <summary>
/// Receives ComfyUI execution events through /ws. The existing database poller
/// remains enabled as a recovery path for reconnects and server restarts.
/// </summary>
public sealed class ComfyUIProgressListener : BackgroundService
{
    private static readonly string[] ActiveStatuses =
    [
        AIJobStatus.Created,
        AIJobStatus.Queued,
        AIJobStatus.Running,
        AIJobStatus.Processing,
        AIJobStatus.Uploading
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AIJobProgressBroker _broker;
    private readonly ComfyUIServerOptions _options;
    private readonly ILogger<ComfyUIProgressListener> _logger;
    private readonly AIJobRefreshCoordinator _refreshCoordinator;

    public ComfyUIProgressListener(
        IServiceScopeFactory scopeFactory,
        AIJobProgressBroker broker,
        AIJobRefreshCoordinator refreshCoordinator,
        IOptions<ComfyUIServerOptions> options,
        ILogger<ComfyUIProgressListener> logger)
    {
        _scopeFactory = scopeFactory;
        _broker = broker;
        _refreshCoordinator = refreshCoordinator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WebSocketProgressEnabled)
        {
            _logger.LogInformation("ComfyUI WebSocket进度监听已关闭，继续使用轮询兜底。");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                if (!string.IsNullOrWhiteSpace(_options.AuthorizationHeader))
                    socket.Options.SetRequestHeader("Authorization", _options.AuthorizationHeader);
                var uri = BuildWebSocketUri();
                await socket.ConnectAsync(uri, stoppingToken);
                _logger.LogInformation("已连接ComfyUI WebSocket进度通道. Uri={Uri}", RedactQuery(uri));
                await ReceiveLoopAsync(socket, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ComfyUI WebSocket进度通道中断，将使用轮询并自动重连。");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Clamp(_options.WebSocketReconnectSeconds, 2, 60)),
                stoppingToken);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[32 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken);
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Text)
                    await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text || message.Length == 0) continue;
            await HandleMessageAsync(Encoding.UTF8.GetString(message.ToArray()), cancellationToken);
        }
    }

    private async Task HandleMessageAsync(string json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeElement)
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
            return;

        var type = typeElement.GetString()?.Trim().ToLowerInvariant();
        var providerJobId = ReadString(data, "prompt_id");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(providerJobId)) return;

        var shouldFinalize = type is "execution_error" or "execution_interrupted"
            || (type == "executing" && data.TryGetProperty("node", out var node) && node.ValueKind == JsonValueKind.Null);
        var progress = type switch
        {
            "execution_start" => 5,
            "executing" => 8,
            "executed" => 94,
            "progress" => CalculateProgress(data),
            _ => 0
        };

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var job = await context.aigenerationjobs.FirstOrDefaultAsync(
            item => item.ProviderJobId == providerJobId && ActiveStatuses.Contains(item.Status),
            cancellationToken);
        if (job == null) return;

        if (progress > 0)
        {
            job.Status = progress >= 94 ? AIJobStatus.Processing : AIJobStatus.Running;
            job.ProgressValue = Math.Max(job.ProgressValue, progress);
            job.Progress = job.ProgressValue.ToString();
            job.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            Publish(job);
        }

        if (!shouldFinalize) return;
        var generationService = scope.ServiceProvider.GetRequiredService<IAIGenerationService>();
        await _refreshCoordinator.RunAsync(
            job.JobId,
            () => generationService.RefreshAsync(job.JobId, job.UserID!.Value, cancellationToken),
            cancellationToken);
        await context.Entry(job).ReloadAsync(cancellationToken);
        Publish(job);
    }

    private void Publish(AiGenerationJob job) => _broker.Publish(new AIJobProgressEvent(
        job.JobId,
        job.Status,
        job.ProgressValue,
        job.ErrorMessage,
        job.UpdatedAt ?? DateTime.UtcNow));

    private static int CalculateProgress(JsonElement data)
    {
        if (!TryReadDouble(data, "value", out var value)
            || !TryReadDouble(data, "max", out var max)
            || max <= 0)
            return 10;
        return Math.Clamp(5 + (int)Math.Round(value / max * 87), 5, 92);
    }

    private Uri BuildWebSocketUri()
    {
        var api = new Uri(_options.ApiUrl.EndsWith('/') ? _options.ApiUrl : _options.ApiUrl + "/");
        var builder = new UriBuilder(new Uri(api, "ws"))
        {
            Scheme = api.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Query = "clientId=" + Uri.EscapeDataString(_options.ClientId)
        };
        return builder.Uri;
    }

    private static string RedactQuery(Uri uri) => uri.GetLeftPart(UriPartial.Path);
    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    private static bool TryReadDouble(JsonElement value, string name, out double result)
    {
        result = 0;
        return value.TryGetProperty(name, out var element) && element.TryGetDouble(out result);
    }
}
