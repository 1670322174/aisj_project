using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Services
{
    public class FluxWorkflowService
    {
        private readonly JObject _baseWorkflow;
        private readonly ILogger<FluxWorkflowService> _logger;

        public FluxWorkflowService(ILogger<FluxWorkflowService> logger, IConfiguration config)
        {
            _logger = logger;
            var workflowFilePath = config["ComfyUI:WorkflowName"];  // 从配置文件获取路径
            var workflowJson = File.ReadAllText(workflowFilePath);
            _baseWorkflow = JObject.Parse(workflowJson);
        }

        public JObject GenerateWorkflow(FluxGenerationParameters parameters)
        {
            try
            {
                // 复制基础工作流
                var workflow = (JObject)_baseWorkflow.DeepClone();

                // 更新节点参数
                // 安全更新节点参数
                SafeUpdateNode(workflow, "6", "text", parameters.Prompt);
                SafeUpdateNode(workflow, "25", "noise_seed", parameters.Seed);
                SafeUpdateNode(workflow, "17", "steps", parameters.Steps);
                SafeUpdateNode(workflow, "17", "denoise", parameters.Denoise);
                SafeUpdateNode(workflow, "26", "guidance", parameters.CfgScale);
                SafeUpdateNode(workflow, "27", "width", parameters.Width);
                SafeUpdateNode(workflow, "27", "height", parameters.Height);
                SafeUpdateNode(workflow, "27", "batch_size", parameters.BatchSize); // 设置batch_size

                // 记录最终工作流（调试用）
                _logger.LogDebug("生成的工作流: {Workflow}", workflow.ToString());
                return workflow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "工作流生成失败");
                throw new WorkflowException("无法生成工作流配置", ex);
            }
        }
        private void SafeUpdateNode(JObject workflow, string nodeId, string inputKey, object value)
        {
            var node = workflow[nodeId] as JObject;
            if (node == null)
            {
                _logger.LogError($"节点 {nodeId} 不存在");
                throw new InvalidOperationException($"节点 {nodeId} 不存在");
            }

            // 确保 class_type 存在
            if (!node.ContainsKey("class_type"))
            {
                _logger.LogError($"节点 {nodeId} 缺少 class_type 属性");
                throw new InvalidOperationException($"节点 {nodeId} 缺少 class_type 属性");
            }

            var inputs = node["inputs"] as JObject;
            if (inputs == null)
            {
                _logger.LogError($"节点 {nodeId} 缺少 inputs 对象");
                throw new InvalidOperationException($"节点 {nodeId} 缺少 inputs 对象");
            }

            // 安全更新值
            inputs[inputKey] = JToken.FromObject(value);
        }
    }

    public class FluxGenerationParameters
    {
        [Required(ErrorMessage = "提示文本不能为空")]
        [StringLength(1000, ErrorMessage = "提示文本不能超过1000字符")]
        public string Prompt { get; set; } = "A modern office building design with glass walls and green plants";

        [Range(1, 50, ErrorMessage = "步数必须在1-50之间")]
        public int Steps { get; set; } = 20;

        [Range(0.1, 1.0, ErrorMessage = "去噪强度必须在0.1-1.0之间")]
        public float Denoise { get; set; } = 1.0f;

        [Range(0, long.MaxValue, ErrorMessage = "种子必须是非负数")]
        public long Seed { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [Range(0.1, 10.0, ErrorMessage = "引导强度必须在0.1-10.0之间")]
        public float CfgScale { get; set; } = 3.5f;

        [Range(512, 2048, ErrorMessage = "宽度必须在512-2048之间")]
        public int Width { get; set; } = 512;

        [Range(512, 2048, ErrorMessage = "高度必须在512-2048之间")]
        public int Height { get; set; } = 512;

        [Range(1, 10, ErrorMessage = "批量生成的图片数量必须在1到10之间")]
        public int BatchSize { get; set; } = 1;  // 新增 batch_size 参数
    }

    public class WorkflowException : Exception
    {
        public WorkflowException(string message) : base(message) { }
        public WorkflowException(string message, Exception inner) : base(message, inner) { }
    }
}
