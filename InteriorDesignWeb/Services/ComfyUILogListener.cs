using System.Text.RegularExpressions;

namespace InteriorDesignWeb.Services
{
    public class ComfyUILogListener
    {
        private readonly ILogger<ComfyUILogListener> _logger;
        private readonly JobTrackingService _jobTrackingService;

        public ComfyUILogListener(ILogger<ComfyUILogListener> logger, JobTrackingService jobTrackingService)
        {
            _logger = logger;
            _jobTrackingService = jobTrackingService;
        }

        public async Task StartListening(string jobId)
        {
            var logFilePath = "path_to_comfyui_log_file.log"; // ComfyUI日志文件路径
            var fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(logFilePath))
            {
                Filter = Path.GetFileName(logFilePath),
                NotifyFilter = NotifyFilters.LastWrite
            };

            fileWatcher.Changed += async (sender, e) =>
            {
                try
                {
                    var logContent = File.ReadAllText(logFilePath);
                    // 根据日志内容解析生成进度，假设日志中有类似 "50%" 的信息
                    var progress = ParseProgressFromLog(logContent);

                    // 更新任务进度到数据库
                    await _jobTrackingService.UpdateJobStatusAsync(jobId, "processing", progress.ToString());

                    // 如果进度为100%，更新为完成
                    if (progress >= 100)
                    {
                        await _jobTrackingService.UpdateJobStatusAsync(jobId, "completed", "image_url_here");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "监听日志失败");
                }
            };

            fileWatcher.EnableRaisingEvents = true;
        }

        private int ParseProgressFromLog(string logContent)
        {
            // 示例：解析日志中的进度信息
            var match = Regex.Match(logContent, @"(\d+)%");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;
        }
    }
}
