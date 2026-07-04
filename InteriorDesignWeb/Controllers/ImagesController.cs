using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs;
using InteriorDesignWeb.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLaborsImage = SixLabors.ImageSharp.Image;
using SixLaborsSize = SixLabors.ImageSharp.Size;
using System.Security.Cryptography;
using COSXML.Model.Object;
using Newtonsoft.Json.Linq;
using COSXML.Auth;
using InteriorDesignWeb.Services;
using TencentCloud.Tione.V20211111.Models;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;
using System.Configuration;
using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.DataProtection;


namespace InteriorDesignWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly CosService _cosService;
        private readonly DesignHubContext _context;
        private readonly ILogger<ImagesController> _logger;
        private readonly IConfiguration _config;
        private readonly ITimeLimitedDataProtector _protector;

        public ImagesController(
            CosService cosService,
            DesignHubContext context,
            ILogger<ImagesController> logger,
            IConfiguration config,
            IDataProtectionProvider dataProtectionProvider)
        {
            _cosService = cosService ?? throw new ArgumentNullException(nameof(cosService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _protector = dataProtectionProvider.CreateProtector("search-cursor").ToTimeLimitedDataProtector();
        }

        [HttpPost("upload")]
        [SwaggerOperation(
           Summary = "上传设计图片",
           Description = "支持上传图片并附加元数据",
           OperationId = "UploadDesignImage"
        )]
        [Consumes("multipart/form-data")] // 明确声明内容类型
        public async Task<IActionResult> Upload(
           [FromForm] ImageUploadRequest request) // 新增可选参数
        {
            try
            {
                // ==== 1. 参数校验 ====
                if (request?.file == null || request.file.Length == 0) // 修复 CS8602 错误
                    return BadRequest("文件不能为空");
                if (string.IsNullOrEmpty(request?.roomType)) // 修复 CS8601 错误
                    return BadRequest("房间类型必填");

                // ==== 2. 上传主图 ====
                await using var fileStream = request.file.OpenReadStream();
                var (cosPath, fileHash) = await _cosService.UploadImageAsync(
                    fileStream,
                    request.file.FileName,
                    request.roomType // 确保 roomType 不为 null
                );

                // ==== 3. 生成并上传缩略图 ====
                fileStream.Position = 0;
                var thumbnailPath = await _cosService.GenerateThumbnailAsync(
                    fileStream,
                    cosPath
                );

                // ==== 4. 构建数据库记录 ====
                var image = new Models.Entities.Image
                {
                    FileName = request.file.FileName, // 修复 CS8602 错误
                    FilePath = cosPath,
                    ThumbnailPath = thumbnailPath,
                    FileSize = (int)request.file.Length, // 修复 CS8602 错误
                    FileHash = fileHash,
                    Room = request.roomType, // 确保 roomType 不为 null
                    Tags = BuildTagsString(request)
                };

                // ==== 5. 持久化存储 ====
                _context.images.Add(image);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    ImageID = image.ImageID,
                    OriginalUrl = $"{_config["COS:BaseUrl"]}/{cosPath}",
                    ThumbnailUrl = $"{_config["COS:BaseUrl"]}/{thumbnailPath}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"上传失败: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetImageInfo(int id)
        {
            var image = await _context.images
                .Where(i => i.ImageID == id)
                .Select(i => new
                {
                    i.ImageID,
                    i.FileName,
                    i.FilePath,
                    i.ThumbnailPath,
                    Room = i.Room.ToString(),
                    i.Tags,
                    i.UploadTime
                })
                .FirstOrDefaultAsync();

            if (image == null) return NotFound();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    image.ImageID,
                    image.FileName,
                    ThumbnailUrl = Url.Action("GetImageFile", new { id = image.ImageID, type = "thumbnail" }),
                    FullImageUrl = Url.Action("GetImageFile", new { id = image.ImageID }),
                    image.Room,
                    image.Tags
                }
            });
        }

        // 在类级别声明 categoryFields
        private static readonly List<string> categoryFields = new List<string> { "户型", "风格", "材质", "包含元素", "其他" ,"式"};

        [Authorize(Roles = "FreeUser, Administrator, Member, PremiumMember")]
        [HttpGet("search")]
        public async Task<IActionResult> SearchImages([FromQuery] ImageSearchRequest request, CancellationToken ct)
        {
            // 1) 入参与兜底
            if (request == null)
                return BadRequest(new { Success = false, Message = "请求无效" });

            request.PageSize = Math.Clamp(request.PageSize, 1, 100);

            request.Keyword = request.Keyword?.Trim();
            if (string.IsNullOrWhiteSpace(request.Keyword))
            {
                if (!string.IsNullOrWhiteSpace(request.Q))
                    request.Keyword = request.Q!.Trim();
                else if (HttpContext.Request.Query.TryGetValue("keyword", out var k) && !string.IsNullOrWhiteSpace(k))
                    request.Keyword = k.ToString().Trim();
            }
            if (string.IsNullOrWhiteSpace(request.Keyword))
                return BadRequest(new { Success = false, Message = "搜索关键词不能为空" });

            try
            {
                // 2) 分词与文本规范化
                var (cleanKeyword, wordList, charList) = ProcessKeywordSafe(request.Keyword!);
                if (!wordList.Any() && !charList.Any())
                    return BadRequest(new { Success = false, Message = "关键词无有效内容" });

                // 3) 确定 seed：前端传则用之；否则生成并回传
                var seed = !string.IsNullOrWhiteSpace(request.Seed)
                    ? request.Seed!
                    : NewSeed();

                // 4) 解析分页游标并校验 Keyword/Seed 一致性
                var hasCursor = TryDecodeCursor(request.PageToken, out var cursor);
                if (hasCursor && cursor != null)
                {
                    if (!string.Equals(cursor.Keyword, cleanKeyword, StringComparison.Ordinal) ||
                        !string.Equals(cursor.Seed, seed, StringComparison.Ordinal))
                    {
                        return BadRequest(new { Success = false, Message = "分页上下文不一致，请重新搜索" });
                    }
                }

                // 5) 初筛（DB 侧过滤 + LIKE 转义 + 快照上界）
                var query = _context.images.AsNoTracking().AsQueryable();

                if (hasCursor && cursor != null)
                {
                    query = query.Where(i => i.UploadTime <= cursor.SnapshotMaxCreatedAt);
                }

                // 组装 OR 条件，限制项数避免 SQL 过长
                const int termCap = 8;
                var terms = new List<string>();
                terms.AddRange(charList.Take(termCap));
                terms.AddRange(wordList.Take(termCap));

                if (terms.Count > 0)
                {
                    var predicate = PredicateFalse<Models.Entities.Image>();
                    foreach (var t in terms.Select(EscapeLike))
                    {
                        var tt = t; // 闭包防坑
                        predicate = Or(predicate,
                            i => EF.Functions.Like((i.Room ?? "").ToLower(), $"%{tt.ToLower()}%", "[")
                            || EF.Functions.Like((i.Tags ?? "").ToLower(), $"%{tt.ToLower()}%", "["));
                    }
                    query = query.Where(predicate);
                }

                // 候选硬上限 + 初始稳定排序
                const int candidateCap = 2000;
                var candidates = await query
                    .OrderByDescending(i => i.UploadTime)
                    .Take(candidateCap)
                    .Select(i => new
                    {
                        i.ImageID,
                        i.FileName,
                        i.Room,
                        i.UploadTime,
                        i.Tags
                    })
                    .ToListAsync(ct);

                // 快照上界：没有游标就取本批最大 UploadTime
                var snapshotMaxCreatedAt = hasCursor && cursor != null
                    ? cursor.SnapshotMaxCreatedAt
                    : (candidates.Any() ? candidates.Max(x => x.UploadTime) : DateTime.UtcNow);

                // 6) 内存打分
                var scored = new List<(Models.Entities.Image image, int score)>(candidates.Count);
                foreach (var i in candidates)
                {
                    ct.ThrowIfCancellationRequested();

                    var score = 0;
                    score += ComputeScoreSafe(i.Room ?? "", wordList, charList) * 2;
                    score += ComputeScoreSafe(i.Tags ?? "", wordList, charList) * 1;

                    if (score >= 3)
                    {
                        scored.Add((new Models.Entities.Image
                        {
                            ImageID = i.ImageID,
                            FileName = i.FileName,
                            Room = i.Room,
                            UploadTime = i.UploadTime,
                            Tags = i.Tags
                        }, score));
                    }
                }

                // 7) 稳定随机 + 排序
                var withRand = scored.Select(x => new
                {
                    x.image,
                    x.score,
                    Rand = StableHash64($"{x.image.ImageID}-{seed}"),
                    Id = x.image.ImageID
                });

                IEnumerable<dynamic> ordered = withRand
                    .OrderByDescending(r => r.score)
                    .ThenBy(r => r.Rand)
                    .ThenBy(r => r.Id);

                if (hasCursor && cursor != null)
                {
                    ordered = withRand.Where(r =>
                                (r.score < cursor.LastScore) ||
                                (r.score == cursor.LastScore && r.Rand > cursor.LastRand) ||
                                (r.score == cursor.LastScore && r.Rand == cursor.LastRand && r.Id > cursor.LastId))
                            .OrderByDescending(r => r.score)
                            .ThenBy(r => r.Rand)
                            .ThenBy(r => r.Id);
                }

                var page = ordered.Take(request.PageSize).ToList();
                var hasMore = page.Count == request.PageSize;

                var nextPageToken = hasMore
                    ? EncodeCursor(new SearchCursor
                    {
                        LastScore = page.Last().score,
                        LastRand = page.Last().Rand,
                        LastId = page.Last().Id,
                        SnapshotMaxCreatedAt = snapshotMaxCreatedAt,
                        Keyword = cleanKeyword,
                        Seed = seed
                    })
                    : null;

                // 8) 响应
                return Ok(new
                {
                    Success = true,
                    Seed = seed,
                    HasMore = hasMore,
                    NextPageToken = nextPageToken,
                    FilteredCount = scored.Count, // 打分后且≥3 的数量
                    Data = page.Select(x => new
                    {
                        x.image.ImageID,
                        x.image.FileName,
                        ThumbnailUrl = Url.Action("GetImageFile", new { id = x.image.ImageID, type = "thumbnail" }),
                        FullImageUrl = Url.Action("GetImageFile", new { id = x.image.ImageID }),
                        x.image.Room,
                        x.image.UploadTime,
                        x.image.Tags,
                        Score = x.score
                    })
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { Success = false, Message = "请求已取消" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索失败: {Keyword}", request.Keyword);
                return StatusCode(500, new { Success = false, Message = "搜索服务暂时不可用" });
            }
        }


        // ========== 工具函数 ==========

        // 游标结构：包含排序三元组、快照、关键词与 seed
        internal class SearchCursor
        {
            public int LastScore { get; set; }
            public ulong LastRand { get; set; }
            public int LastId { get; set; }
            public DateTime SnapshotMaxCreatedAt { get; set; }
            public string Keyword { get; set; } = "";
            public string Seed { get; set; } = "";
        }

        private string EncodeCursor(SearchCursor c)
        {
            var json = JsonSerializer.Serialize(c);
            var bytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = _protector.Protect(bytes, TimeSpan.FromHours(24)); // 游标有效期可调
            return Base64UrlEncode(protectedBytes);
        }

        private bool TryDecodeCursor(string? token, out SearchCursor? cursor)
        {
            cursor = null;
            if (string.IsNullOrWhiteSpace(token)) return false;
            try
            {
                var protectedBytes = Base64UrlDecode(token);
                var bytes = _protector.Unprotect(protectedBytes, out _);
                cursor = JsonSerializer.Deserialize<SearchCursor>(bytes);
                return cursor != null;
            }
            catch { return false; }
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        // 生成新的种子（首次搜索或主动洗牌用）
        private static string NewSeed()
        {
            Span<byte> b = stackalloc byte[8];
            RandomNumberGenerator.Fill(b);
            var ul = BitConverter.ToUInt64(b);
            return ul.ToString("x16");
        }

        // 规范化：小写、全角转半角、Trim
        private static string NormalizeText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim().ToLowerInvariant();
            var chars = s.Select(c => c >= 65281 && c <= 65374 ? (char)(c - 65248) : (c == 12288 ? ' ' : c)).ToArray();
            return new string(chars);
        }

        // 供 EF.Like 使用的归一化包装
        private static string Norm(string? s) => NormalizeText(s ?? "");

        // LIKE 转义，配合 escape='['
        private static string EscapeLike(string s)
        {
            return s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        // 分词（保留你的 bigram + 单字，做了规范化）
        private (string cleanText, List<string> words, List<string> chars) ProcessKeywordSafe(string keyword)
        {
            var cleaned = NormalizeText(keyword).Replace(",", "").Replace("，", "").Replace(" ", "");
            var charList = cleaned.ToCharArray().Select(c => c.ToString()).Distinct().ToList();
            var wordList = new List<string>();
            for (int i = 0; i < cleaned.Length - 1; i++)
            {
                var word = cleaned.Substring(i, 2);
                if (!wordList.Contains(word)) wordList.Add(word);
            }
            return (cleaned, wordList, charList);
        }

        // 计算得分（规范化 + Contains Ordinal）
        private int ComputeScoreSafe(string text, List<string> words, List<string> chars)
        {
            int score = 0;
            var cleanedText = RemoveCategoryKeys(NormalizeText(text));

            foreach (var word in words)
                if (cleanedText.Contains(word, StringComparison.Ordinal)) score += 3;

            foreach (var c in chars)
                if (cleanedText.Contains(c, StringComparison.Ordinal)) score += 1;

            var tags = cleanedText.Split(';')
                           .SelectMany(t => t.Split('，'))
                           .Select(t => t.Trim())
                           .Where(t => !string.IsNullOrEmpty(t))
                           .ToList();

            foreach (var tag in tags)
                if (tag.Length == 1 && chars.Contains(tag)) score += 1;

            return score;
        }

        // 去除“户型”、“风格”等前缀
        private string RemoveCategoryKeys(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var t = text.Replace("：", ":"); // 全角冒号
            foreach (var key in categoryFields)
            {
                var k = NormalizeText(key);
                t = t.Replace($"{k}:", "", StringComparison.Ordinal);
                t = t.Replace($"{k}: ", "", StringComparison.Ordinal);
            }
            return t;
        }

        // FNV-1a 64位：稳定随机
        private static ulong StableHash64(string s)
        {
            const ulong fnvOffset = 1469598103934665603UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong hash = fnvOffset;
            foreach (var ch in s)
            {
                hash ^= ch;
                hash *= fnvPrime;
            }
            return hash;
        }

        // 表达式工具
        private static Expression<Func<T, bool>> PredicateFalse<T>() => f => false;

        private static Expression<Func<T, bool>> Or<T>(
            Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var parameter = Expression.Parameter(typeof(T));
            var left = new ReplaceParameterVisitor(expr1.Parameters[0], parameter).Visit(expr1.Body)!;
            var right = new ReplaceParameterVisitor(expr2.Parameters[0], parameter).Visit(expr2.Body)!;
            return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left, right), parameter);
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParam;
            private readonly ParameterExpression _newParam;
            public ReplaceParameterVisitor(ParameterExpression oldParam, ParameterExpression newParam)
            {
                _oldParam = oldParam; _newParam = newParam;
            }
            protected override Expression VisitParameter(ParameterExpression node) =>
                node == _oldParam ? _newParam : base.VisitParameter(node);
        }
    



        [HttpGet("{id}/file")]
        [Produces("image/jpeg", "image/png", "image/gif")] // 声明支持的MIME类型
        public async Task<IActionResult> GetImageFile(
            int id,
            [FromQuery] string? type = null)
        {
            try
            {
                // 获取图片流
                var (stream, contentType) = await _cosService.GetImageStreamAsync(id, type);

                // 确定Content-Type
                var imageInfo = await _cosService.GetImageInfoAsync(id, type);
                var objectKey = type == "thumbnail" ? imageInfo.Url : imageInfo.Url;


                return File(stream, contentType);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Code = "NOT_FOUND", ex.Message });
            }
            catch (CosException ex)
            {
                return StatusCode(502, new { Code = "COS_ERROR", ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Code = "SERVER_ERROR", ex.Message });
            }
        }


        private static string BuildTagsString(ImageUploadRequest request)
        {
            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.houseType))
                tags.Add($"户型:{request.houseType}");
            if (!string.IsNullOrWhiteSpace(request.style))
                tags.Add($"风格:{request.style}");
            if (!string.IsNullOrWhiteSpace(request.material))
                tags.Add($"材质:{request.material}");
            if (!string.IsNullOrWhiteSpace(request.elements))
                tags.Add($"包含元素:{request.elements}");
            if (!string.IsNullOrWhiteSpace(request.other))
                tags.Add($"其他:{request.other}");
            return string.Join(";", tags);
        }
    }
}
