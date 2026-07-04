// src/api/client.ts

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string

const DEFAULT_HEADERS: Record<string, string> = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
}

/* ─────────────────────────────────────────
   统一响应处理
───────────────────────────────────────── */
async function handleResponse(response: Response): Promise<unknown> {
  // 204 No Content 或无 content-type，直接返回 null
  const contentType = response.headers.get('content-type') ?? ''
  if (response.status === 204 || !contentType) return null

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
      headers: {
        ...DEFAULT_HEADERS,
        ...(options.headers as Record<string, string> | undefined),
      },
    })
    return await handleResponse(response)
  } catch (error) {
    if (error instanceof Error) throw error
    throw new Error('网络请求异常')
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
  const token = localStorage.getItem('access_token')

  try {
    const response = await fetch(`${BASE_URL}${endpoint}`, {
      ...options,
      headers: {
        ...DEFAULT_HEADERS,
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(options.headers as Record<string, string> | undefined),
      },
    })

    // 401：清除 token，派发事件，抛出错误
    if (response.status === 401) {
      localStorage.removeItem('access_token')
      window.dispatchEvent(new Event('auth:unauthorized'))
      throw new Error('登录已过期，请重新登录')
    }

    return await handleResponse(response)
  } catch (error) {
    if (error instanceof Error) throw error
    throw new Error('网络请求异常')
  }
}