# 后端 AI 接口清单

## 生成接口

- `GET /api/ai/generations/options`
- `POST /api/ai/generations/upload`
- `POST /api/ai/generations`

## 任务接口

- `GET /api/ai/jobs`
- `GET /api/ai/jobs/{jobId}`
- `GET /api/ai/jobs/{jobId}/results`
- `POST /api/ai/jobs/{jobId}/cancel`

## 已移除

- `/api/flux/*`
- `/api/ai-jobs/*`
- `POST /api/ai/jobs` 占位任务接口
