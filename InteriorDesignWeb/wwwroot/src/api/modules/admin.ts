import { requestWithAuth } from '../client'

type RawObject = Record<string, unknown>

export type AdminRole = 'FreeUser' | 'Member' | 'PremiumMember' | 'Administrator'

export interface AdminTrendPoint {
  date: string
  users: number
  jobs: number
  succeeded: number
  failed: number
}

export interface AdminStatusCount {
  status: string
  count: number
}

export interface AdminOverview {
  users: {
    total: number
    enabled: number
    disabled: number
    newToday: number
    active24h: number
  }
  projects: { total: number; newToday: number }
  gallery: { total: number; newToday: number }
  aiJobs: {
    total: number
    today: number
    running: number
    succeeded: number
    failed: number
    successRate: number
    statusDistribution: AdminStatusCount[]
  }
  storage: { objects: number; bytes: number; displaySize: string }
  trends: AdminTrendPoint[]
  recentFailedJobs: AdminJobSummary[]
}

export interface AdminUserSummary {
  userId: string
  username: string
  phone: string
  role: AdminRole | string
  isEnabled: boolean
  createdAt: string
  lastLoginAt: string
  projectCount: number
  jobCount: number
  sessionCount: number
}

export interface AdminSession {
  sessionId: string
  createdAt: string
  lastUsedAt: string
  expiresAt: string
  ipAddress: string
  userAgent: string
  isCurrent: boolean
  isRevoked: boolean
}

export interface AdminActivity {
  id: string
  action: string
  description: string
  createdAt: string
  ipAddress: string
  result: string
}

export interface AdminProjectSummary {
  projectId: string
  name: string
  createdAt: string
  imageCount: number
}

export interface AdminJobSummary {
  jobId: string
  userId: string
  username: string
  workflowCode: string
  prompt: string
  status: string
  createdAt: string
  completedAt: string
  errorMessage: string
}

export interface AdminUserDetail {
  summary: AdminUserSummary
  quota: {
    total: number
    used: number
    available: number
    assistantTokenLimit5Hours: number
    assistantTokensUsed5Hours: number
    assistantTokensRemaining5Hours: number
    assistantTokenWindowStartedAt: string
    assistantTokenWindowEndsAt: string
  }
  counts: { projects: number; jobs: number; images: number; sessions: number }
  recentProjects: AdminProjectSummary[]
  recentJobs: AdminJobSummary[]
  recentActivities: AdminActivity[]
  sessions: AdminSession[]
}

export interface AdminPagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AdminSystemInfo {
  application: {
    name: string
    version: string
    environment: string
    serverTime: string
    uptime: string
  }
  database: ServiceHealth
  cos: ServiceHealth & { bucket: string; region: string; baseUrl: string }
  aiProvider: ServiceHealth & { provider: string }
  runtime: {
    framework: string
    os: string
    processMemoryBytes: number
    processorCount: number
  }
}

export interface ServiceHealth {
  status: string
  message: string
  latencyMs: number
  checkedAt: string
}

export interface AdminApiEndpoint {
  method: string
  path: string
  controller: string
  summary: string
  authorization: string
}

export interface AdminAuditLog {
  id: string
  operatorUserId: string
  operatorName: string
  action: string
  targetType: string
  targetId: string
  description: string
  result: string
  ipAddress: string
  createdAt: string
}

export interface AdminGalleryImage {
  imageId: string
  fileName: string
  roomType: string
  tags: string
  fileSize: number
  thumbnailUrl: string
  originalUrl: string
  createdAt: string
  updatedAt: string
  deletedAt: string
  isDeleted: boolean
  referenceCount: number
}

export interface AdminGalleryQuery {
  search?: string
  page?: number
  pageSize?: number
  status?: 'active' | 'deleted' | 'all'
  room?: string
  referenced?: boolean
}

export interface UpdateGalleryImageInput {
  fileName: string
  roomType: string
  tags?: string
}

export interface AdminUserQuery {
  search?: string
  page?: number
  pageSize?: number
  role?: string
  isEnabled?: boolean
}

export interface CreateAdminUserInput {
  username: string
  password: string
  phoneNumber?: string
  role: AdminRole
}

export interface UpdateAdminUserQuotaInput {
  totalUnits: number
  remainingUnits: number
  assistantTokenLimit5Hours: number
  assistantTokensUsed5Hours: number
  resetAssistantWindow: boolean
}

export interface GalleryUploadInput {
  file: File
  roomType: string
  houseType?: string
  style?: string
  material?: string
  elements?: string
  other?: string
}

function record(value: unknown): RawObject {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? value as RawObject
    : {}
}

function pick<T>(source: RawObject, keys: string[], fallback: T): T {
  for (const key of keys) {
    const value = source[key]
    if (value !== undefined && value !== null) return value as T
  }
  return fallback
}

function textValue(source: RawObject, keys: string[], fallback = ''): string {
  return String(pick(source, keys, fallback))
}

function numberValue(source: RawObject, keys: string[], fallback = 0): number {
  const value = Number(pick(source, keys, fallback))
  return Number.isFinite(value) ? value : fallback
}

function booleanValue(source: RawObject, keys: string[], fallback = false): boolean {
  const value = pick<unknown>(source, keys, fallback)
  if (typeof value === 'string') return value.toLowerCase() === 'true'
  return Boolean(value)
}

function arrayValue(source: RawObject, keys: string[]): unknown[] {
  const value = pick<unknown>(source, keys, [])
  return Array.isArray(value) ? value : []
}

function unwrap(response: unknown): RawObject {
  const root = record(response)
  return record(pick(root, ['data', 'Data'], root))
}

function normalizeStatusCount(value: unknown): AdminStatusCount {
  const raw = record(value)
  return {
    status: textValue(raw, ['status', 'Status', 'name', 'Name'], 'unknown'),
    count: numberValue(raw, ['count', 'Count', 'value', 'Value']),
  }
}

function normalizeJob(value: unknown): AdminJobSummary {
  const raw = record(value)
  return {
    jobId: textValue(raw, ['jobId', 'JobId', 'jobID', 'JobID']),
    userId: textValue(raw, ['userId', 'UserId', 'userID', 'UserID']),
    username: textValue(raw, ['username', 'userName', 'UserName']),
    workflowCode: textValue(raw, ['workflowCode', 'WorkflowCode']),
    prompt: textValue(raw, ['prompt', 'Prompt']),
    status: textValue(raw, ['status', 'Status']),
    createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
    completedAt: textValue(raw, ['completedAt', 'CompletedAt']),
    errorMessage: textValue(raw, ['errorMessage', 'ErrorMessage', 'error', 'Error']),
  }
}

function normalizeUser(value: unknown): AdminUserSummary {
  const raw = record(value)
  return {
    userId: textValue(raw, ['userId', 'UserId', 'userID', 'UserID']),
    username: textValue(raw, ['username', 'userName', 'UserName']),
    phone: textValue(raw, ['phone', 'phoneNumber', 'PhoneNumber']),
    role: textValue(raw, ['role', 'Role'], 'FreeUser'),
    isEnabled: booleanValue(raw, ['isEnabled', 'IsEnabled', 'enabled', 'Enabled'], true),
    createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
    lastLoginAt: textValue(raw, ['lastLoginAt', 'LastLoginAt']),
    projectCount: numberValue(raw, ['projectCount', 'ProjectCount', 'projects', 'Projects']),
    jobCount: numberValue(raw, ['jobCount', 'JobCount', 'jobs', 'Jobs']),
    sessionCount: numberValue(raw, ['sessionCount', 'SessionCount', 'sessions', 'Sessions']),
  }
}

function normalizeSession(value: unknown): AdminSession {
  const raw = record(value)
  return {
    sessionId: textValue(raw, ['sessionId', 'SessionId', 'sessionID', 'SessionID', 'id', 'Id']),
    createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
    lastUsedAt: textValue(raw, ['lastUsedAt', 'LastUsedAt']),
    expiresAt: textValue(raw, ['expiresAt', 'ExpiresAt']),
    ipAddress: textValue(raw, ['ipAddress', 'IpAddress']),
    userAgent: textValue(raw, ['userAgent', 'UserAgent']),
    isCurrent: booleanValue(raw, ['isCurrent', 'IsCurrent']),
    isRevoked: booleanValue(raw, ['isRevoked', 'IsRevoked']),
  }
}

function normalizeActivity(value: unknown): AdminActivity {
  const raw = record(value)
  return {
    id: textValue(raw, ['id', 'Id', 'activityId', 'ActivityId']),
    action: textValue(raw, ['action', 'Action']),
    description: textValue(raw, ['description', 'Description', 'detail', 'Detail']),
    createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
    ipAddress: textValue(raw, ['ipAddress', 'IpAddress']),
    result: textValue(raw, ['result', 'Result'], 'Success'),
  }
}

function normalizePaged<T>(data: RawObject, normalize: (item: unknown) => T): AdminPagedResult<T> {
  const items = arrayValue(data, ['items', 'Items', 'records', 'Records', 'data', 'Data']).map(normalize)
  const totalCount = numberValue(data, ['totalCount', 'TotalCount', 'total', 'Total'], items.length)
  const pageSize = numberValue(data, ['pageSize', 'PageSize'], Math.max(items.length, 1))
  return {
    items,
    page: numberValue(data, ['page', 'Page', 'pageNumber', 'PageNumber'], 1),
    pageSize,
    totalCount,
    totalPages: numberValue(data, ['totalPages', 'TotalPages'], Math.max(1, Math.ceil(totalCount / pageSize))),
  }
}

function queryString(values: Record<string, string | number | boolean | undefined>): string {
  const params = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== '') params.set(key, String(value))
  })
  const value = params.toString()
  return value ? `?${value}` : ''
}

async function getOverview(days = 14): Promise<AdminOverview> {
  const data = unwrap(await requestWithAuth(`/Admin/overview?days=${days}`))
  const users = record(data.users)
  const projects = record(data.projects)
  const gallery = record(data.gallery)
  const aiJobs = record(data.aiJobs)
  const storage = record(data.storage)

  return {
    users: {
      total: numberValue(users, ['total', 'Total']),
      enabled: numberValue(users, ['enabled', 'Enabled']),
      disabled: numberValue(users, ['disabled', 'Disabled']),
      newToday: numberValue(users, ['newToday', 'NewToday']),
      active24h: numberValue(users, ['active24h', 'Active24h', 'activeUsers24h', 'ActiveUsers24h']),
    },
    projects: {
      total: numberValue(projects, ['total', 'Total']),
      newToday: numberValue(projects, ['newToday', 'NewToday']),
    },
    gallery: {
      total: numberValue(gallery, ['total', 'Total']),
      newToday: numberValue(gallery, ['newToday', 'NewToday']),
    },
    aiJobs: {
      total: numberValue(aiJobs, ['total', 'Total']),
      today: numberValue(aiJobs, ['today', 'Today']),
      running: numberValue(aiJobs, ['running', 'Running']),
      succeeded: numberValue(aiJobs, ['succeeded', 'Succeeded', 'success', 'Success']),
      failed: numberValue(aiJobs, ['failed', 'Failed']),
      successRate: numberValue(aiJobs, ['successRate', 'SuccessRate']),
      statusDistribution: arrayValue(aiJobs, ['statusDistribution', 'StatusDistribution', 'statuses', 'Statuses']).map(normalizeStatusCount),
    },
    storage: {
      objects: numberValue(storage, ['objects', 'Objects', 'objectCount', 'ObjectCount']),
      bytes: numberValue(storage, ['bytes', 'Bytes', 'storageBytes', 'StorageBytes']),
      displaySize: textValue(storage, ['displaySize', 'DisplaySize']),
    },
    trends: arrayValue(data, ['trends', 'Trends']).map((value) => {
      const raw = record(value)
      return {
        date: textValue(raw, ['date', 'Date', 'day', 'Day']),
        users: numberValue(raw, ['users', 'Users', 'newUsers', 'NewUsers']),
        jobs: numberValue(raw, ['jobs', 'Jobs', 'total', 'Total']),
        succeeded: numberValue(raw, ['succeeded', 'Succeeded']),
        failed: numberValue(raw, ['failed', 'Failed']),
      }
    }),
    recentFailedJobs: arrayValue(data, ['recentFailedJobs', 'RecentFailedJobs']).map(normalizeJob),
  }
}

async function getUsers(query: AdminUserQuery): Promise<AdminPagedResult<AdminUserSummary>> {
  const data = unwrap(await requestWithAuth(`/Admin/users${queryString({ ...query })}`))
  return normalizePaged(data, normalizeUser)
}

async function createUser(input: CreateAdminUserInput): Promise<AdminUserSummary> {
  const data = unwrap(await requestWithAuth('/Admin/users', {
    method: 'POST',
    body: JSON.stringify(input),
  }))
  return normalizeUser(data)
}

async function setUserRole(userId: string, role: AdminRole): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/role`, {
    method: 'PUT',
    body: JSON.stringify({ role }),
  })
}

async function setUserStatus(userId: string, isEnabled: boolean): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/status`, {
    method: 'PUT',
    body: JSON.stringify({ isEnabled }),
  })
}

async function resetUserPassword(userId: string, newPassword: string): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/password`, {
    method: 'PUT',
    body: JSON.stringify({ newPassword }),
  })
}

async function getUserDetail(userId: string): Promise<AdminUserDetail> {
  const data = unwrap(await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}`))
  const summary = record(pick(data, ['summary', 'Summary', 'user', 'User'], data))
  const quota = record(data.quota)
  const counts = record(data.counts)
  return {
    summary: normalizeUser(summary),
    quota: {
      total: numberValue(quota, ['total', 'Total']),
      used: numberValue(quota, ['used', 'Used']),
      available: numberValue(quota, ['available', 'Available', 'remaining', 'Remaining']),
      assistantTokenLimit5Hours: numberValue(quota, ['assistantTokenLimit5Hours', 'AssistantTokenLimit5Hours']),
      assistantTokensUsed5Hours: numberValue(quota, ['assistantTokensUsed5Hours', 'AssistantTokensUsed5Hours']),
      assistantTokensRemaining5Hours: numberValue(quota, ['assistantTokensRemaining5Hours', 'AssistantTokensRemaining5Hours']),
      assistantTokenWindowStartedAt: textValue(quota, ['assistantTokenWindowStartedAt', 'AssistantTokenWindowStartedAt']),
      assistantTokenWindowEndsAt: textValue(quota, ['assistantTokenWindowEndsAt', 'AssistantTokenWindowEndsAt']),
    },
    counts: {
      projects: numberValue(counts, ['projects', 'Projects']),
      jobs: numberValue(counts, ['jobs', 'Jobs']),
      images: numberValue(counts, ['images', 'Images']),
      sessions: numberValue(counts, ['sessions', 'Sessions']),
    },
    recentProjects: arrayValue(data, ['recentProjects', 'RecentProjects']).map((value) => {
      const raw = record(value)
      return {
        projectId: textValue(raw, ['projectId', 'ProjectId', 'projectID', 'ProjectID']),
        name: textValue(raw, ['name', 'Name']),
        createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
        imageCount: numberValue(raw, ['imageCount', 'ImageCount']),
      }
    }),
    recentJobs: arrayValue(data, ['recentJobs', 'RecentJobs']).map(normalizeJob),
    recentActivities: arrayValue(data, ['recentActivities', 'RecentActivities']).map(normalizeActivity),
    sessions: arrayValue(data, ['sessions', 'Sessions']).map(normalizeSession),
  }
}

async function updateUserQuota(userId: string, input: UpdateAdminUserQuotaInput): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/quota`, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

async function revokeSession(userId: string, sessionId: string): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/sessions/${encodeURIComponent(sessionId)}`, {
    method: 'DELETE',
  })
}

async function revokeAllSessions(userId: string): Promise<void> {
  await requestWithAuth(`/Admin/users/${encodeURIComponent(userId)}/sessions`, { method: 'DELETE' })
}

async function getSystem(): Promise<AdminSystemInfo> {
  const data = unwrap(await requestWithAuth('/Admin/system'))
  const application = record(data.application)
  const database = record(data.database)
  const cos = record(data.cos)
  const aiProvider = record(pick(data, ['aiProvider', 'AIProvider', 'comfy', 'Comfy'], {}))
  const runtime = record(data.runtime)
  const health = (raw: RawObject): ServiceHealth => ({
    status: textValue(raw, ['status', 'Status'], 'Unknown'),
    message: textValue(raw, ['message', 'Message']),
    latencyMs: numberValue(raw, ['latencyMs', 'LatencyMs']),
    checkedAt: textValue(raw, ['checkedAt', 'CheckedAt']),
  })
  return {
    application: {
      name: textValue(application, ['name', 'Name'], 'InteriorDesignWeb'),
      version: textValue(application, ['version', 'Version'], 'Unknown'),
      environment: textValue(application, ['environment', 'Environment'], 'Unknown'),
      serverTime: textValue(application, ['serverTime', 'ServerTime']),
      uptime: textValue(application, ['uptime', 'Uptime']),
    },
    database: health(database),
    cos: {
      ...health(cos),
      bucket: textValue(cos, ['bucket', 'Bucket']),
      region: textValue(cos, ['region', 'Region']),
      baseUrl: textValue(cos, ['baseUrl', 'BaseUrl']),
    },
    aiProvider: {
      ...health(aiProvider),
      provider: textValue(aiProvider, ['provider', 'Provider'], 'ComfyUI'),
    },
    runtime: {
      framework: textValue(runtime, ['framework', 'Framework']),
      os: textValue(runtime, ['os', 'OS', 'operatingSystem', 'OperatingSystem']),
      processMemoryBytes: numberValue(runtime, ['processMemoryBytes', 'ProcessMemoryBytes']),
      processorCount: numberValue(runtime, ['processorCount', 'ProcessorCount']),
    },
  }
}

async function getApis(): Promise<AdminApiEndpoint[]> {
  const response = await requestWithAuth('/Admin/apis')
  const root = record(response)
  const rawData = pick<unknown>(root, ['data', 'Data'], response)
  const data = record(rawData)
  const values = Array.isArray(rawData) ? rawData : arrayValue(data, ['items', 'Items', 'endpoints', 'Endpoints'])
  return values.map((value) => {
    const raw = record(value)
    return {
      method: textValue(raw, ['method', 'Method', 'httpMethod', 'HttpMethod'], 'GET'),
      path: textValue(raw, ['path', 'Path', 'route', 'Route']),
      controller: textValue(raw, ['controller', 'Controller']),
      summary: textValue(raw, ['summary', 'Summary', 'displayName', 'DisplayName']),
      authorization: textValue(raw, ['authorization', 'Authorization', 'access', 'Access']),
    }
  })
}

async function getAuditLogs(query: { page?: number; pageSize?: number; search?: string; action?: string }): Promise<AdminPagedResult<AdminAuditLog>> {
  const data = unwrap(await requestWithAuth(`/Admin/audit-logs${queryString(query)}`))
  return normalizePaged(data, (value) => {
    const raw = record(value)
    return {
      id: textValue(raw, ['id', 'Id', 'auditLogId', 'AuditLogId']),
      operatorUserId: textValue(raw, ['operatorUserId', 'OperatorUserId']),
      operatorName: textValue(raw, ['operatorName', 'OperatorName', 'username', 'UserName']),
      action: textValue(raw, ['action', 'Action']),
      targetType: textValue(raw, ['targetType', 'TargetType']),
      targetId: textValue(raw, ['targetId', 'TargetId']),
      description: textValue(raw, ['description', 'Description']),
      result: textValue(raw, ['result', 'Result'], 'Success'),
      ipAddress: textValue(raw, ['ipAddress', 'IpAddress']),
      createdAt: textValue(raw, ['createdAt', 'CreatedAt']),
    }
  })
}

async function uploadGalleryImage(input: GalleryUploadInput): Promise<AdminGalleryImage> {
  const body = new FormData()
  body.set('file', input.file)
  body.set('roomType', input.roomType)
  if (input.houseType) body.set('houseType', input.houseType)
  if (input.style) body.set('style', input.style)
  if (input.material) body.set('material', input.material)
  if (input.elements) body.set('elements', input.elements)
  if (input.other) body.set('other', input.other)

  const data = unwrap(await requestWithAuth('/Admin/gallery/images', { method: 'POST', body }))
  return {
    imageId: textValue(data, ['imageId', 'ImageId', 'imageID', 'ImageID']),
    fileName: textValue(data, ['fileName', 'FileName'], input.file.name),
    roomType: textValue(data, ['roomType', 'RoomType', 'room', 'Room'], input.roomType),
    tags: '',
    fileSize: numberValue(data, ['fileSize', 'FileSize'], input.file.size),
    thumbnailUrl: textValue(data, ['thumbnailUrl', 'ThumbnailUrl']),
    originalUrl: textValue(data, ['originalUrl', 'OriginalUrl']),
    createdAt: textValue(data, ['createdAt', 'CreatedAt'], new Date().toISOString()),
    updatedAt: '',
    deletedAt: '',
    isDeleted: false,
    referenceCount: 0,
  }
}

function normalizeGalleryImage(value: unknown): AdminGalleryImage {
  const raw = record(value)
  return {
    imageId: textValue(raw, ['imageId', 'ImageId', 'imageID', 'ImageID']),
    fileName: textValue(raw, ['fileName', 'FileName']),
    roomType: textValue(raw, ['roomType', 'RoomType', 'room', 'Room']),
    tags: textValue(raw, ['tags', 'Tags']),
    fileSize: numberValue(raw, ['fileSize', 'FileSize']),
    thumbnailUrl: textValue(raw, ['thumbnailUrl', 'ThumbnailUrl']),
    originalUrl: textValue(raw, ['originalUrl', 'OriginalUrl']),
    createdAt: textValue(raw, ['createdAt', 'CreatedAt', 'uploadTime', 'UploadTime']),
    updatedAt: textValue(raw, ['updatedAt', 'UpdatedAt']),
    deletedAt: textValue(raw, ['deletedAt', 'DeletedAt']),
    isDeleted: booleanValue(raw, ['isDeleted', 'IsDeleted']),
    referenceCount: numberValue(raw, ['referenceCount', 'ReferenceCount']),
  }
}

async function getGalleryImages(query: AdminGalleryQuery): Promise<AdminPagedResult<AdminGalleryImage>> {
  const data = unwrap(await requestWithAuth(`/Admin/gallery/images${queryString(query)}`))
  return normalizePaged(data, normalizeGalleryImage)
}

async function updateGalleryImage(imageId: string, input: UpdateGalleryImageInput): Promise<void> {
  await requestWithAuth(`/Admin/gallery/images/${encodeURIComponent(imageId)}`, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

async function deleteGalleryImage(imageId: string): Promise<{ mode: string; referenceCount: number }> {
  const data = unwrap(await requestWithAuth(`/Admin/gallery/images/${encodeURIComponent(imageId)}`, { method: 'DELETE' }))
  return {
    mode: textValue(data, ['mode', 'Mode']),
    referenceCount: numberValue(data, ['referenceCount', 'ReferenceCount']),
  }
}

async function restoreGalleryImage(imageId: string): Promise<void> {
  await requestWithAuth(`/Admin/gallery/images/${encodeURIComponent(imageId)}/restore`, { method: 'POST' })
}

export const adminApi = {
  getOverview,
  getUsers,
  createUser,
  setUserRole,
  setUserStatus,
  resetUserPassword,
  getUserDetail,
  updateUserQuota,
  revokeSession,
  revokeAllSessions,
  getSystem,
  getApis,
  getAuditLogs,
  uploadGalleryImage,
  getGalleryImages,
  updateGalleryImage,
  deleteGalleryImage,
  restoreGalleryImage,
}
