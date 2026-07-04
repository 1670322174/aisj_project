// src/api/auth.ts
import { request } from './client'

/* ─────────────────────────────────────────
   常量
───────────────────────────────────────── */
const TOKEN_KEY = 'access_token'

const CLAIMS = {
  USER_ID:  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  USERNAME: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
  ROLE:     'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  PHONE:    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone',
} as const

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
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export function parseToken(token: string): AuthUser | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1])) as Record<string, unknown>
    return {
      userId:    (payload[CLAIMS.USER_ID]  as string) ?? '',
      username:  (payload[CLAIMS.USERNAME] as string) ?? '',
      role:      (payload[CLAIMS.ROLE]     as string) ?? '',
      phone:     (payload[CLAIMS.PHONE]    as string) ?? '',
      expiresAt: ((payload['exp'] as number) ?? 0) * 1000,
    }
  } catch {
    return null
  }
}

export function isTokenValid(token: string | null): boolean {
  if (!token) return false

  const parsed = parseToken(token)
  if (!parsed) return false

  if (parsed.expiresAt < Date.now()) {
    clearToken()
    return false
  }

  return true
}

export function getCurrentUser(): AuthUser | null {
  const token = getToken()
  if (!isTokenValid(token)) return null
  return parseToken(token!)
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
  const data = result?.['data'] as Record<string, unknown> | undefined
  const token = data?.['token'] as string | undefined

  if (!token) {
    throw new Error((result?.['message'] as string) || '登录失败')
  }

  setToken(token)

  const user = parseToken(token)
  if (!user) throw new Error('Token 解析失败')

  return user
}

export function logout(): void {
  clearToken()
  // 无服务端登出接口，仅清除本地 token
}

export async function initAuth(): Promise<AuthUser | null> {
  const token = getToken()
  if (!isTokenValid(token)) return null
  return getCurrentUser()
}