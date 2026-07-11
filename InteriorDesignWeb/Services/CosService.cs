namespace InteriorDesignWeb.Services
{
    using COSXML;
    using COSXML.Common;
    using COSXML.CosException;
    using COSXML.Model.Object;
    using COSXML.Model.Tag;
    using COSXML.Transfer;
    using InteriorDesignWeb.Data;
    using Microsoft.EntityFrameworkCore;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Processing;
    using System.Collections.Concurrent;
    using System.Configuration;
    using System.Security.Cryptography;

    public class CosService
    {

        private readonly DesignHubContext _context;
        private readonly CosXmlServer _cosXml;
        private readonly IConfiguration _config;
        private readonly ILogger<CosService> _logger;

        // 存储桶配置缓存
        private readonly Dictionary<string, BucketConfig> _bucketConfigs = new();
        // 长期签名缓存
        private readonly ConcurrentDictionary<string, (DateTime Expiry, string Url)> _urlCache = new();
        public CosService(
            DesignHubContext context,
            IConfiguration config,
            CosXmlServer cosXml,
            ILogger<CosService> logger)
        {
            _context = context;
            _config = config;
            _cosXml = cosXml;
            _logger = logger;

                    // 初始化存储桶配置
        InitializeBucketConfigs();
        }
        private void InitializeBucketConfigs()
        {
            // 主存储桶配置
            _bucketConfigs["default"] = new BucketConfig
            {
                BucketName = _config["COS:Bucket"],
                Region = _config["COS:Region"],
                BaseUrl = _config["COS:BaseUrl"] ??
                         $"https://{_config["COS:Bucket"]}.cos.{_config["COS:Region"]}.myqcloud.com"
            };

            // AI 图片存储桶配置
            _bucketConfigs["ai"] = new BucketConfig
            {
                BucketName = _config["COS:AIBucket"],
                Region = _config["COS:AIRegion"],
                BaseUrl = _config["COS:AIBaseUrl"] ??
                         $"https://{_config["COS:AIBucket"]}.cos.{_config["COS:AIRegion"]}.myqcloud.com"
            };
        }
        /// <summary>
        /// 上传图片到COS服务器，并生成缩略图。
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="fileName"></param>
        /// <param name="roomType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<(string Path, string Hash)> UploadImageAsync(
   Stream fileStream,
   string fileName,
   string roomType)
        {
            try
            {
                // ==== 1. 计算文件哈希 ====  
                using var md5 = MD5.Create();
                var hashBytes = await md5.ComputeHashAsync(fileStream);
                var fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                // ==== 2. 构建存储路径 ====  
                var fileExt = Path.GetExtension(fileName);
                var cosPath = $"InteriorDesignImages/{roomType}/{fileHash}{fileExt}";

                // ==== 3. 上传原图（使用临时文件上传）====  
                fileStream.Position = 0; // 重置流位置
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var tempFileStream = File.Create(tempFilePath))
                    {
                        await fileStream.CopyToAsync(tempFileStream);
                    }

                    // 创建上传任务
                    var uploadTask = new COSXMLUploadTask(
                        _config["COS:Bucket"],
                        cosPath
                    );

                    // 设置源路径
                    uploadTask.SetSrcPath(tempFilePath);

                    // 使用TransferManager上传
                    var transferConfig = new TransferConfig();
                    var uploadResult = await new TransferManager(_cosXml, transferConfig).UploadAsync(uploadTask);

                    if (uploadResult.httpCode != 200)
                    {
                        _logger.LogError($"上传失败: {uploadResult.httpCode} - {uploadResult.httpMessage}");
                        throw new Exception($"原图上传失败: {uploadResult.httpMessage}");
                    }

                    return (cosPath, fileHash);
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
            }
            catch (CosServerException ex)
            {
                // 捕获腾讯云特定异常
                _logger.LogError($"COS服务器异常: 状态码={ex.statusCode}, 错误码={ex.errorCode}, 错误消息={ex.errorMessage}");
                throw new Exception($"COS上传失败: {ex.errorMessage}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传过程中发生异常");
                throw;
            }
        }

        /// <summary>
        /// 生成缩略图并上传到COS服务器。
        /// </summary>
        /// <param name="imageStream"></param>
        /// <param name="originalPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> GenerateThumbnailAsync(
   Stream imageStream,
   string originalPath)
        {
            try
            {
                // ==== 1. 生成缩略图路径 ====  
                var thumbPath = originalPath
                    .Replace("InteriorDesignImages", "thumbnails")
                    .Replace(Path.GetFileName(originalPath),
                        Path.GetFileNameWithoutExtension(originalPath) + "_thumbnail" + Path.GetExtension(originalPath));

                // ==== 2. 生成缩略图 ====  
                imageStream.Position = 0;
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                // 使用内存流保存缩略图
                using var memStream = new MemoryStream();
                await image.SaveAsJpegAsync(memStream);
                memStream.Position = 0;

                // ==== 3. 上传缩略图 ====  
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var tempFileStream = File.Create(tempFilePath))
                    {
                        await memStream.CopyToAsync(tempFileStream);
                    }

                    var uploadTask = new COSXMLUploadTask(
                        _config["COS:Bucket"],
                        thumbPath
                    );

                    uploadTask.SetSrcPath(tempFilePath);

                    var transferConfig = new TransferConfig();
                    var thumbResult = await new TransferManager(_cosXml, transferConfig).UploadAsync(uploadTask);

                    if (thumbResult.httpCode != 200)
                        throw new Exception($"缩略图上传失败: {thumbResult.httpMessage}");

                    return thumbPath;
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
            }
            catch (CosServerException ex)
            {
                _logger.LogError($"缩略图上传异常: 状态码={ex.statusCode}, 错误码={ex.errorCode}, 错误消息={ex.errorMessage}");
                throw new Exception($"缩略图上传失败: {ex.errorMessage}", ex);
            }
        }

        /// <summary>
        /// 上传AI生成的图像到COS
        /// </summary>
        /// <summary>
        /// 专门用于上传 AI 生成图片到指定存储桶
        /// </summary>
        public async Task<(string Path, string Url)> UploadAIImageAsync(
            Stream imageStream,
            string fileName,
            string category = "AI-Generated")
        {
            try
            {
                // 1. 获取存储桶信息
                var aiBucket = _config["COS:AIBucket"] ?? throw new ConfigurationException("缺少 AI 存储桶配置: AIBucket");
                var aiRegion = _config["COS:AIRegion"] ?? throw new ConfigurationException("缺少 AI 存储桶区域配置: AIRegion");

                // 2. 构建存储路径
                var fileExt = Path.GetExtension(fileName);
                var objectKey = $"{category}/{DateTime.Now:yyyyMMdd}/{Guid.NewGuid()}{fileExt}";

                // 3. 创建临时文件保存图像数据
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    // 将图像流写入临时文件
                    using (var tempFileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await imageStream.CopyToAsync(tempFileStream);
                    }

                    // 4. 创建上传任务
                    var uploadTask = new COSXMLUploadTask(aiBucket, objectKey);
                    uploadTask.SetSrcPath(tempFilePath);  // 使用临时文件的路径上传

                    // 5. 使用TransferManager进行上传
                    var transferConfig = new TransferConfig();
                    var uploadResult = await new TransferManager(_cosXml, transferConfig).UploadAsync(uploadTask);

                    if (uploadResult.httpCode != 200)
                        throw new Exception($"AI图片上传失败: {uploadResult.httpCode} - {uploadResult.httpMessage}");

                    // 6. 生成预签名 URL（30天有效期）
                    var signedUrl = GenerateAIPublicUrl(objectKey);
                    return (objectKey, signedUrl);
                }
                finally
                {
                    // 确保临时文件在上传后被删除
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
            }
            catch (CosServerException ex)
            {
                _logger.LogError($"AI图片上传异常: 状态码={ex.statusCode}, 错误码={ex.errorCode}, 错误消息={ex.errorMessage}");
                throw new Exception($"AI图片上传失败: {ex.errorMessage}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI图片上传失败");
                throw;
            }
        }
        /// <summary>
        /// 生成缩略图并上传到COS服务器（专门用于AI生成的图片）
        /// </summary>
        /// <param name="imageStream"></param>
        /// <param name="originalPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> GenerateAndUploadThumbnailAsync(
            Stream imageStream,
            string originalPath)
        {
            try
            {
                // 1. 生成缩略图路径
                var thumbPath = originalPath
                    .Replace("InteriorDesignImages", "thumbnails")
                    .Replace(Path.GetFileName(originalPath),
                        Path.GetFileNameWithoutExtension(originalPath) + "_thumbnail" + Path.GetExtension(originalPath));

                _logger.LogInformation("生成的缩略图路径: {ThumbPath}", thumbPath);  // 日志记录生成的缩略图路径

                // 2. 生成缩略图
                imageStream.Position = 0;
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                // 使用内存流保存缩略图
                using var memStream = new MemoryStream();
                await image.SaveAsJpegAsync(memStream);
                memStream.Position = 0;

                _logger.LogInformation("缩略图生成完毕，开始上传...");

                // 3. 上传缩略图
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var tempFileStream = File.Create(tempFilePath))
                    {
                        await memStream.CopyToAsync(tempFileStream);
                    }

                    var uploadTask = new COSXMLUploadTask(
                        _config["COS:AIBucket"],  // 使用 AIBucket 存储缩略图
                        thumbPath
                    );

                    _logger.LogInformation("开始上传缩略图到COS: {ThumbPath}", thumbPath); // 日志记录上传操作开始

                    uploadTask.SetSrcPath(tempFilePath);

                    var transferConfig = new TransferConfig();
                    var thumbResult = await new TransferManager(_cosXml, transferConfig).UploadAsync(uploadTask);

                    // 4. 检查上传结果
                    if (thumbResult.httpCode != 200)
                    {
                        _logger.LogError("缩略图上传失败: {HttpCode} - {HttpMessage}", thumbResult.httpCode, thumbResult.httpMessage);
                        throw new Exception($"缩略图上传失败: {thumbResult.httpMessage}");
                    }

                    _logger.LogInformation("缩略图上传成功，路径: {ThumbPath}", thumbPath);  // 日志记录上传成功

                    return thumbPath;
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInformation("临时文件已删除: {TempFilePath}", tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成并上传缩略图失败");
                throw new Exception("生成并上传缩略图失败", ex);
            }
        }

        /// <summary>
        /// 生成 AI 图片的公有访问 URL
        /// </summary>
        public string GenerateAIPublicUrl(string objectKey)
        {
            var aiBaseUrl = _config["COS:AIBaseUrl"] ??
                $"https://{_config["COS:AIBucket"]}.cos.{_config["COS:AIRegion"]}.myqcloud.com";

            objectKey = objectKey.Replace("\\", "/").TrimStart('/');
            return $"{aiBaseUrl}/{objectKey}";
        }

        /// <summary>
        /// Deletes an AI asset by object key. The caller must verify that the
        /// database row is no longer referenced by a project before calling.
        /// </summary>
        public void DeleteAIObject(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return;
            }

            var bucket = _config["COS:AIBucket"]
                ?? throw new ConfigurationException("缺少 AI 存储桶配置: AIBucket");
            var normalizedKey = objectKey.Replace("\\", "/").TrimStart('/');
            var result = _cosXml.DeleteObject(new DeleteObjectRequest(bucket, normalizedKey));

            if (result.httpCode is < 200 or >= 300)
            {
                throw new CosException(
                    $"COS 删除失败: HTTP {result.httpCode}",
                    new InvalidOperationException(result.httpMessage));
            }

            _urlCache.TryRemove(normalizedKey, out _);
        }

        public async Task<(string Url, string SignedUrl)> GetImageInfoAsync(
        int imageId,
        string? type = null,
        bool needSign = false)
        {
            // 1. 查询数据库
            var image = await _context.images
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ImageID == imageId);

            if (image == null)
                throw new KeyNotFoundException("图片记录不存在");

            // 2. 确定路径
            var objectKey = (type?.ToLower() == "thumbnail") ?
                image.ThumbnailPath :
                image.FilePath;

            if (string.IsNullOrWhiteSpace(objectKey))
                throw new InvalidOperationException("图片路径无效");

            // 3. 生成基础URL
            var baseUrl = _config["COS:BaseUrl"] ??
                throw new ConfigurationException("COS BaseUrl 未配置");

            var directUrl = $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(objectKey.Replace("\\", "/"))}";

            // 4. 生成签名URL
            string signedUrl = needSign ?
                GenerateSignedUrl(objectKey) :
                directUrl;

            return (directUrl, signedUrl);
        }

        public async Task<(Stream Stream, string ContentType)> GetImageStreamAsync(int imageId, string? type = null)
        {
            var (_, signedUrl) = await GetImageInfoAsync(imageId, type, needSign: true);

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(signedUrl);

            if (!response.IsSuccessStatusCode)
                throw new CosException($"COS访问失败: {response.StatusCode}", null);

            // 将流复制到内存流避免提前释放
            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // 重置流位置

            // 确定Content-Type
            var contentType = response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";

            return (memoryStream, contentType);
        }
        /// <summary>
        /// 获取ai图片
        /// </summary>
        /// <param name="aiImageId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<(Stream Stream, string ContentType)> GetImageStreamByAiImageIdAsync(int aiImageId, string type = "original")
        {
            // 1. 查询数据库获取图片记录
            var jobImage = await _context.aigenerationjobimages
                .FirstOrDefaultAsync(i => i.AiImageID == aiImageId);

            if (jobImage == null)
            {
                throw new KeyNotFoundException($"图片ID {aiImageId} 未找到");
            }

            // 2. 对象路径只在后端解析；浏览器始终访问受控媒体接口。
            var wantsThumbnail = type.Equals("thumbnail", StringComparison.OrdinalIgnoreCase);
            var imagePath = wantsThumbnail && !string.IsNullOrWhiteSpace(jobImage.ThumbnailPath)
                ? GenerateAIPublicUrl(jobImage.ThumbnailPath)
                : jobImage.ImageUrl;

            if (string.IsNullOrWhiteSpace(imagePath) && !string.IsNullOrWhiteSpace(jobImage.CosPath))
            {
                imagePath = GenerateAIPublicUrl(jobImage.CosPath);
            }

            if (string.IsNullOrWhiteSpace(imagePath))
                throw new InvalidOperationException("图片路径无效");

            // 4. 获取COS中的图像流
            var (imageStream, contentType) = await GetImageStreamFromCos(imagePath);

            return (imageStream, contentType);
        }
        private async Task<(Stream Stream, string ContentType)> GetImageStreamFromCos(string objectKey)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(objectKey);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"COS访问失败: {response.StatusCode}");
            }

            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // 重置流位置

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            return (memoryStream, contentType);
        }
        // 改造后的签名方法
        public string GenerateSignedUrl(string objectKey)
        {
            // 统一路径格式
            objectKey = objectKey.Replace("\\", "/").TrimStart('/');

            // 使用官方预签名方法（延长有效期）
            try
            {
                // 1. 创建签名配置 - 延长到最大有效期（30天）
                var preSignatureStruct = new PreSignatureStruct
                {
                    appid = _config["COS:AppId"],
                    region = _config["COS:Region"],
                    bucket = _config["COS:Bucket"],
                    key = objectKey,
                    httpMethod = "GET",
                    isHttps = true,
                    signDurationSecond = 8640*325 // 30天有效期
                };

                // 2. 生成预签名 URL
                return _cosXml.GenerateSignURL(preSignatureStruct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预签名URL生成失败 Key={ObjectKey}", objectKey);
                throw new CosException("生成访问链接失败", ex);
            }
        }
        /// <summary>
        /// 自动续期签名（后台服务）
        /// </summary>
        public string GetSignedUrl(string objectKey)
        {
            // 检查缓存中是否有未过期的URL
            if (_urlCache.TryGetValue(objectKey, out var cached) && cached.Expiry > DateTime.UtcNow)
            {
                return cached.Url;
            }

            // 生成新签名并缓存
            var newUrl = GenerateSignedUrl(objectKey);
            _urlCache[objectKey] = (DateTime.UtcNow.AddDays(29), newUrl); // 提前1天刷新
            return newUrl;
        }
        /// <summary>
        /// 生成长期有效的访问URL（使用CDN域名）
        /// </summary>
        public string GeneratePublicUrl(string objectKey)
        {
            // 统一路径格式
            objectKey = objectKey.Replace("\\", "/").TrimStart('/');

            // 使用CDN域名
            var cdnDomain = _config["COS:CdnDomain"];
            if (!string.IsNullOrEmpty(cdnDomain))
            {
                return $"https://{cdnDomain}/{objectKey}";
            }

            // 使用默认存储桶域名
            return $"https://{_config["COS:Bucket"]}.cos.{_config["COS:Region"]}.myqcloud.com/{objectKey}";
        }

        public void ValidateCosConfig()
        {
            var requiredKeys = new[] { "SecretId", "SecretKey", "Region", "Bucket", "AppId" };
            var missingKeys = new List<string>();

            foreach (var key in requiredKeys)
            {
                if (string.IsNullOrEmpty(_config[$"COS:{key}"]))
                {
                    missingKeys.Add(key);
                }
            }

            if (missingKeys.Count > 0)
            {
                throw new Exception($"缺少必要的COS配置: {string.Join(", ", missingKeys)}");
            }

            // 验证SecretId和SecretKey格式
            if (!_config["COS:SecretId"].StartsWith("AKID") || _config["COS:SecretKey"].Length != 32)
            {
                throw new Exception("COS SecretId或SecretKey格式不正确");
            }
        }
        private class BucketConfig
        {
            public string BucketName { get; set; }
            public string Region { get; set; }
            public string BaseUrl { get; set; }
        }
    }

    // Define the missing CosException class  
    public class CosException : Exception
    {
        public CosException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
