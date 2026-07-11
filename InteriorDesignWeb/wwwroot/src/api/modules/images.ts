// src/api/modules/images.ts
import { refreshAuthSession, requestWithAuth } from '../client'

const BASE_URL = (import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE as string

/* ─────────────────────────────────────────
   类型定义
───────────────────────────────────────── */
export type NormalizedImage = {
  imageId:      string
  room:         string
  fileName:     string
  thumbnailUrl: string
  fullImageUrl: string
}

export type SearchImagesParams = {
  keyword:    string
  pageSize?:  number
  seed?:      string
  pageToken?: string
}

export type SearchImagesResult = {
  success:       boolean
  data:          NormalizedImage[]
  seed:          string
  nextPageToken: string
  hasMore:       boolean
  message:       string
}

/* ─────────────────────────────────────────
   内置 Blob 缓存
   key: imageId, value: blob object URL
───────────────────────────────────────── */
const blobCache = new Map<string, string>()

/* ─────────────────────────────────────────
   ① URL 工具函数
───────────────────────────────────────── */

/**
 * 返回缩略图 URL，可直接用于 <img src>
 * 接口本身公开访问（无需 Authorization）
 */
function getThumbnailUrl(imageId: string): string {
  return `${BASE_URL}/Images/${imageId}/file?type=thumbnail`
}

/**
 * 返回原图 URL
 * 该接口需要 Authorization 头，不可直接用于 <img src>
 * 请使用 fetchOriginalAsBlob 获取可展示的 blob URL
 */
function getOriginalUrl(imageId: string): string {
  return `${BASE_URL}/Images/${imageId}/file`
}

/* ─────────────────────────────────────────
   ② fetchOriginalAsBlob
   带 Authorization 头请求原图，返回 blob URL
   仅在 Lightbox 查看大图时调用
───────────────────────────────────────── */
async function fetchOriginalAsBlob(imageId: string): Promise<string> {
  // 命中缓存直接返回
  const cached = blobCache.get(imageId)
  if (cached) return cached

  let response = await fetch(getOriginalUrl(imageId), {
    credentials: 'include',
  })

  if (response.status === 401 && await refreshAuthSession()) {
    response = await fetch(getOriginalUrl(imageId), { credentials: 'include' })
  }

  if (!response.ok) {
    throw new Error(`图片加载失败: ${response.status}`)
  }

  const blob = await response.blob()
  const blobUrl = URL.createObjectURL(blob)

  // 写入缓存
  blobCache.set(imageId, blobUrl)

  return blobUrl
}

/* ─────────────────────────────────────────
   标准化工具
───────────────────────────────────────── */

/** 兼容后端字段名大小写混用，取第一个有值的字段 */
function pick<T>(
  obj: Record<string, unknown>,
  keys: string[],
  fallback: T,
): T {
  for (const key of keys) {
    const val = obj[key]
    if (val !== undefined && val !== null) return val as T
  }
  return fallback
}

function normalizeImage(raw: Record<string, unknown>): NormalizedImage {
  return {
    imageId:      pick(raw, ['imageID', 'ImageID', 'imageId', 'image_id'], ''),
    room:         pick(raw, ['room', 'Room'], ''),
    fileName:     pick(raw, ['fileName', 'FileName'], ''),
    thumbnailUrl: pick(raw, ['thumbnailUrl', 'ThumbnailUrl'], ''),
    fullImageUrl: pick(raw, ['fullImageUrl', 'FullImageUrl'], ''),
  }
}

function normalizeSearchResult(resp: Record<string, unknown>): SearchImagesResult {
  const rawData = pick<unknown[]>(resp, ['data', 'Data'], [])

  return {
    success:       pick(resp, ['success', 'Success'], false),
    data:          rawData.map((item) => normalizeImage(item as Record<string, unknown>)),
    seed:          pick(resp, ['seed', 'Seed'], ''),
    nextPageToken: pick(resp, ['nextPageToken', 'NextPageToken'], ''),
    hasMore:       pick(resp, ['hasMore', 'HasMore'], false),
    message:       pick(resp, ['message', 'Message'], ''),
  }
}

/* ─────────────────────────────────────────
   ③ searchImages
───────────────────────────────────────── */
async function searchImages(params: SearchImagesParams): Promise<SearchImagesResult> {
  const { keyword, pageSize = 16, seed = '', pageToken = '' } = params

  const qs = new URLSearchParams()
  qs.set('keyword',  keyword)
  qs.set('pageSize', String(pageSize))

  // 非空字符串才加入，避免后端收到空参数产生歧义
  if (seed)      qs.set('seed',      seed)
  if (pageToken) qs.set('pageToken', pageToken)

  const resp = await requestWithAuth(`/Images/search?${qs.toString()}`, {
    method: 'GET',
  }) as Record<string, unknown>

  return normalizeSearchResult(resp)
}

/* ─────────────────────────────────────────
   ④ 导出
───────────────────────────────────────── */
export const imagesApi = {
  getThumbnailUrl,
  getOriginalUrl,
  fetchOriginalAsBlob,
  searchImages,
}
