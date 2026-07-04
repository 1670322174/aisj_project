// src/store/useAppStore.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { login as apiLogin, logout as apiLogout, initAuth } from '../api'
import type { AuthUser } from '../api'
import { projectsApi } from '../api/modules/projects'
import type { Project } from '../api/modules/projects'

/* ─────────────────────────────────────────
   常量
───────────────────────────────────────── */
const ACTIVE_PROJECT_KEY = 'active_project_id'

/* ─────────────────────────────────────────
   类型定义
───────────────────────────────────────── */
export interface User {
  name:   string
  email:  string
  avatar: string
}

type LoginCredentials = {
  Username: string
  Password: string
}

interface AppState {
  /* ── 主题（原有） ── */
  theme:         'dark' | 'light' | 'system'
  resolvedTheme: 'dark' | 'light'
  setTheme:      (theme: 'dark' | 'light' | 'system') => void
  toggleTheme:   () => void

  /* ── 登录弹窗（原有） ── */
  isLoginModalOpen: boolean
  openLoginModal:   () => void
  closeLoginModal:  () => void

  /* ── 旧用户状态（原有） ── */
  user:   User | null
  login:  (user: User) => void
  logout: () => void

  /* ── API 认证用户（新增） ── */
  authUser:      AuthUser | null
  isAuthLoading: boolean
  authError:     string | null

  /* ── 认证 Actions（新增） ── */
  loginAction:    (credentials: LoginCredentials) => Promise<void>
  logoutAction:   () => void
  initAuthAction: () => Promise<void>
  clearAuthError: () => void

  /* ── 方案状态（新增） ── */
  activeProject:     Project | null
  projects:          Project[]
  isProjectsLoading: boolean

  /* ── 方案 Actions（新增） ── */
  loadProjects:       () => Promise<void>
  setActiveProject:   (project: Project | null) => void
  quickCreateProject: (name: string) => Promise<Project>
}

/* ─────────────────────────────────────────
   主题工具函数（原有，完整保留）
───────────────────────────────────────── */
function getSystemTheme(): 'dark' | 'light' {
  if (typeof window !== 'undefined') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  }
  return 'dark'
}

function resolveTheme(theme: 'dark' | 'light' | 'system'): 'dark' | 'light' {
  if (theme === 'system') return getSystemTheme()
  return theme
}

function applyTheme(resolved: 'dark' | 'light') {
  if (typeof document !== 'undefined') {
    if (resolved === 'light') {
      document.documentElement.setAttribute('data-theme', 'light')
    } else {
      document.documentElement.removeAttribute('data-theme')
    }
  }
}

/* ─────────────────────────────────────────
   AuthUser → User 转换（原有）
───────────────────────────────────────── */
function authUserToUser(authUser: AuthUser): User {
  return {
    name:   authUser.username,
    email:  authUser.phone || `${authUser.username}@designai.com`,
    avatar: `https://api.dicebear.com/7.x/shapes/svg?seed=${authUser.username}`,
  }
}

/* ─────────────────────────────────────────
   Store
───────────────────────────────────────── */
export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({

      /* ══════════════════════════════════════
         主题（原有，完整保留）
      ══════════════════════════════════════ */
      theme:         'system',
      resolvedTheme: getSystemTheme(),

      setTheme: (theme) => {
        const resolved = resolveTheme(theme)
        applyTheme(resolved)
        set({ theme, resolvedTheme: resolved })
      },

      toggleTheme: () => {
        const { resolvedTheme } = get()
        const next = resolvedTheme === 'dark' ? 'light' : 'dark'
        applyTheme(next)
        set({ theme: next, resolvedTheme: next })
      },

      /* ══════════════════════════════════════
         登录弹窗（原有，完整保留）
      ══════════════════════════════════════ */
      isLoginModalOpen: false,
      openLoginModal:   () => set({ isLoginModalOpen: true }),
      closeLoginModal:  () => set({ isLoginModalOpen: false }),

      /* ══════════════════════════════════════
         旧用户状态（原有，完整保留）
      ══════════════════════════════════════ */
      user: null,

      login:  (user) => set({ user, isLoginModalOpen: false }),
      logout: () => set({ user: null }),

      /* ══════════════════════════════════════
         API 认证状态（原有，完整保留）
      ══════════════════════════════════════ */
      authUser:      null,
      isAuthLoading: false,
      authError:     null,

      loginAction: async (credentials) => {
        set({ isAuthLoading: true, authError: null })
        try {
          const authUser = await apiLogin(credentials)
          set({
            authUser,
            user:             authUserToUser(authUser),
            isAuthLoading:    false,
            isLoginModalOpen: false,
          })
          // 登录成功后拉取方案列表
          get().loadProjects().catch(() => {})
        } catch (error) {
          const message = error instanceof Error ? error.message : '登录失败，请稍后重试'
          set({ authError: message, isAuthLoading: false })
        }
      },

      logoutAction: () => {
        apiLogout()
        // 清除方案相关状态
        localStorage.removeItem(ACTIVE_PROJECT_KEY)
        set({
          authUser:      null,
          user:          null,
          authError:     null,
          activeProject: null,
          projects:      [],
        })
      },

      /* ── initAuthAction：恢复登录状态后拉取方案列表 ── */
      initAuthAction: async () => {
        try {
          const authUser = await initAuth()
          if (authUser) {
            set({
              authUser,
              user: authUserToUser(authUser),
            })
            // 恢复登录后，fire and forget 拉取方案
            get().loadProjects().catch(() => {})
          } else {
            set({ authUser: null, user: null })
          }
        } catch {
          set({ authUser: null, user: null })
        }
      },

      clearAuthError: () => set({ authError: null }),

      /* ══════════════════════════════════════
         方案状态（新增）
      ══════════════════════════════════════ */
      activeProject:     null,
      projects:          [],
      isProjectsLoading: false,

      /* ── loadProjects ── */
      loadProjects: async () => {
        set({ isProjectsLoading: true })
        try {
          const result = await projectsApi.getUserProjects()
          set({ projects: result, isProjectsLoading: false })

          // 恢复上次选中的方案
          const savedId = localStorage.getItem(ACTIVE_PROJECT_KEY)
          if (savedId) {
            const found = result.find((p) => p.projectID === savedId) ?? null
            if (found) {
              set({ activeProject: found })
            } else {
              // 方案已被删除，清除本地记录
              set({ activeProject: null })
              localStorage.removeItem(ACTIVE_PROJECT_KEY)
            }
          }
        } catch {
          set({ projects: [], isProjectsLoading: false })
        }
      },

      /* ── setActiveProject ── */
      setActiveProject: (project) => {
        set({ activeProject: project })
        if (project) {
          localStorage.setItem(ACTIVE_PROJECT_KEY, project.projectID)
        } else {
          localStorage.removeItem(ACTIVE_PROJECT_KEY)
        }
      },

      /* ── quickCreateProject ── */
      quickCreateProject: async (name) => {
        const project = await projectsApi.createProject({
          name:        name.trim(),
          description: '',
        })

        // 批量添加默认房间（部分失败不阻断）
        await projectsApi.addRoomsToProject(project.projectID, {
          bedroom:    1,
          livingRoom: 1,
          bathroom:   1,
          balcony:    1,
        })

        // 添加到列表头部并自动选中
        set({ projects: [project, ...get().projects] })
        get().setActiveProject(project)

        return project
      },
    }),

    /* ─────────────────────────────────────────
       持久化配置（原有，完整保留）
       activeProject / projects 不持久化，
       由 loadProjects 在启动时从接口恢复
    ───────────────────────────────────────── */
    {
      name: 'designai-store',

      partialize: (state) => ({
        theme: state.theme,
        user:  state.user,
      }),

      onRehydrateStorage: () => (state) => {
        if (state) {
          const resolved = resolveTheme(state.theme)
          applyTheme(resolved)
          state.resolvedTheme = resolved
        }
      },
    },
  ),
)

/* ─────────────────────────────────────────
   系统主题变化监听（原有，完整保留）
───────────────────────────────────────── */
if (typeof window !== 'undefined') {
  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    const { theme, setTheme } = useAppStore.getState()
    if (theme === 'system') setTheme('system')
  })
}

/* ─────────────────────────────────────────
   全局 401 事件监听（原有，完整保留）
───────────────────────────────────────── */
if (typeof window !== 'undefined') {
  window.addEventListener('auth:unauthorized', () => {
    const store = useAppStore.getState()
    store.logoutAction()
    store.openLoginModal()
  })
}