// src/api/client.ts

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string

const DEFAULT_HEADERS: Record<string, string> = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
}

let refreshPromise: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  const response = await fetch(`${BASE_URL}/User/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  return response.ok
}

export function refreshAuthSession(): Promise<boolean> {
  if (refreshPromise) return refreshPromise

  refreshPromise = (async () => {
    try {
      if (await tryRefresh()) return true
      await new Promise((resolve) => window.setTimeout(resolve, 150))
      return await tryRefresh()
    } catch {
      return false
    } finally {
      refreshPromise = null
    }
  })()

  return refreshPromise
}

function normalizeRequestError(error: unknown): Error {
  if (error instanceof TypeError) {
    return new Error('无法连接到服务端，请稍后重试')
  }
  if (error instanceof Error) {
    const message = error.message.toLowerCase()
    if (message.includes('failed to fetch') || message.includes('networkerror') || message.includes('load failed')) {
      return new Error('无法连接到服务端，请稍后重试')
    }
    return error
  }
  return new Error('网络请求异常，请稍后重试')
}

function buildHeaders(endpoint: string, options: RequestInit): Record<string, string> {
  const headers: Record<string, string> = {
    ...DEFAULT_HEADERS,
    ...(options.headers as Record<string, string> | undefined),
  }

  const method = (options.method ?? 'GET').toUpperCase()
  const isAdminWrite = endpoint.toLowerCase().startsWith('/admin')
    && !['GET', 'HEAD', 'OPTIONS'].includes(method)
  if (isAdminWrite) headers['X-DesignHub-Admin'] = '1'

  // 浏览器必须自行生成 multipart/form-data 的 boundary。
  if (options.body instanceof FormData) delete headers['Content-Type']
  return headers
}

/* ─────────────────────────────────────────
   统一响应处理
───────────────────────────────────────── */
async function handleResponse(response: Response): Promise<unknown> {
  // 204 No Content 或无 content-type，直接返回 null
  const contentType = response.headers.get('content-type') ?? ''
  if (response.status === 204) return null
  if (!contentType) {
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    return null
  }

  // 解析响应体
  let data: unknown = null
  if (contentType.includes('application/json') || contentType.includes('text/json')) {
    data = await response.json()
  } else {
    const text = await response.text()
    data = { raw: text }
  }

  // 非 2xx 抛出错误
  if (!response.ok) {
    const message =
      (data as Record<string, unknown>)?.message as string | undefined
    throw new Error(message || `HTTP ${response.status}`)
  }

  return data
}

/* ─────────────────────────────────────────
   基础请求（不注入 Token）
   用于登录等不需要认证的接口
───────────────────────────────────────── */
export async function request(
  endpoint: string,
  options: RequestInit = {},
): Promise<unknown> {
  try {
    const response = await fetch(`${BASE_URL}${endpoint}`, {
      ...options,
      credentials: 'include',
      headers: buildHeaders(endpoint, options),
    })
    return await handleResponse(response)
  } catch (error) {
    throw normalizeRequestError(error)
  }
}

/* ─────────────────────────────────────────
   带 Token 的请求
   从 localStorage 读取 access_token
   401 时派发 auth:unauthorized 事件
───────────────────────────────────────── */
export async function requestWithAuth(
  endpoint: string,
  options: RequestInit = {},
): Promise<unknown> {
  try {
    let response = await fetch(`${BASE_URL}${endpoint}`, {
      ...options,
      credentials: 'include',
      headers: buildHeaders(endpoint, options),
    })

    // 401：清除 token，派发事件，抛出错误
    if (response.status === 401) {
      const refreshed = await refreshAuthSession()
      if (refreshed) {
        response = await fetch(`${BASE_URL}${endpoint}`, {
          ...options,
          credentials: 'include',
          headers: buildHeaders(endpoint, options),
        })
      }
    }

    if (response.status === 401) {
      window.dispatchEvent(new Event('auth:unauthorized'))
      throw new Error('登录已过期，请重新登录')
    }

    return await handleResponse(response)
  } catch (error) {
    throw normalizeRequestError(error)
  }
}
