// 作用：统一读取需要 JWT 的图片/视频文件，并转换为浏览器可展示的 Blob URL。

import { refreshAuthSession } from './client'

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string
const MAX_CACHE_SIZE = 100
const blobCache = new Map<string, string>()

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

  let response = await fetch(`${BASE_URL}${endpoint}`, {
    credentials: 'include',
  })

  if (response.status === 401 && await refreshAuthSession()) {
    response = await fetch(`${BASE_URL}${endpoint}`, { credentials: 'include' })
  }

  if (response.status === 401) {
    window.dispatchEvent(new Event('auth:unauthorized'))
    throw new Error('登录已过期，请重新登录')
  }

  if (!response.ok) {
    throw new Error(`媒体加载失败：HTTP ${response.status}`)
  }

  const blobUrl = URL.createObjectURL(await response.blob())
  remember(cacheKey, blobUrl)
  return blobUrl
}
