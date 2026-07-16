# InteriorDesignWeb 项目接续总览

> 当前快照：2026-07-14  
> 目标：让新的开发者或 AI 在较短时间内理解项目当前态，并安全接续开发。  
> 事实优先级：当前代码与数据库实况 > 本文 > `DEVELOPMENT_ROADMAP.md` > `PROJECT_MEMORY.md` 中的历史记录。

## 1. 一句话定位与当前阶段

InteriorDesignWeb 是一个 AI 室内设计工作台。用户可以检索设计图库、创建方案和房间、通过 ComfyUI 生成或编辑图片/视频、将结果保存到方案，并使用 AI 设计助手形成设计方向；管理员可以管理用户、权限、额度、图库、AI 规则和系统状态。

项目已进入“核心功能基本完成、服务器端到端验收与可靠性收尾”阶段，不是从零开发阶段。

当前重点：

1. 完成服务器真实环境的 AI 生图、助手、COS、鉴权验收；
2. 用新增日志锁定剩余故障；
3. 补数据库、COS、ComfyUI 集成测试；
4. 再进行组件拆分、视觉精修和新产品能力。

## 2. 新接手者先做什么

1. 运行 `git status --short`。当前工作区包含大量未提交开发成果，禁止覆盖或回滚不属于当前任务的修改。
2. 阅读本文、`DEVELOPMENT_ROADMAP.md` 和与任务相关的专项文档。
3. 检查 `InteriorDesignWeb/database/migration-order.json` 与实际 SQL 文件、服务器 `schema_migrations` 是否一致。
4. 修改前从前端路由追到 API、Service、Entity 和后台 Worker，不要只修界面表现。
5. 修改后至少运行后端测试和前端生产构建；涉及数据库、COS 或 ComfyUI 时还要做真实环境验收。

## 3. 整体架构

```text
浏览器
  React + React Router + Zustand
        |
        | 同源 /api，HttpOnly Cookie
        v
ASP.NET Core 8
  Controller -> Service -> EF Core -> MySQL
                     |       |
                     |       +-> 用户、方案、任务、额度、助手、审计数据
                     |
                     +-> ComfyUI HTTP/WebSocket
                     +-> 腾讯云 COS（普通图库桶 / AI 结果桶）

后台服务
  AIJobBackgroundWorker       任务恢复和轮询兜底
  ComfyUIProgressListener     ComfyUI WebSocket 实时进度
  AIAssetCleanupWorker        清理未被方案保留的 AI 资产
  UserSessionCleanupWorker    清理过期登录会话
```

| 层 | 技术 |
| --- | --- |
| 前端 | React 19、TypeScript、Vite 7、Tailwind CSS 4、Zustand、React Router 6 |
| 后端 | ASP.NET Core 8、EF Core 8、Pomelo MySQL、JWT Bearer |
| 数据库 | MySQL 5.7 兼容配置 |
| AI 生成 | 自建 ComfyUI Server，HTTP 提交 + WebSocket 进度 + HTTP 历史/结果 |
| AI 助手 | OpenAI-compatible `/chat/completions` |
| 存储 | 腾讯云 COS，普通图库和 AI 结果使用不同配置 |
| 测试 | xUnit，目前 30 项测试 |

主要入口：

- 后端：`InteriorDesignWeb/Program.cs`
- 前端路由：`InteriorDesignWeb/wwwroot/src/App.tsx`
- 数据模型：`InteriorDesignWeb/Data/DesignHubContext.cs`
- AI 工作流：`InteriorDesignWeb/Services/AI/WorkflowRegistry.cs`
- 生产发布：`InteriorDesignWeb/InteriorDesignWeb.csproj`

## 4. 页面与前端路由

| 路由 | 功能 | 当前状态 |
| --- | --- | --- |
| `/` | 首页和产品介绍 | 基础完成；AI 案例素材仍待完善 |
| `/app/generate/:mode` | AI 生成/编辑/视频工作台 | 已实现，主链路待服务器持续验收 |
| `/app/generate/:mode/jobs/:jobId` | 恢复指定 AI 任务 | 已实现 |
| `/app/gallery` | 普通图库搜索、固定分页、大图 | 已实现，待云端 COS 验收 |
| `/app/new` | 新建方案 | 已实现 |
| `/app/projects` | 方案列表 | 已实现 |
| `/app/projects/:projectId` | 通过路由打开方案抽屉 | 已实现 |
| `/app/assistant` | AI 设计助手入口 | 代码完成，真实模型兼容性仍在定位 |
| `/app/assistant/:conversationId` | 恢复助手对话 | 已实现 |
| `/app/admin` | 管理后台 | 第一版完成，仅 Administrator 可访问 |

## 5. 模块关键链路

### 5.1 登录、会话与权限

```text
管理员创建账号
  -> 用户 POST /api/User/login
  -> 校验密码和账号启用状态
  -> 签发短期 Access JWT + 14 天 Refresh Token
  -> 两者写入 HttpOnly Cookie
  -> 每次鉴权复查用户 IsEnabled、AuthVersion、Role
  -> Access 过期时 POST /api/User/refresh 轮换 Refresh Token
  -> 注销/禁用/改密码/改角色时撤销会话
```

关键事实：

- 角色为 `FreeUser`、`Member`、`PremiumMember`、`Administrator`。
- Access Token 实际被限制为 15–120 分钟；“两周免重复登录”由 14 天 Refresh Session 实现。
- Cookie 为 `HttpOnly`；生产环境 `Secure`；当前使用 `SameSite=Strict`。
- Refresh Token 只保存 SHA-256 摘要到 `usersessions`，每次刷新都会轮换。
- JWT 含 `auth_version`；用户权限、密码或启用状态改变后旧 Token 立即失效。
- 公开注册已禁用：旧 `Register` Action 标记为 `NonAction`，正式新用户通过管理员后台创建。
- 登录接口有限流和未知用户等时密码验证，降低撞库与用户名枚举风险。

### 5.2 图库

```text
用户进入 /app/gallery
  -> GET /api/Images/search?keyword&seed&pageToken
  -> MySQL 按名称/标签筛选
  -> seed 生成稳定随机排序
  -> Data Protection 保护分页游标
  -> 前端保存已访问页面快照和 URL 参数
  -> GET /api/Images/{id}/file 受控读取普通 COS
  -> Blob URL 展示缩略图/大图
```

已具备：关键词/标签搜索、稳定随机排序、上一页固定、随机新页、路由恢复、大图缩放拖动。普通图库软删除后不再公开搜索，但被方案引用的图片仍可通过方案媒体接口读取。

### 5.3 方案、房间和方案图片

```text
创建 Project
  -> 按角色检查方案数量
  -> 创建层级 ProjectRoom
  -> 从普通图库或 AI 结果加入 ProjectImage
  -> 验证项目、房间、图片所有权与额度
  -> 保存 RelationID、来源和 RoomID
  -> /api/projects/{projectId}/images/{relationId}/file
     在后端按来源选择普通 COS 或 AI COS
```

核心表：`projects`、`projectrooms`、`projectimages`。

关键约束：

- `ProjectImage.ImageID` 与 `AiImageID` 必须二选一。
- UI 和 DTO 必须保留 `sourceType`，不能用一个裸 ID 混合普通图和 AI 图。
- 浏览器不应自行拼 COS URL，统一通过受控媒体接口读取。
- AI 图片加入方案后标记为 `retained`，删除任务不能删除方案中的图片。
- 方案支持普通图和 AI 图封面字段；房间支持父子层级。

### 5.4 AI 生成工作台

```text
GET /api/ai/generations/options
  -> 前端选择工作流并填写参数/上传素材
  -> POST /api/ai/generations/upload 上传 ComfyUI 输入
  -> POST /api/ai/generations
  -> 权限、工作流、并发、方案绑定、额度和幂等检查
  -> 同一数据库事务创建 AIJob + 预占 UsageRecord
  -> 提交 ComfyUI /prompt
  -> 保存 ProviderJobId
  -> WebSocket 推送进度，后台轮询兜底
  -> 完成后读取 history/output
  -> 下载结果、生成缩略图、上传 AI COS
  -> 保存 aigenerationjobimages
  -> 前端通过任务结果接口和鉴权媒体接口展示
```

当前 7 个工作流：

| Code | 功能 | 输出 | 单次额度 |
| --- | --- | --- | ---: |
| `api_grok_image_edit` | 简单文生图 | 图片 | 4 |
| `api_seedream_image_edit` | 参考图风格迁移 | 图片 | 7 |
| `api_bria_image_edit` | 文本图片编辑 | 图片 | 9 |
| `api_banana_image` | 双图高级参考编辑 | 图片 | 21 |
| `api_luma_image_edit` | 高级文字图像编辑 | 图片 | 21 |
| `api_seedance2` | 首尾帧漫游视频 | 视频 | 322 |
| `api_veo3` | 单图漫游视频 | 视频 | 343 |

系统负面提示词由后端 `NegativePromptPolicy` 注入，并放在用户负面提示词之前；不会自动显示在前端输入框。

### 5.5 AI 任务、进度和删除

```text
侧边栏 GET /api/ai/jobs
  -> 最近任务列表
  -> 点击进入 /app/generate/:mode/jobs/:jobId
  -> GET /api/ai/jobs/{jobId}
  -> SSE /api/ai/jobs/{jobId}/events 获取实时进度
  -> SSE/ComfyUI WebSocket 失败时浏览器和后台轮询兜底
```

终态任务删除目前是真删除任务条目：

- 删除 `aigenerationjobs`；
- 结果图片解除 `JobId`；
- 已被方案或封面引用的图片继续保留；
- 未被引用的图片进入 `cleanup_pending`，七天后由 `AIAssetCleanupWorker` 清理；
- 运行中任务必须先取消或等待终态。

注意：`AIJobsController` 中仍有“软删除”的旧注释，与当前 `AIJobService.HardDeleteAsync` 行为不一致，后续应清理该注释。

### 5.6 AI 设计助手

助手已升级为“受控多 Agent 编排 + 结构化产物 + 用户确认生图”，不是允许模型任意执行工具的开放式 Agent。

```text
保存对话、用户消息和可选图片附件
  -> 权限与 5 小时 Token 检查
  -> orchestrator / MiniMax 判断阶段
  -> 有图片时强制 vision / 火山方舟先分析
  -> designer / DeepSeek 生成设计产物
  -> prompt-engineer / DeepSeek 生成工作流与提示词建议
  -> 保存 Agent run、事件、artifact、brief 和 proposed action
  -> 用户确认后调用现有 AIGenerationService
  -> 复用 AIJob、ComfyUI、COS 和图片展示
  -> 用户可主动调用 vision + result-evaluator 评估实际结果
```

Agent 配置位于 `InteriorDesignWeb/AIAgent`，运行表为 `assistantagentruns`、`assistantagentevents`、
`assistantagentartifacts`，附件表为 `assistantattachments`。附件存入 AI COS 私有桶，浏览器通过鉴权媒体接口读取，
视觉模型只收到 15 分钟短时签名 URL。带图片请求禁止回退到看不到图片的旧助手。完整日志与故障定位详见
`ASSISTANT_DIAGNOSTICS.md`。

### 5.7 管理员后台

`/app/admin` 是现有网站的受保护子页面，不另开端口。前端隐藏入口不构成权限边界，后端 `/api/admin/*` 统一要求 `Administrator`。

已具备：

- 数据总览和近期开销/任务趋势；
- 用户创建、查询、角色、启停、密码和会话撤销；
- 用户 AI 生图额度与助手 Token 额度管理；
- 普通图库 COS 上传、搜索、编辑标签、上下架、删除和恢复；
- 应用、数据库、COS、AI Provider 健康状态；
- 网站 API 清单；
- 管理员操作审计；
- AI 模型脱敏配置状态与连接测试；
- 助手业务规则草稿/发布；
- 角色级和用户级 AI 权限、工作流白名单、并发控制。

## 6. 关键数据关系

```text
User
  ├─ UserSession
  ├─ UserQuota
  ├─ UsageRecord
  ├─ Project
  │    ├─ ProjectRoom
  │    └─ ProjectImage ── Image 或 AiGenerationJobImage（二选一）
  ├─ AiGenerationJob
  │    └─ AiGenerationJobImage
  └─ AssistantConversation
       ├─ AssistantMessage
       └─ AssistantGenerationAction ── 可关联 AiGenerationJob

AI 权限治理
  AssistantPolicyVersion
  AiRolePolicy
  AiUserPolicyOverride
```

额度分两类：

- AI 生图额度：`UserQuota.TotalUnits/UsedUnits/RemainingUnits` + `UsageRecord` 流水；
- AI 助手额度：固定 5 小时窗口的 Token 上限与已使用量。

## 7. 鉴权、媒体与安全边界

- 用户私有项目、房间、AI 任务、AI 结果和助手对话均必须复查 `UserID`。
- 普通图库是网站公共素材，但删除/下架和上传管理由管理员控制。
- 普通 COS 与 AI COS 的对象路径不能混用。
- 前端媒体使用 `credentials: include` 请求并转为 Blob URL；401 时尝试刷新会话。
- API Key、数据库密码、JWT Secret、COS Secret 只能在本机 User Secrets 或服务器环境变量/密钥服务中保存。
- 生产异常响应只返回错误码、用户可读消息和 `RequestId`，不向浏览器返回堆栈。
- 管理操作写入 `adminauditlogs`。

## 8. 后台可靠性设计

- `AIJobBackgroundWorker`：扫描数据库中的非终态任务，服务重启后可继续恢复。
- `ComfyUIProgressListener`：后端连接 ComfyUI `/ws`，把进度写库并通过 SSE 推给浏览器。
- `AIJobRefreshCoordinator`：防止单进程内 WebSocket 和轮询同时完成同一任务。
- `AIResultService`：以 OutputKey 等机制避免重复保存结果。
- `AIAssetCleanupWorker`：对未被方案引用的脱离任务图片提供七天清理宽限期。
- `UserSessionCleanupWorker`：清理过期或撤销的登录会话。

尚未完成：数据库级任务租约和多实例互斥。当前协调器只在单进程内有效；部署多个网站实例时可能重复恢复同一 ComfyUI 任务。

## 9. 部署和配置

推荐生产拓扑：

```text
Internet -> Nginx 80/443 -> ASP.NET Core 127.0.0.1:5000
OpenClaw                     127.0.0.1:18789
ComfyUI                      内网或 127.0.0.1:8188
MySQL                        不直接暴露公网
```

生产要点：

- 使用 Nginx 终止 HTTPS，并正确传递 `X-Forwarded-For`、`X-Forwarded-Proto`。
- ASP.NET Core 已启用 Forwarded Headers、HSTS、压缩、SPA 回退和健康检查。
- 前后端统一同源 `/api`；不要在构建中写占位正式域名。
- `dotnet publish` 会自动运行前端构建，只发布 `wwwroot/dist`，并拒绝 PDB、前端源码和开发配置进入生产包。
- ComfyUI 若经过反向代理，必须允许 `/ws` WebSocket Upgrade。
- Data Protection Keys 应持久化，否则图库分页游标在重启或换机后失效。
- 必需生产配置包括数据库、JWT、COS、ComfyUI；启用助手时还需要 `Assistant__Enabled/BaseUrl/Model/ApiKey`。

详细步骤见 `DEPLOYMENT.md` 和 `SECURITY_SETUP.md`。

## 10. 当前完成度

| 模块 | 代码完成度 | 当前判断 |
| --- | ---: | --- |
| 登录、刷新、会话撤销 | 高 | 已实现，需持续做生产 Cookie/反代验收 |
| 图库搜索与大图 | 高 | 已实现，云端普通 COS 权限需要验收 |
| 方案与房间 | 高 | 主功能完成 |
| 普通图/AI 图加入方案 | 高 | 统一媒体链路完成，需真实图片回归 |
| AI 生成工作台 | 高 | 功能完成；近期额度事务已修复，需服务器真实任务验收 |
| 任务历史、恢复、进度 | 高 | WebSocket + SSE + 轮询兜底已接入 |
| 任务真删除和图片保留 | 高 | 已实现，控制器旧注释待清理 |
| 管理员后台 | 中高 | 第一版全面，仍需完整人工验收 |
| AI 助手 | 中高 | 多 Agent、图片理解、多房间推进和结果评估已接入；需真实四模型与 COS 联调 |
| 暗色视觉与动效 | 中高 | 第一轮完成，仍需响应式和视觉走查 |
| 首页内容 | 中低 | 基础页面存在，AI 案例素材未完成 |
| 自动化测试 | 中低 | 33 项单元/安全测试；缺少真实数据库、COS、模型、ComfyUI 和浏览器 E2E |

## 11. 当前必须关注的问题

### P0：服务器真实闭环验收

- 重新发布最新前后端并重启服务；
- 验证普通用户登录刷新、文生图、任务进度、结果图片、加入方案和方案大图；
- 验证助手聊天、结构化建议、确认生图和结果回显；
- 使用 `RequestId`、助手请求 ID、JobId 串联日志。

### P0：迁移清单与文件不一致

当前 `migration-order.json` 引用了以下本地不存在的文件：

- `20260712_gallery_management.sql`
- `20260714_ai_governance.sql`
- `20260715_assistant_experience_phase2.sql`

新数据库按清单迁移时会失败。服务器可能已手动执行相应结构，但不能据此认为仓库完整。下一步应先从已确认来源恢复这三个 SQL，或根据实际表结构重建可重复执行的迁移并核对 `schema_migrations`；不要直接从清单删除名称，也不要伪造 checksum。

### P1：多 Agent 真实环境验收

依次验证 MiniMax 前台、DeepSeek Flash、DeepSeek Pro 和火山视觉四个配置；覆盖纯文本、上传房间图、
视觉转设计、确认生图、结果评估、Token 不足、模型超时和 COS 私有桶访问。使用 RunId、ClientRequestId、
ProviderRequestId 与 JobId 串联日志。

### P1：集成测试

优先补：

1. 登录/刷新/禁用账号；
2. AIJob + UsageRecord 同事务与失败退款；
3. 普通图/AI 图媒体所有权；
4. 任务真删除后的保留和清理状态；
5. 助手 proposed action 到 AIJob；
6. 浏览器深层路由、401 刷新和图片显示。

### P2：维护性和产品完善

- 拆分体积较大的 `GeneratePage.tsx`、`AdminPage.tsx` 和图库页面；
- 集中定义状态、错误码、媒体 DTO 和 API 字段规范；
- 增加路由级代码拆分，当前前端仍打包成约 700 KB 的单 HTML；
- 完成响应式与视觉走查；
- 准备首页 6–8 组高质量案例素材；
- 后续为助手增加结构化长期记忆、可检索项目知识和真实材料预算数据；图片理解与可版本化 Skill 已有第一版。

## 12. 开发不可破坏的规则

1. 不得把普通图片 ID 和 AI 图片 ID 合并成无来源的单一 ID。
2. 不得让浏览器根据 COS 对象路径猜 URL；媒体由后端鉴权和分桶。
3. 项目、房间、任务、结果和助手写操作必须校验当前用户所有权。
4. 数据库与外部 Provider/COS 的跨系统操作必须有幂等、补偿和可恢复策略。
5. MySQL 已启用 `EnableRetryOnFailure`；手动事务必须放在 `CreateExecutionStrategy().ExecuteAsync(...)` 中。
6. 删除任务只删除任务条目；被方案/封面引用的图片必须继续保留。
7. 系统负面提示词在后端注入，不显示在用户输入框，也不能被用户负面词覆盖。
8. AI 助手输出只能形成后端可校验的建议；执行生图时必须重新鉴权，不能让模型直接控制 ComfyUI。
9. 不记录密钥、Cookie、Token 原文、完整系统提示词或完整模型原始内容。
10. 不在脏工作区执行破坏性 Git 操作，不覆盖现有未提交成果。

## 13. 验证命令

```powershell
# 后端
dotnet test InteriorDesignWeb.Tests/InteriorDesignWeb.Tests.csproj -c Release

# 前端
cd InteriorDesignWeb/wwwroot
npm run build

# 数据库迁移（先确认清单缺失文件问题）
powershell -ExecutionPolicy Bypass -File scripts/invoke-database-migrations.ps1

# 发布包
powershell -ExecutionPolicy Bypass -File scripts/test-publish-package.ps1 -PublishPath <发布目录>
```

## 14. 文档职责

| 文档 | 用途 |
| --- | --- |
| `PROJECT_HANDOFF.md` | 当前项目事实与接续入口，结构性修改后更新 |
| `DEVELOPMENT_ROADMAP.md` | 产品排期、状态和验收标准 |
| `PROJECT_MEMORY.md` | 历史故障、根因和设计决策，不宜当作当前状态唯一来源 |
| `ASSISTANT_DIAGNOSTICS.md` | AI 助手链路和日志对照 |
| `DEPLOYMENT.md` | 生产部署和服务器操作 |
| `SECURITY_SETUP.md` | 密钥、JWT、Cookie 和安全配置 |
