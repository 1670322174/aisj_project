// 作用：集中定义远程/本地 ComfyUI Server 的连接、轮询和认证配置。
// ComfyUI AccountApiKey 仅用于工作流内的 Partner Nodes；服务器入口鉴权可通过 AuthorizationHeader 单独配置。

namespace InteriorDesignWeb.Config;

public sealed class ComfyUIServerOptions
{
    public const string SectionName = "ComfyUI";

    /// <summary>
    /// ComfyUI Server 地址，例如 http://192.168.1.20:8188/ 或 HTTPS 反向代理地址。
    /// </summary>
    public string ApiUrl { get; set; } = "http://127.0.0.1:8188/";

    /// <summary>
    /// ComfyUI Account API Key。Grok、Veo、Seedream 等 Partner Nodes 需要该 Key 和足够 Credits。
    /// </summary>
    public string AccountApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 可选的服务器入口 Authorization 请求头完整值，例如 Bearer xxx 或 Basic xxx。
    /// 仅用于 Nginx/网关保护 ComfyUI，不等同于 AccountApiKey。
    /// </summary>
    public string? AuthorizationHeader { get; set; }

    /// <summary>
    /// 提交请求和WebSocket监听共同使用的客户端标识。
    /// </summary>
    public string ClientId { get; set; } = "interior-design-web";

    public bool WebSocketProgressEnabled { get; set; } = true;

    public int WebSocketReconnectSeconds { get; set; } = 5;

    public int RequestTimeoutMinutes { get; set; } = 15;

    public int PollIntervalSeconds { get; set; } = 3;

    public int MaxPollAttempts { get; set; } = 600;

    public long MaxUploadBytes { get; set; } = 50L * 1024L * 1024L;

    /// <summary>
    /// 默认关闭系统代理，避免内网 ComfyUI 请求被代理软件转发。
    /// </summary>
    public bool UseProxy { get; set; } = false;

    /// <summary>
    /// 仅用于开发环境自签名证书，生产环境必须保持 false。
    /// </summary>
    public bool AllowInvalidCertificate { get; set; } = false;
}
