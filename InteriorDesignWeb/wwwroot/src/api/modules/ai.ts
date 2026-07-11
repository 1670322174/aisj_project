// 作用：封装 AI 工作流、图片上传、任务轮询和结果查询接口。
// 前端只调用业务 API，不直接访问 ComfyUI Server。
import { refreshAuthSession, requestWithAuth } from '../client'
import { fetchAuthenticatedMedia } from '../media'

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string

type ApiEnvelope<T> = {
  success?: boolean
  Success?: boolean
  code?: string
  Code?: string
  message?: string
  Message?: string
  data?: T
  Data?: T
}

export type WorkflowOutputType = 'image' | 'video'

export interface WorkflowOption {
  workflowCode: string
  name: string
  description: string
  providerType: string
  outputType: WorkflowOutputType
  defaultModelCode: string
  costUnits: number
  enabled: boolean
  requiredInputs: string[]
  optionalInputs: string[]
}

export interface UploadResult {
  name: string
  subfolder: string
  type: string
  fieldName: string
}

export interface SubmitGenerationRequest {
  workflowCode: string
  modelCode?: string | null
  prompt?: string | null
  negativePrompt?: string | null
  projectId?: number | null
  roomId?: number | null
  sourceImageName?: string | null
  referenceImageName?: string | null
  firstFrameImageName?: string | null
  lastFrameImageName?: string | null
  inputImages?: Record<string, string> | null
  parameters?: Record<string, unknown> | null
}

export interface SubmitGenerationResponse {
  jobId: string
  providerJobId: string
  promptId: string
  workflowCode: string
  modelCode: string | null
  outputType: WorkflowOutputType
  status: string
  progressValue: number
}

export interface AIJob {
  jobId: string
  status: string
  userID?: number | null
  workflowCode: string
  modelCode?: string | null
  providerType: string
  prompt?: string | null
  negativePrompt?: string | null
  progressValue: number
  costUnits: number
  errorCode?: string | null
  errorMessage?: string | null
  createdAt: string
  startedAt?: string | null
  completedAt?: string | null
  updatedAt?: string | null
  imageCount: number
}

export interface AIJobResult {
  aiImageID: number
  jobId: string
  imageUrl?: string | null
  cosPath?: string | null
  thumbnailPath?: string | null
  thumbnailUrl?: string | null
  originalUrl?: string | null
  sourceType: string
  metadataJson?: string | null
  createdAt: string
}

export interface PagedJobs {
  items: AIJob[]
  page: number
  pageSize: number
  total: number
  hasNext: boolean
}

function pick<T>(obj: Record<string, unknown>, keys: string[], fallback: T): T {
  for (const key of keys) {
    const value = obj[key]
    if (value !== undefined && value !== null) return value as T
  }
  return fallback
}

function unwrap<T>(value: unknown): T {
  const envelope = value as ApiEnvelope<T>
  const data = envelope?.data ?? envelope?.Data
  return (data === undefined ? value : data) as T
}

function normalizeWorkflow(raw: Record<string, unknown>): WorkflowOption {
  const outputType = pick<string>(raw, ['outputType', 'OutputType'], 'image') === 'video' ? 'video' : 'image'
  return {
    workflowCode: pick(raw, ['workflowCode', 'WorkflowCode'], ''),
    name: pick(raw, ['name', 'Name'], ''),
    description: pick(raw, ['description', 'Description'], ''),
    providerType: pick(raw, ['providerType', 'ProviderType'], 'ComfyUI'),
    outputType,
    defaultModelCode: pick(raw, ['defaultModelCode', 'DefaultModelCode'], ''),
    costUnits: Number(pick(raw, ['costUnits', 'CostUnits'], 0)),
    enabled: Boolean(pick(raw, ['enabled', 'Enabled'], true)),
    requiredInputs: pick(raw, ['requiredInputs', 'RequiredInputs'], []),
    optionalInputs: pick(raw, ['optionalInputs', 'OptionalInputs'], []),
  }
}

function normalizeJob(raw: Record<string, unknown>): AIJob {
  return {
    jobId: pick(raw, ['jobId', 'JobId'], ''),
    status: pick(raw, ['status', 'Status'], ''),
    userID: pick(raw, ['userID', 'UserID'], null),
    workflowCode: pick(raw, ['workflowCode', 'WorkflowCode'], ''),
    modelCode: pick(raw, ['modelCode', 'ModelCode'], null),
    providerType: pick(raw, ['providerType', 'ProviderType'], ''),
    prompt: pick(raw, ['prompt', 'Prompt'], null),
    negativePrompt: pick(raw, ['negativePrompt', 'NegativePrompt'], null),
    progressValue: Number(pick(raw, ['progressValue', 'ProgressValue', 'progress', 'Progress'], 0)),
    costUnits: Number(pick(raw, ['costUnits', 'CostUnits'], 0)),
    errorCode: pick(raw, ['errorCode', 'ErrorCode'], null),
    errorMessage: pick(raw, ['errorMessage', 'ErrorMessage'], null),
    createdAt: pick(raw, ['createdAt', 'CreatedAt'], ''),
    startedAt: pick(raw, ['startedAt', 'StartedAt'], null),
    completedAt: pick(raw, ['completedAt', 'CompletedAt'], null),
    updatedAt: pick(raw, ['updatedAt', 'UpdatedAt'], null),
    imageCount: Number(pick(raw, ['imageCount', 'ImageCount'], 0)),
  }
}

function normalizeResult(raw: Record<string, unknown>): AIJobResult {
  return {
    aiImageID: Number(pick(raw, ['aiImageID', 'AiImageID'], 0)),
    jobId: pick(raw, ['jobId', 'JobId'], ''),
    imageUrl: pick(raw, ['imageUrl', 'ImageUrl'], null),
    cosPath: pick(raw, ['cosPath', 'CosPath'], null),
    thumbnailPath: pick(raw, ['thumbnailPath', 'ThumbnailPath'], null),
    thumbnailUrl: pick(raw, ['thumbnailUrl', 'ThumbnailUrl'], null),
    originalUrl: pick(raw, ['originalUrl', 'OriginalUrl'], null),
    sourceType: pick(raw, ['sourceType', 'SourceType'], 'ai'),
    metadataJson: pick(raw, ['metadataJson', 'MetadataJson'], null),
    createdAt: pick(raw, ['createdAt', 'CreatedAt'], ''),
  }
}

async function getWorkflowOptions(): Promise<WorkflowOption[]> {
  const response = await requestWithAuth('/ai/generations/options', { method: 'GET' })
  const raw = unwrap<unknown[]>(response) ?? []
  return raw.map((item) => normalizeWorkflow(item as Record<string, unknown>)).filter((item) => item.enabled)
}

/**
 * 使用 XMLHttpRequest 上传原始文件，以便展示真实上传进度。
 * 不做压缩和裁剪，上传内容与用户选中的文件一致。
 */
function uploadInputImage(
  file: File,
  fieldName: string,
  onProgress?: (progress: number) => void,
  hasRetried = false,
): Promise<UploadResult> {
  return new Promise((resolve, reject) => {
    const formData = new FormData()
    formData.append('file', file, file.name)
    formData.append('fieldName', fieldName)
    formData.append('overwrite', 'true')

    const xhr = new XMLHttpRequest()
    xhr.open('POST', `${BASE_URL}/ai/generations/upload`)
    xhr.withCredentials = true
    xhr.setRequestHeader('Accept', 'application/json')

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) return
      onProgress?.(Math.round((event.loaded / event.total) * 100))
    }

    xhr.onerror = () => reject(new Error('图片上传失败，请检查网络连接'))
    xhr.onabort = () => reject(new Error('图片上传已取消'))
    xhr.onload = async () => {
      let body: unknown
      try {
        body = xhr.responseText ? JSON.parse(xhr.responseText) : null
      } catch {
        body = null
      }

      if (xhr.status === 401) {
        if (!hasRetried && await refreshAuthSession()) {
          uploadInputImage(file, fieldName, onProgress, true).then(resolve, reject)
          return
        }
        window.dispatchEvent(new Event('auth:unauthorized'))
        reject(new Error('登录已过期，请重新登录'))
        return
      }

      if (xhr.status < 200 || xhr.status >= 300) {
        const record = body as Record<string, unknown> | null
        reject(new Error(String(record?.message ?? record?.Message ?? `上传失败：HTTP ${xhr.status}`)))
        return
      }

      const raw = unwrap<Record<string, unknown>>(body)
      resolve({
        name: pick(raw, ['name', 'Name'], ''),
        subfolder: pick(raw, ['subfolder', 'Subfolder'], ''),
        type: pick(raw, ['type', 'Type'], 'input'),
        fieldName: pick(raw, ['fieldName', 'FieldName'], fieldName),
      })
    }

    xhr.send(formData)
  })
}

async function submitGeneration(request: SubmitGenerationRequest): Promise<SubmitGenerationResponse> {
  const response = await requestWithAuth('/ai/generations', {
    method: 'POST',
    body: JSON.stringify(request),
  })
  const raw = unwrap<Record<string, unknown>>(response)
  return {
    jobId: pick(raw, ['jobId', 'JobId'], ''),
    providerJobId: pick(raw, ['providerJobId', 'ProviderJobId'], ''),
    promptId: pick(raw, ['promptId', 'PromptId'], ''),
    workflowCode: pick(raw, ['workflowCode', 'WorkflowCode'], ''),
    modelCode: pick(raw, ['modelCode', 'ModelCode'], null),
    outputType: pick<string>(raw, ['outputType', 'OutputType'], 'image') === 'video' ? 'video' : 'image',
    status: pick(raw, ['status', 'Status'], ''),
    progressValue: Number(pick(raw, ['progressValue', 'ProgressValue'], 0)),
  }
}

async function getJob(jobId: string): Promise<AIJob> {
  const response = await requestWithAuth(`/ai/jobs/${encodeURIComponent(jobId)}`, { method: 'GET' })
  return normalizeJob(unwrap<Record<string, unknown>>(response))
}

async function getJobResults(jobId: string): Promise<AIJobResult[]> {
  const response = await requestWithAuth(`/ai/jobs/${encodeURIComponent(jobId)}/results`, { method: 'GET' })
  const raw = unwrap<unknown[]>(response) ?? []
  return raw.map((item) => normalizeResult(item as Record<string, unknown>))
}

function fetchJobResultMedia(
  jobId: string,
  aiImageId: number,
  type: 'thumbnail' | 'original',
): Promise<string> {
  const endpoint = `/ai/jobs/${encodeURIComponent(jobId)}/results/${aiImageId}/file?type=${type}`
  return fetchAuthenticatedMedia(endpoint, `ai-result:${jobId}:${aiImageId}:${type}`)
}

async function getJobs(page = 1, pageSize = 20): Promise<PagedJobs> {
  const response = await requestWithAuth(`/ai/jobs?page=${page}&pageSize=${pageSize}`, { method: 'GET' })
  const raw = unwrap<Record<string, unknown>>(response)
  const itemsRaw = pick<unknown[]>(raw, ['items', 'Items'], [])
  return {
    items: itemsRaw.map((item) => normalizeJob(item as Record<string, unknown>)),
    page: Number(pick(raw, ['page', 'Page'], page)),
    pageSize: Number(pick(raw, ['pageSize', 'PageSize'], pageSize)),
    total: Number(pick(raw, ['total', 'Total'], itemsRaw.length)),
    hasNext: Boolean(pick(raw, ['hasNext', 'HasNext'], false)),
  }
}

async function cancelJob(jobId: string): Promise<void> {
  await requestWithAuth(`/ai/generations/${encodeURIComponent(jobId)}/cancel`, { method: 'POST' })
}

async function deleteJob(jobId: string): Promise<void> {
  await requestWithAuth(`/ai/jobs/${encodeURIComponent(jobId)}`, { method: 'DELETE' })
}

export const aiApi = {
  getWorkflowOptions,
  uploadInputImage,
  submitGeneration,
  getJob,
  getJobResults,
  fetchJobResultMedia,
  getJobs,
  cancelJob,
  deleteJob,
}
