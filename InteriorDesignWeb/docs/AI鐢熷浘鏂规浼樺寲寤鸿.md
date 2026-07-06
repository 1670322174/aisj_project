# AI 生图方案优化建议

## 当前方案

当前项目已经具备一个可运行的 ComfyUI 生图链路：

```text
/api/flux/generate → ComfyUIService → FluxWorkflowService → ComfyUI → COS → 数据库
```

当前问题是：

| 问题 | 影响 |
|---|---|
| `ComfyUIService` 职责过重 | 后续难维护 |
| `FluxWorkflowService` 硬编码节点 ID | 接 7 个工作流会混乱 |
| `JobTrackingService` 与 `AIJobService` 职责重叠 | 任务状态来源不统一 |
| `/api/flux` 与 `/api/ai-jobs` 双入口并存 | 前端调用容易混乱 |
| 缓存和数据库同时存状态 | 状态一致性风险 |

---

## 推荐目标方案

推荐采用“任务中心 + Provider + 工作流配置”的方案：

```text
AIJobsController
  ↓
AIJobService / AIJobOrchestrator
  ↓
WorkflowRegistry
  ↓
WorkflowBuilder
  ↓
ComfyUIProvider
  ↓
AIResultService
```

---

## 工作流配置化示例

后续每个工作流不要单独写一个 Service，而是使用配置：

```json
{
  "workflowCode": "material_replace",
  "name": "材质替换",
  "provider": "ComfyUI",
  "workflowFile": "workflows/material_replace.json",
  "modelCode": "qwen_image_edit",
  "inputMappings": [
    {
      "field": "prompt",
      "nodeId": "6",
      "inputKey": "text"
    },
    {
      "field": "sourceImage",
      "nodeId": "12",
      "inputKey": "image"
    }
  ]
}
```

优势：

| 优势 | 说明 |
|---|---|
| 扩展快 | 新工作流主要加配置 |
| 减少重复代码 | 不需要 7 套 WorkflowService |
| 方便维护 | 节点 ID 变化只改配置 |
| 便于前端动态展示 | 前端可根据配置生成表单 |
| 便于计费 | 每个 workflowCode 可单独配置成本 |

---

## 分阶段落地顺序

### 阶段 1：保持旧链路可用

保留 `/api/flux/*`，不影响当前生图能力。

### 阶段 2：任务中心接管状态

让真实 ComfyUI 任务创建后统一写入 `AIJobService`。

### 阶段 3：拆分 ComfyUIService

拆成：

| 新模块 | 职责 |
|---|---|
| `ComfyUIProvider` | 提交、查询、中断、下载 |
| `AIResultService` | 处理生成结果和图片入库 |
| `WorkflowBuilder` | 根据配置生成 workflow JSON |
| `AIJobOrchestrator` | 编排任务生命周期 |

### 阶段 4：接入 7 个工作流

在你提供完整工作流资料后，再建立 `WorkflowRegistry` 和具体配置。

