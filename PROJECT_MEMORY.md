# InteriorDesignWeb 项目记忆

> 最后更新：2026-07-14  
> 用途：为后续开发保留项目定位、核心链路、已确认问题、工程约束和优先级。每次完成结构性改造后应同步更新本文。

当前项目事实、模块链路和接续顺序优先阅读根目录 `PROJECT_HANDOFF.md`。本文包含较多历史故障与阶段性判断，不能脱离当前代码单独作为现状依据。

当前开发优先级、完成状态和待确认事项统一维护在根目录 `DEVELOPMENT_ROADMAP.md`。

## 0. 第一阶段实施状态（2026-07-10）

第一阶段图片主闭环已完成代码开发，前后端生产构建通过，尚需在实际数据库、JWT 和 COS 环境中做端到端验收。

已完成：

- AI 结果缩略图和原图改为通过 `/api/ai/jobs/{jobId}/results/{aiImageId}/file` 受控读取，并验证任务/图片所有权；
- 方案缩略图和原图改为通过 `/api/projects/{projectId}/images/{relationId}/file` 受控读取，由后端判断普通图或 AI 图及对应 COS；
- 前端新增统一 JWT 媒体加载器，将响应转换为 Blob URL，并提供有上限的缓存和回收；
- 生成结果卡片、大图、视频预览、再次编辑、方案卡片、方案大图、项目素材选择、历史素材选择均切换到新链路；
- AI 图片加入方案时验证所有权和房间归属，并保存真实 `RoomID`；
- 重复加入改为幂等返回，删除不存在关联不再触发空引用；
- `IsAddedToProject` 在删除关联后根据剩余关联重新计算；
- 增加普通图/AI 图方案关联的 EF 唯一索引定义和数据库升级脚本：`InteriorDesignWeb/database/20260710_project_image_media_phase1.sql`。
- 修复方案抽屉关闭期间的空引用：抽屉在 300ms 退场动画内保留稳定的 `displayProject` 快照，不能直接读取已经被父组件置空的 `project` 属性。
- 修复自动比例提交：选择“自动”时不向工作流写入 `aspectRatio`，由各工作流 JSON 使用自身默认比例。
- 侧边栏已增加最近 5 条 AI 任务，可跳转到独立任务路由恢复状态和结果。
- 图库已缓存浏览过的页面快照，并使用查询参数记录搜索关键词、页码、seed 和 token。
- 方案抽屉增加 `/app/projects/{projectId}` 路由，AI 助理占坑页增加 `/app/assistant` 路由。
- 后端不可连接时统一显示“无法连接到服务端，请稍后重试”，不再误报为生图失败；此问题与任务记录功能无关。
- AI任务记录支持软删除：`DELETE /api/ai/jobs/{jobId}` 只隐藏终态历史，不删除生成结果或方案图片；部署前需执行 `database/20260711_ai_job_soft_delete.sql`。
- 侧栏任务列表最多加载 20 条并占满剩余高度，任务卡片省略号菜单提供打开和删除操作。
- 大图查看已支持桌面端滚轮缩放、拖动、双击复位、适应窗口、原始尺寸和键盘操作；移动端使用 Pointer Events 实现双指缩放与单指拖动，缩放范围为 25%–500%。
- 暗色主题第一轮美化已完成：石墨黑背景层级、明亮钴蓝强调色、更高文字/边框对比度、统一表单原生样式和页面/弹窗/菜单动效；亮色主题仅做兼容性保持。
- 首页案例继续搁置；AI 助理占坑页已由第一阶段真实工作台替换。
- 生产部署路径已统一：Vite 固定输出 `wwwroot/dist`，生产 API 使用同源 `/api`；`dotnet publish` 自动执行前端构建且发布包不包含 `src`、npm 配置或开发环境配置；ASP.NET Core 仅托管 `dist` 并为非 API 的浏览器历史路由提供 SPA 回退。
- 第二批 P0 工程化已完成第一轮：React Router 增加统一错误页；新增 `InteriorDesignWeb.Tests` xUnit 项目，首批 8 项策略测试通过；数据库增加 `schema_migrations` 台账和校验和迁移工具；发布包增加自动与手动完整性检查。
- 管理员后台第一版位于 `/app/admin`，作为现有网站的受保护子页面部署，不开放独立端口。仅 `Administrator` 可见入口，真正的权限边界由 `/api/admin/*` 后端角色校验负责。
- 管理后台已覆盖数据总览、用户创建、角色/状态/密码管理、登录会话撤销、普通图库 COS 上传、系统与接口状态、管理员审计日志。角色、密码或启用状态变化会递增 `AuthVersion` 并撤销刷新会话，使旧访问令牌立即失效。
- 管理员数据升级脚本为 `InteriorDesignWeb/database/20260712_admin_management.sql`，已纳入迁移清单并在本机数据库执行；二次执行全部为 `SKIP`。自动测试目前共 15 项并通过，前端生产构建通过。
- 管理员图库管理使用 `20260712_gallery_management.sql` 增加普通图片软删除字段。删除规则固定为：被方案图片或封面引用时仅从公共图库下架；零引用时才删除数据库记录和未被其他记录复用的普通 COS 对象。下架图片不能再加入新方案，但原有方案通过方案媒体接口继续显示。
- AI 设计助手第一阶段位于 `/app/assistant` 与 `/app/assistant/{conversationId}`。持久化表为 `assistantconversations`、`assistantmessages`、`assistantgenerationactions`；聊天模型只生成结构化方案和生成建议，用户确认后由后端选择真实工作流并幂等创建 AIJob。生成结果可在所有权复检后自动加入绑定房间。
- 助手模型通过 OpenAI 兼容 HTTP 接口配置，密钥只允许放在服务器 `Assistant__ApiKey`；未配置或 `Assistant__Enabled=false` 时不影响网站其他功能，对话接口返回明确的未启用状态。
- 管理员新增用户曾因 MySQL `EnableRetryOnFailure` 与手动事务冲突而失败；用户和审计日志现合并为一次 `SaveChanges`，由 EF 隐式事务及重试策略管理。同类的房间删除和 AI 任务彻底删除也已移除手动事务。
- 生产启动已补 HSTS、响应压缩、可信代理配置、Data Protection 密钥持久化选项、缺少前端产物时快速失败，并调整日志中间件顺序以记录异常转换后的真实 HTTP 状态码。

部署/验收前必须：

1. 生产环境备份数据库，并通过统一迁移工具执行迁移清单（其中包含 `20260710_project_image_media_phase1.sql` 与 `20260712_admin_management.sql`）；前者会删除同一方案内重复图片关联，仅保留最早一条。
2. 重启后端，使新路由和依赖注入生效。
3. 使用真实账号逐项验收：新生成卡片、新生成大图、加入指定房间、方案卡片、方案大图、再次编辑。
4. 验证其他用户无法读取或加入不属于自己的 AI 图片。

## 1. 项目是做什么的

InteriorDesignWeb 是一个面向室内设计灵感与方案管理的 AI 工作台。用户可以：

- 注册、登录并按角色使用功能；
- 搜索和浏览室内设计图库；
- 创建“方案（Project）”及房间（ProjectRoom）；
- 将图库图片或 AI 生成图片加入方案；
- 使用文生图、图生图、图生视频工作流生成设计素材；
- 在任务独立路由中查看生成进度、恢复任务和查看结果；
- 将生成结果再次编辑或沉淀到当前方案。

当前产品主闭环是：

`选择工作流/输入素材 → 提交 AI 任务 → ComfyUI 执行 → 下载结果 → 上传腾讯云 COS → 写入 AI 结果表 → 结果卡片展示 → 加入方案 → 方案内预览`

## 2. 技术栈与主要入口

- 后端：ASP.NET Core 8、Entity Framework Core、MySQL、JWT、Swagger。
- 前端：React 19、TypeScript、Vite 7、Tailwind CSS 4、Zustand、React Router。
- AI：通过 `IAIProvider` 对接 ComfyUI Cloud 或自建 ComfyUI Server；工作流 JSON 位于 `InteriorDesignWeb/workflow/`。
- 文件：普通图库和 AI 结果分别使用 COS 配置；`CosService` 负责上传、缩略图和读取。
- 后端入口：`InteriorDesignWeb/Program.cs`。
- 前端入口：`InteriorDesignWeb/wwwroot/src/App.tsx`。
- AI 工作台：`InteriorDesignWeb/wwwroot/src/pages/app/GeneratePage.tsx`。
- 方案图片抽屉：`InteriorDesignWeb/wwwroot/src/components/ProjectDrawer.tsx`。

## 3. 当前数据模型的关键事实

系统存在两类图片，不能只靠一个无类型的 `imageId` 处理：

| 图片来源 | 数据表 | 主键 | 原图字段 | 缩略图字段 | 存储桶/读取方式 |
|---|---|---|---|---|---|
| 普通图库 | `images` | `ImageID` | `FilePath`（对象路径） | `ThumbnailPath`（对象路径） | 普通 COS；可由 `/api/Images/{id}/file` 代理读取 |
| AI 结果 | `aigenerationjobimages` | `AiImageID` | `ImageUrl`（完整 URL）+ `CosPath` | `ThumbnailPath`（对象路径） | AI COS；不能调用普通图片 ID 接口 |

`projectimages` 是方案与图片的关联表，`ImageID` 与 `AiImageID` 二选一。后续所有 DTO 和 UI 模型都应显式保留 `sourceType`、`imageID`、`aiImageID`，不能再用 `imageID ?? aiImageID` 抹掉来源。

## 4. AI 生成链路现状

1. 前端从 `/api/ai/generations/options` 获取工作流能力。
2. 输入图片先上传到 ComfyUI，再提交 `/api/ai/generations`。
3. 后端创建 `aigenerationjobs` 并提交 Provider；数据库驱动的 `AIJobBackgroundWorker` 扫描非终态任务，服务重启后能够继续恢复。
4. 前端进入 `/app/generate/:mode/jobs/:jobId`，每 2.5 秒查询任务。
5. Provider 完成后，后端下载输出、上传 AI COS、生成缩略图并写入 `aigenerationjobimages`。
6. 前端通过 `/api/ai/jobs/{jobId}/results` 获取结果。
7. 用户可将 `AiImageID` POST 到 `/api/projects/{projectId}/images`。

## 5. 已确认的图片显示故障与根因

### P0：生成后卡片不显示，但大图可以显示

- `GeneratePage.getAssetPreview()` 优先选择 `thumbnailPath`。
- `AIResultService` 保存的 `ThumbnailPath` 是 AI COS 对象路径，不是浏览器可直接访问的完整 URL。
- 结果大图优先选择 `imageUrl`，而该字段是完整 URL，所以大图可能正常。

修复原则：后端 DTO 应直接返回稳定的 `thumbnailUrl` 和 `originalUrl`（或统一的受控媒体端点），前端不应猜测对象路径如何拼接。

### P0：加入方案后，卡片和大图都无法显示

- `projects.ts` 将 `ImageID` 与 `AiImageID` 合并成一个 `imageId`。
- `ProjectDrawer.ImageCard` 无条件调用 `imagesApi.getThumbnailUrl(image.imageId)`。
- 大图无条件调用 `imagesApi.fetchOriginalAsBlob(image.imageId)`。
- 这两个函数都访问 `/api/Images/{id}/file`，而该接口只查询普通 `images` 表。
- AI 图片的 ID 属于 `aigenerationjobimages`，因此进入方案后缩略图和原图都会走错接口。

修复原则：方案图片接口返回可直接消费的统一媒体 DTO；至少根据 `isAi/sourceType` 分流到普通图片端点与 AI 图片端点。推荐统一为后端媒体代理接口，避免前端接触 COS 对象路径和签名细节。

### P0：AI 图片加入方案时房间被丢弃

前端发送了 `RoomID`，但 `ProjectImagesController` 的 AI 分支固定写入 `RoomID = null`，因此 AI 图片永远进入“未分类”。

## 6. 其他恶性问题

### 安全与数据一致性

- 添加 AI 图片时只验证 `AiImageID` 存在，没有验证该图片属于当前用户；猜到 ID 可能把其他用户的 AI 结果加入自己的方案。
- 普通图片文件读取端点未强制授权。若 COS 内容涉及用户私有素材，需要重新定义公开图库与私有上传图的权限边界。
- 方案图片允许重复关联，没有 `(ProjectID, ImageID)` / `(ProjectID, AiImageID)` 唯一约束。
- `IsAddedToProject` 是单个布尔值，却允许一张图关联多个方案；删除任一关联就置为 false，会与实际关联数不一致。应由关联表实时判断或改为计数/查询。
- 删除方案图片时在判空前访问 `relation.AiImageID`，不存在的 relation 会触发空引用并返回 500，而不是 404。
- AI 结果保存的“已有任意结果就直接返回”会让批量输出在只保存部分结果后无法补齐。

### 稳定性与可恢复性

- AI 任务已经由数据库驱动的 `BackgroundService` 恢复扫描，不再依赖请求内 `Task.Run`；下一阶段仍需增加数据库租约和心跳，避免多实例同时处理同一任务。
- AI 生成额度已正式接入任务提交链路：按工作流 `CostUnits` 原子预扣，幂等任务只扣一次；提交到 AI 服务前失败返还，服务已接受后记为已提交。额度不足返回 `QUOTA_EXCEEDED`，不再向 AI 服务发起任务。
- AI 助手使用固定 5 小时 Token 窗口。调用前按上下文和最大输出预留，成功后按模型接口返回的实际 Token 结算，调用失败释放；管理员可编辑上限、当前用量并重置窗口。
- AI 治理后台已加入管理员页面：核心安全规则编译在后端且不可编辑；业务规则支持草稿、发布和旧版本回滚；角色及用户覆盖可控制助手、提出建议、执行生图、自动入方案、工作流白名单和最大并发，所有写操作进入管理员审计。
- 助手设计摘要不再作为 `system` 消息，而作为明确标记的不可信用户数据；模型输出仍需结构校验，执行动作继续由后端重新鉴权。
- Release 发布固定关闭应用 PDB 并校验包内不存在 `InteriorDesignWeb.pdb`，避免生产日志暴露开发机绝对源码路径；生产异常响应始终返回通用错误与 RequestId。
- MiniMax OpenAI 兼容接口不再使用通用 `json_object`：`ResponseFormatMode=auto` 会识别 MiniMax，清理 thinking 标签并在 JSON 无效时执行一次隔离的结构修复；仍失败则仅展示自然语言，强制 `generationDraft=null`，不会产生可执行动作。
- COS URL、对象路径、签名 URL 混用，且普通桶与 AI 桶的语义不同，是当前显示故障的根本结构原因。

### 前端体验与维护性

- 图片加载失败只显示色块或静默失败，缺少明确错误、重试和诊断信息。
- 加入方案成功后只更新当前按钮状态，方案抽屉没有统一缓存失效/即时刷新机制。
- `GeneratePage.tsx` 体积过大，表单、上传、任务状态机、结果卡片、项目写入混在一个组件中。
- API 字段兼容逻辑分散，部分模块声明“兼容大小写”但实际上只读取单一字段形式。
- `imagesApi` 的 Blob URL 缓存没有释放策略，长时间浏览可能累积内存。
- 前端打包为单一 HTML，当前约 530 KB（gzip 约 148 KB）；规模增长后应做路由级拆包和按需加载。

## 7. 建议的修复顺序

### 第一阶段：恢复图片主闭环（必须先做）

1. 定义统一 `MediaAssetDto` / `ProjectImageDto`：保留来源、两个原始 ID、`thumbnailUrl`、`originalUrl`、`mediaType`。
2. 后端新增或统一媒体读取端点，内部根据来源访问正确数据表和 COS 桶；鉴权由后端负责。
3. AI 结果接口返回真正可访问的缩略图 URL，不再返回给浏览器裸对象路径。
4. `ProjectDrawer` 根据后端 URL 展示，不再把 AI ID 传给普通 `/Images/{id}/file`。
5. AI 加入方案时校验图片所有权、房间归属，并真正保存 `RoomID`。
6. 修复删除空引用、重复关联和 `IsAddedToProject` 语义。
7. 为四段链路补集成测试：生成结果卡片、生成结果大图、加入方案后卡片、加入方案后大图。

### 第二阶段：任务可靠性和计费

1. 把 `Task.Run` 轮询迁移到 `BackgroundService` + 持久化队列，或采用 Hangfire/Quartz/消息队列。
2. 增加任务租约、幂等键、失败重试、断点恢复、超时和多实例互斥。
3. 额度基础闭环已完成；后续根据商业规则决定 AI 服务已接受后的执行失败、取消任务是否退还，并补充额度流水查询页。
4. 增加任务历史页、失败原因、重试入口和管理员诊断信息。

### 第三阶段：产品能力

1. 方案内支持排序、封面、收藏、备注、版本对比和按房间编排。
2. 建立“生成谱系”：保留原图、参考图、工作流、模型、参数、提示词和派生关系。
3. 支持批量加入方案、批量下载、分享/导出方案和客户审阅链接。
4. 增加模板化提示词、风格预设、历史参数复用和优秀结果复刻。
5. 建立生成质量、耗时、失败率、成本和用户转化的可观测面板。

## 8. 后续开发必须遵守的约束

- 任何图片 UI 模型必须显式携带来源，禁止再用一个裸 ID 同时表示普通图和 AI 图。
- 浏览器只消费完整 URL 或受控 API URL；COS 对象路径只在后端内部流转。
- 项目、房间、AI 结果的写操作必须同时验证当前用户所有权。
- 跨数据库与外部 Provider/COS 的操作必须设计幂等和补偿机制。
- 状态值、媒体类型和错误码应集中定义，不在各页面重复用字符串集合猜测。
- 新增链路至少覆盖成功、失败、刷新恢复、重复请求和无权限五类场景。

## 9. 当前工程状态（2026-07-10）

- 工作区正处于 AI Provider/工作流重构，存在大量未提交修改和新增文件；后续修改前必须先查看 `git status`，不要覆盖现有成果。
- 后端已使用独立输出目录验证构建成功。
- 前端 `npm run build` 验证成功。
- 默认后端构建输出曾被正在运行的 `InteriorDesignWeb` 进程锁定；验证时应使用独立输出目录，或在明确获得许可后停止运行实例。
- 当前没有自动化测试项目/有效测试覆盖，图片主闭环修复后应优先补测试。
