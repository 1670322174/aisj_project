// 作用：统一读取需要 JWT 的图片/视频文件，并转换为浏览器可展示的 Blob URL。

import { refreshAuthSession } from './client'

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string
const MAX_CACHE_SIZE = 100
const blobCache = new Map<string, string>()

export class AuthenticatedMediaError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly endpoint: string,
  ) {
    super(message)
    this.name = 'AuthenticatedMediaError'
  }
}

function remember(key: string, url: string): void {
  const previous = blobCache.get(key)
  if (previous && previous !== url) URL.revokeObjectURL(previous)
  blobCache.delete(key)
  blobCache.set(key, url)

  while (blobCache.size > MAX_CACHE_SIZE) {
    const oldest = blobCache.entries().next().value as [string, string] | undefined
    if (!oldest) break
    URL.revokeObjectURL(oldest[1])
    blobCache.delete(oldest[0])
  }
}

export async function fetchAuthenticatedMedia(
  endpoint: string,
  cacheKey: string = endpoint,
): Promise<string> {
  const cached = blobCache.get(cacheKey)
  if (cached) {
    blobCache.delete(cacheKey)
    blobCache.set(cacheKey, cached)
    return cached
  }

  const requestUrl = `${BASE_URL}${endpoint}`
  const requestOptions: RequestInit = {
    credentials: 'include',
    cache: 'no-store',
    headers: { Accept: 'image/*,video/*,application/octet-stream' },
  }
  let response = await fetch(requestUrl, requestOptions)

  if (response.status === 401 && await refreshAuthSession()) {
    response = await fetch(requestUrl, requestOptions)
  }

  if (response.status === 401) {
    window.dispatchEvent(new Event('auth:unauthorized'))
    throw new AuthenticatedMediaError(
      `图片请求未通过登录验证（HTTP 401）：${endpoint}`,
      401,
      endpoint,
    )
  }

  if (!response.ok) {
    throw new AuthenticatedMediaError(
      `媒体加载失败（HTTP ${response.status}）：${endpoint}`,
      response.status,
      endpoint,
    )
  }

  const blobUrl = URL.createObjectURL(await response.blob())
  remember(cacheKey, blobUrl)
  return blobUrl
}
