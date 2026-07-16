# AI 设计助手链路与诊断指南

## 当前定位

当前 AI 助手是“受控多 Agent 编排 + 结构化产物 + 人工确认生图”。模型只能使用后端本轮下发的白名单工具，不能直接访问数据库、COS 或 ComfyUI。

它具备：

- 保存对话、消息和当前设计摘要；
- 根据用户需求进行一次关键追问或直接提出方案；
- 输出结构化 `brief` 和 `generationDraft`；
- 将生图建议保存为待确认动作；
- 用户确认后复用现有 AIJob、ComfyUI、COS 和图片结果链路；
- 用户、角色、工作流、5 小时 Token 和生图额度控制；
- 最近消息上下文及早期用户需求的简单压缩召回；
- 结构化输出失败时进行一次修复，仍失败时可降级为只读自然语言。
- MiniMax 前台协调、DeepSeek 设计/提示词/结果评估、火山方舟视觉分析；
- 上传房间、毛坯、户型、风格或材料图片并持久化到 AI COS；
- 多房间推进状态、Agent 运行事件和专业产物回放；
- 对实际生成结果进行视觉观察与设计符合度评估。

它暂不具备：

- 任意工具调用、模型直接执行生图或无人确认的付费重试；
- 运行时动态安装 Skill（当前 Skill 为服务器端版本化配置）；
- 向量检索、语义长期记忆或项目知识库；
- 模型流式文字输出；
- 无需用户确认的自动生图。

## 完整调用链

```text
浏览器发送消息
  -> POST /api/assistant/conversations/{conversationId}/messages
  -> 验证登录、对话归属和 AI 权限
  -> 使用 ClientRequestId 做幂等检查
  -> 保存用户消息
  -> 读取最近消息并压缩较早用户要求
  -> 预占 5 小时助手 Token
  -> 读取管理员发布的业务规则
  -> orchestrator(MiniMax) 判断直接回复或 handoff
  -> 带附件时 vision(火山方舟) 必须先读取短时 COS URL
  -> designer(DeepSeek) 形成 design_plan
  -> prompt-engineer(DeepSeek) 形成 generation_proposal
  -> 结算实际 Token
  -> 保存助手回复和当前 brief
  -> 若 action=propose_generation，保存待确认生图动作
  -> 前端右侧工作台显示方案和生图按钮

用户确认生图
  -> POST /api/assistant/conversations/{conversationId}/actions/{actionId}/execute
  -> 验证方案、房间、权限和工作流
  -> AIGenerationService 创建 AIJob 并预占生图额度
  -> 提交 ComfyUI
  -> ComfyUI WebSocket -> 后端 -> SSE -> 浏览器显示进度
  -> 后端读取结果、上传 COS、保存结果记录
  -> 前端通过鉴权媒体接口显示图片

用户主动评估结果
  -> POST /api/assistant/conversations/{conversationId}/actions/{actionId}/evaluate
  -> vision 观察实际 COS 结果图
  -> result-evaluator 对照 design_plan 和 generation_proposal
  -> 保存 generation_visual_analysis / result_evaluation
  -> 前端展示评分、优点、问题分类和下一轮修改指令
```

## 一次请求如何关联日志

前端失败提示现在会显示：

- 错误码；
- 服务端追踪 ID；
- 助手请求 ID；
- 生图失败时的对话 ID 和动作 ID。

服务端同一次聊天请求会携带以下日志字段：

```text
RequestId
AssistantRequestId
ConversationId
UserId
```

模型供应商返回追踪头时还会记录：

```text
ProviderRequestId
```

不要在日志中记录或搜索 API Key、Cookie、Token 原文、完整用户提示词、系统提示词或完整模型原始正文。

## 关键日志和含义

| 日志 | 表示的阶段 | 常见问题 |
| --- | --- | --- |
| `助手消息处理开始` | 请求已通过路由并进入业务层 | 后续没有日志时检查数据库查询或进程退出 |
| `助手消息命中幂等结果` | 相同请求已处理过 | 前端重复提交，但不会重复扣 Token |
| `助手上下文构建完成` | 消息和摘要读取成功 | 数量过大可能造成上下文成本高 |
| `助手 Token 已预占` | 用户额度允许本次调用 | 之前失败通常是额度或数据库问题 |
| `助手模型请求开始` | 即将请求模型供应商 | 检查 Model、ProviderHost、ResponseFormatMode |
| `助手模型网络请求失败` | DNS、TLS、代理或网络连接失败 | 检查服务器出站网络和 BaseUrl |
| `助手模型请求超时` | 在配置时间内没有完成 | 检查模型延迟和 TimeoutSeconds |
| `助手模型请求失败` | 供应商返回非 2xx | 使用 StatusCode、ProviderError、ProviderRequestId 排查 |
| `助手模型 HTTP 响应不是有效 JSON` | 外层兼容接口响应就不是 JSON | BaseUrl 路径、反向代理或供应商兼容性错误 |
| `响应缺少 choices.message.content` | 外层响应结构不兼容 | 供应商不是 OpenAI chat completions 格式 |
| `未返回有效结构化 JSON` | content 存在，但助手业务 JSON 不合格 | 查看 ParseFailure 和是否进入 repair |
| `格式修复仍未返回有效 JSON` | 第二次模型修复也失败 | 模型遵循结构能力不足或 schema 不兼容 |
| `助手模型结果已接收` | 已得到可用结果 | OutputMode 可判断是否发生过修复或降级 |
| `助手回复已保存` | 回复和 brief 已持久化 | 后续没有动作时查看 Action 和权限 |
| `助手已创建待确认生图建议` | 右侧应出现生成卡片 | 若前端没有显示，检查对话详情响应和页面状态 |
| `助手生图建议开始提交` | 用户已经确认生图 | 之后问题进入 AIJob 链路 |
| `助手生图任务已创建` | AIJob 已创建并返回 JobId | 后续看 ComfyUI、SSE、COS 和媒体接口日志 |

`Stage` 字段可能为：

- `reserve_quota`：助手 Token 额度预占；
- `load_business_policy`：读取管理员业务规则；
- `call_model`：模型请求、外层响应或结构化解析；
- `settle_quota`：根据供应商 usage 结算实际 Token。

## ParseFailure 对照

| ParseFailure | 含义 |
| --- | --- |
| `json_parse_error(...)` | 模型 content 不是可解析 JSON |
| `output_null` | JSON 被解析为 null |
| `assistant_text_missing` | 缺少可显示回复 |
| `action_invalid` | action 不在允许列表 |
| `brief_missing` | 缺少结构化方案摘要 |
| `generation_draft_or_prompt_missing` | 声称建议生图，但没有提示词草案 |

## 建议的服务端定位方式

先在前端复制“服务端追踪 ID”或“助手请求 ID”，再查询对应服务日志：

```bash
sudo journalctl -u <网站服务名> --since "10 minutes ago" --no-pager \
  | grep -E '<追踪ID>|助手模型|助手消息|助手生图'
```

若模型供应商返回了 `ProviderRequestId`，可以同时把它提供给供应商技术支持。不要提供网站密钥。

## 当前优先优化方向

1. 在服务器依次完成四个模型配置测试，再做一次图片输入到结果评估的真实闭环。
2. 为每个 Provider 记录请求 ID、模型、耗时和 Token；禁止记录签名 URL、图片内容、密钥或完整系统提示词。
3. 将当前“截取较早用户要求”升级为可维护的对话摘要，并在摘要中记录已确认、待确认和被否决的设计决策。
4. 增加模型流式文字输出；当前 Agent 阶段事件已可轮询回放，生图继续保持人工确认。
5. 将提示词、追问和方案审查拆成服务器端可版本化策略模块；在稳定前不引入开放式工具调用。
6. 为结果评估加入用户好/不好反馈和离线指标，供迭代分析 Agent 使用。

## 2026-07 诊断链路约定

一次 Agent 请求使用以下标识串联，排查时优先按此顺序搜索：

```text
RequestId (HTTP 请求)
  -> ClientRequestId (Assistant 幂等请求)
  -> AgentRunId (Agent 运行)
  -> ProviderRequestId (MiniMax / DeepSeek / 火山方舟)
  -> JobId (ComfyUI 生图任务，仅生图阶段)
```

前端错误卡会显示：失败阶段、定位原因、处理建议、RequestId、ProviderRequestId 和是否可重试。管理后台的“AI 治理 → Agent 运行诊断”可回放最近运行事件。

主要 `reason` 分类：

| reason | 意义 | 优先检查 |
| --- | --- | --- |
| `provider_auth_failed` | 模型鉴权失败 | API Key、模型权限、账户和地域 |
| `provider_rate_limited` | 供应商限流 | 供应商额度、并发和重试间隔 |
| `provider_timeout` | 模型请求超时 | 出站网络、供应商状态、`TimeoutSeconds` |
| `provider_network_error` | DNS/TLS/连接失败 | `BaseUrl`、DNS、证书、防火墙 |
| `provider_invalid_json` | HTTP 正文不是 JSON | API 路径、反向代理、协议选择 |
| `provider_*_missing` / `provider_empty_model_output` | 供应商响应结构不匹配 | 模型协议、模型版本、ProviderRequestId |
| `assistant_attachment_failed` | 附件上传链路失败 | `stage` 中的图片解析、COS 上传或数据库阶段 |

生产环境使用 JSON Console，默认将 EF SQL 和 `HttpClient` 框架级日志降为 `Warning`，保留项目自己的结构化 Agent 日志，避免模型请求被大量 SQL 和 HTTP 起止日志淹没。需要临时排查数据库时，可通过环境变量短时调高，排查后恢复：

```bash
Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information
```

禁止写入日志或 Agent 事件的内容：API Key、Authorization/Cookie、签名 COS URL、COS 对象路径、完整系统提示词、完整用户提示词、模型原始响应正文。
