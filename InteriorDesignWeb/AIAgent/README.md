# InteriorDesignWeb Agent 配置

此目录保存可版本化的 Agent 定义、Skill、工具目录、业务流程、策略和回归场景。

## 安全边界

- API Key 不得写入此目录，只能通过服务器环境变量或秘密管理服务提供。
- Agent 定义只能引用 `AgentModels` 中存在的模型配置。
- Skill 是业务操作说明，不是权限；真正权限由后端工具网关校验。
- 模型不能直接调用 ComfyUI、数据库或 COS，必须通过后端白名单工具。
- 付费生图、覆盖项目和其他高风险写操作必须经过持久化确认。
- 迭代分析只能生成建议和草稿，不得自行发布 Agent、Skill 或策略。

## 修改流程

1. 修改 Agent、Skill 或工具配置。
2. 运行后端测试和配置校验。
3. 在管理员后台查看加载状态。
4. 以草稿方式发布；正在运行的任务继续使用启动时锁定的版本。

当前为平台底座阶段，定义会被加载和校验，但尚未全部参与用户请求编排。

## 当前模型映射

| 配置名 | 用途 | Provider / 协议 | 模型 |
| --- | --- | --- | --- |
| `minimax_frontdesk` | 前台协调与意图路由 | MiniMax / Anthropic Messages | `MiniMax-M3` |
| `deepseek_worker` | 设计、提示词和评估默认模型 | DeepSeek / Chat Completions | `deepseek-v4-flash` |
| `deepseek_design_pro` | 复杂设计按策略升级 | DeepSeek / Chat Completions | `deepseek-v4-pro` |
| `volc_vision` | 图片、户型与空间视觉分析 | 火山方舟 / Responses | `doubao-seed-2-1-turbo-260628` |

配置名使用下划线，确保能被 Linux 环境变量稳定覆盖。

## 服务器配置

密钥不要写入 `appsettings.json` 或本目录。systemd 环境文件可配置：

```ini
AgentModels__Profiles__minimax_frontdesk__ApiKey=替换为实际密钥
AgentModels__Profiles__deepseek_worker__ApiKey=替换为实际密钥
AgentModels__Profiles__deepseek_design_pro__ApiKey=替换为实际密钥
AgentModels__Profiles__volc_vision__ApiKey=替换为实际密钥
AgentPlatform__Enabled=true
AgentPlatform__FallbackToLegacy=true
AgentPlatform__MaxModelCallsPerRun=5
```

DeepSeek 两个配置可使用同一把密钥。启用前必须依次执行
`database/20260716_agent_runtime_phase2.sql` 和
`database/20260716_agent_vision_phase3.sql`，再在管理端逐个测试四个模型配置。
`FallbackToLegacy=true` 会在新模型服务暂时不可用或返回格式错误时回退旧助手；数据库、
权限和业务校验错误不会被静默回退。修改配置文件后，管理员可调用
`POST /api/admin/ai-governance/agent-config/reload` 重新加载；密钥或其他
`appsettings` 配置发生变化时仍建议重启服务。

管理端可分别测试每个模型：

```text
POST /api/admin/ai-governance/model-profiles/{profileId}/test
```

接口只返回 Provider、模型、耗时、Token 数和请求追踪 ID，不返回密钥。

## 第二阶段运行链路

```text
用户消息
  -> orchestrator (MiniMax-M3)
  -> request_agent_handoff
  -> designer (DeepSeek V4 Flash，可申请 Pro)
  -> emit_design_artifact
  -> prompt-engineer (DeepSeek V4 Flash)
  -> emit_generation_proposal
  -> 用户确认
  -> 原有 AssistantGenerationAction / AIJob / ComfyUI 链路
```

每次运行写入 `assistantagentruns`，阶段事件写入 `assistantagentevents`，设计、视觉和提示词
成果写入 `assistantagentartifacts`。前端通过 `ClientRequestId` 查询运行事件，因此刷新后仍可回放。

## 第三阶段图片与结果评估链路

```text
用户上传图片 -> assistantattachments / AI COS 私有桶
  -> 浏览器通过鉴权媒体接口预览
  -> orchestrator 强制转交 vision
  -> vision 使用 15 分钟短时 COS 签名读取图片
  -> visual_analysis -> designer -> prompt-engineer
  -> 用户确认并生成图片
  -> 用户点击结果评估
  -> vision 观察实际结果 -> result-evaluator 对照设计方案
  -> result_evaluation 卡片（不会自动再次生图）
```

附件只在数据库保存 COS 对象键，前端不接收持久签名 URL。支持 JPEG、PNG、WebP，单张上限
15 MB、每条消息最多 6 张。房间进度复用 `projectrooms.Status`，避免产生两套互相冲突的状态。
