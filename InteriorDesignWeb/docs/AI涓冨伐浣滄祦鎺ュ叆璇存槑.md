# AI 七工作流接入说明

## 1. 本次接入目标

本次调整不是把 7 个工作流写成 7 套 Controller，而是新增统一 AI 生图入口：

```text
前端
  ↓
/api/ai/generations
  ↓
AIGenerationService
  ↓
WorkflowRegistry + WorkflowBuilder
  ↓
ComfyUIProvider
  ↓
ComfyUI
  ↓
AIResultService
  ↓
COS + aigenerationjobimages
```

旧接口 `/api/flux/*` 暂时保留，作为兼容入口。

## 2. 已注册工作流

| workflowCode | 功能 | 输入 | 输出 | 积分 |
|---|---|---|---|---:|
| `api_banana_image` | 高级参考图编辑 | sourceImage、referenceImage、prompt | image | 21 |
| `api_bria_image_edit` | 文本图片编辑 | sourceImage、prompt | image | 9 |
| `api_grok_image_edit` | 简单文生图 | prompt | image | 4 |
| `api_luma_image_edit` | 高级文本图像编辑 | sourceImage、prompt | image | 21 |
| `api_seedance2` | 首尾帧漫游视频 | firstFrame、lastFrame | video | 322 |
| `api_seedream_image_edit` | 参考图风格迁移 | sourceImage、referenceImage | image | 7 |
| `api_veo3` | 图片生成漫游视频 | sourceImage | video | 343 |

## 3. 主要接口

### 获取工作流选项

```http
GET /api/ai/generations/options
Authorization: Bearer {token}
```

### 上传输入图片到 ComfyUI

```http
POST /api/ai/generations/upload
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: 图片文件
fieldName: sourceImage / referenceImage / firstFrame / lastFrame
```

返回 `name` 后，把它填入提交任务请求。

### 提交任务

```http
POST /api/ai/generations
Authorization: Bearer {token}
Content-Type: application/json
```

文生图示例：

```json
{
  "workflowCode": "api_grok_image_edit",
  "prompt": "现代暖色室内客厅，木质地板，高级感",
  "parameters": {
    "batchSize": 1,
    "resolution": "1K",
    "aspectRatio": "1:1"
  }
}
```

图像编辑示例：

```json
{
  "workflowCode": "api_bria_image_edit",
  "prompt": "把图中的地面换成瓷砖，把水印去掉，其他保持不变",
  "sourceImageName": "上传接口返回的 name",
  "parameters": {
    "steps": 50,
    "guidanceScale": 3
  }
}
```

双图参考编辑示例：

```json
{
  "workflowCode": "api_banana_image",
  "prompt": "参考图2的风格把图1生成精装房照片",
  "sourceImageName": "原图 name",
  "referenceImageName": "参考图 name",
  "parameters": {
    "resolution": "2K",
    "aspectRatio": "auto"
  }
}
```

首尾帧视频示例：

```json
{
  "workflowCode": "api_seedance2",
  "firstFrameImageName": "首帧 name",
  "lastFrameImageName": "尾帧 name",
  "parameters": {
    "duration": 7,
    "resolution": "720p",
    "aspectRatio": "16:9",
    "generateAudio": true
  }
}
```

### 查询任务

```http
GET /api/ai/jobs/{jobId}
GET /api/ai/jobs/{jobId}/results
```

旧路由 `/api/ai-jobs/*` 仍兼容。

## 4. 注意事项

1. 需要先执行 `database/20260706_ai_workflow_integration.sql`，否则新增字段可能不存在。
2. 视频输出当前也会写入 `aigenerationjobimages`，`SourceType` 为 `video`。
3. 旧 `/api/flux/*` 没有删除，避免破坏已有前端或历史测试流程。
4. 生产环境应将真实 `appsettings.json` 密钥放在本地或环境变量，不要提交到 GitHub。
