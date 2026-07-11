// src/api/auth.ts
import { request, requestWithAuth } from './client'

/* ─────────────────────────────────────────
   常量
───────────────────────────────────────── */
const LEGACY_TOKEN_KEY = 'access_token'

/* ─────────────────────────────────────────
   用户信息类型
───────────────────────────────────────── */
export type AuthUser = {
  userId: string
  username: string
  role: string
  phone: string
  expiresAt: number
}

type LoginCredentials = {
  Username: string
  Password: string
}

/* ─────────────────────────────────────────
   Token 工具函数
───────────────────────────────────────── */
export function getToken(): string | null {
  return null
}

export function setToken(token: string): void {
  void token
}

export function clearToken(): void {
  localStorage.removeItem(LEGACY_TOKEN_KEY)
}

export function parseToken(token: string): AuthUser | null {
  void token
  return null
}

export function isTokenValid(token: string | null): boolean {
  void token
  return false
}

export function getCurrentUser(): AuthUser | null {
  return null
}

function normalizeUser(raw: Record<string, unknown>, expiresAt = 0): AuthUser {
  return {
    userId: String(raw.userID ?? raw.UserID ?? raw.userId ?? ''),
    username: String(raw.userName ?? raw.UserName ?? raw.username ?? ''),
    role: String(raw.role ?? raw.Role ?? ''),
    phone: String(raw.phoneNumber ?? raw.PhoneNumber ?? raw.phone ?? ''),
    expiresAt,
  }
}

/* ─────────────────────────────────────────
   业务函数
───────────────────────────────────────── */
export async function login(credentials: LoginCredentials): Promise<AuthUser> {
  const result = await request('/User/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  }) as Record<string, unknown>

  // 适配响应结构：{ data: { token: '...' }, message: '...' }
  const data = (result.data ?? result.Data) as Record<string, unknown> | undefined
  const userInfo = (data?.userInfo ?? data?.UserInfo) as Record<string, unknown> | undefined

  if (!userInfo) {
    throw new Error((result?.['message'] as string) || '登录失败')
  }

  clearToken()
  const expiresAt = Date.parse(String(data?.expires ?? data?.Expires ?? ''))

  const user = normalizeUser(userInfo, Number.isFinite(expiresAt) ? expiresAt : 0)
  if (!user) throw new Error('Token 解析失败')

  return user
}

export function logout(): void {
  clearToken()
  void request('/User/logout', { method: 'POST' }).catch(() => undefined)
  // 无服务端登出接口，仅清除本地 token
}

export async function initAuth(): Promise<AuthUser | null> {
  clearToken()
  try {
    const result = await requestWithAuth('/User/me') as Record<string, unknown>
    const data = (result.data ?? result.Data) as Record<string, unknown> | undefined
    return data ? normalizeUser(data) : null
  } catch {
    return null
  }
}
