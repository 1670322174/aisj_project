// src/components/Sidebar.tsx
import { useState, useEffect, useRef } from 'react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'
import {
  Sparkles,
  Wand2,
  Images,
  PlusCircle,
  FolderOpen,
  ChevronLeft,
  ChevronRight,
  ChevronDown,
  Sun,
  Moon,
  LogOut,
  User,
  Settings,
  Plus,
  Loader2,
  CheckCircle2,
  History,
  RefreshCw,
  Bot,
  MoreHorizontal,
  Trash2,
  ExternalLink,
  ShieldCheck,
} from 'lucide-react'
import { aiApi, type AIJob } from '@/api/modules/ai'
import { MODE_LABELS, WORKFLOW_MODE } from '@/features/ai/config'
import { useAppStore } from '@/store/useAppStore'
import { cn } from '@/utils/cn'

const NAV_ITEMS = [
  { to: '/app/generate/text', icon: Wand2,      label: 'AI 生成' },
  { to: '/app/gallery',  icon: Images,     label: '图库'   },
  { to: '/app/new',      icon: PlusCircle, label: '新建方案' },
  { to: '/app/projects', icon: FolderOpen, label: '方案管理' },
  { to: '/app/assistant', icon: Bot, label: 'AI 助理' },
]

/* ─────────────────────────────────────────
   方案选择器（仅在侧边栏展开时显示完整 UI）
───────────────────────────────────────── */
interface ProjectSelectorProps {
  collapsed: boolean
}

function ProjectSelector({ collapsed }: ProjectSelectorProps) {
  const {
    activeProject,
    projects,
    isProjectsLoading,
    setActiveProject,
    quickCreateProject,
    loadProjects,
  } = useAppStore()

  const [isDropdownOpen,   setIsDropdownOpen]   = useState(false)
  const [isQuickCreating,  setIsQuickCreating]  = useState(false)
  const [quickCreateError, setQuickCreateError] = useState<string>('')
  const [quickNameInput,   setQuickNameInput]   = useState<string>('')
  const [showQuickInput,   setShowQuickInput]   = useState(false)

  const dropdownRef = useRef<HTMLDivElement>(null)

  /* ── 点击外部关闭下拉 ── */
  useEffect(() => {
    function handleMouseDown(e: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(e.target as Node)
      ) {
        setIsDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleMouseDown)
    return () => document.removeEventListener('mousedown', handleMouseDown)
  }, [])

  /* ── 侧边栏收起时关闭所有面板 ── */
  useEffect(() => {
    if (collapsed) {
      setIsDropdownOpen(false)
      setShowQuickInput(false)
    }
  }, [collapsed])

  /* ── 挂载时确保方案列表已加载 ── */
  useEffect(() => {
    if (projects.length === 0 && !isProjectsLoading) {
      loadProjects().catch(() => {})
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /* ── 快速创建 ── */
  async function handleQuickCreate() {
    if (!quickNameInput.trim()) return
    setIsQuickCreating(true)
    setQuickCreateError('')
    try {
      await quickCreateProject(quickNameInput.trim())
      setQuickNameInput('')
      setShowQuickInput(false)
      setIsDropdownOpen(false)
    } catch (err) {
      const msg = err instanceof Error ? err.message : '创建失败，请重试'
      setQuickCreateError(msg)
    } finally {
      setIsQuickCreating(false)
    }
  }

  /* ─────────────────────────────────────
     收起状态：只显示图标 + tooltip
  ───────────────────────────────────── */
  if (collapsed) {
    const tooltipText = activeProject ? activeProject.name : '未选择方案'
    return (
      <div className="relative flex justify-center group">
        <div
          className="w-10 h-10 flex items-center justify-center rounded-xl
                     text-[var(--text-secondary)]
                     hover:text-[var(--text-primary)]
                     hover:bg-[var(--bg-card)]
                     transition-all duration-150 cursor-default"
        >
          <FolderOpen size={16} />
        </div>
        {/* Tooltip */}
        <div
          className="absolute left-full top-1/2 -translate-y-1/2 ml-2 z-50
                     px-2.5 py-1.5 rounded-lg whitespace-nowrap
                     bg-[var(--bg-card)] border border-[var(--border-default)]
                     text-xs text-[var(--text-primary)] shadow-lg
                     opacity-0 group-hover:opacity-100
                     pointer-events-none transition-opacity duration-150"
        >
          {tooltipText}
        </div>
      </div>
    )
  }

  /* ─────────────────────────────────────
     展开状态：完整选择器 UI
  ───────────────────────────────────── */
  return (
    <div className="px-2" ref={dropdownRef}>

      {/* 标题行 */}
      <div className="flex items-center justify-between px-1 mb-1.5">
        <span className="text-[10px] font-semibold text-[var(--text-tertiary)] uppercase tracking-widest">
          当前方案
        </span>
        <button
          type="button"
          disabled={isQuickCreating}
          onClick={() => {
            setShowQuickInput((v) => !v)
            setQuickCreateError('')
          }}
          className="p-1 rounded-lg text-[var(--text-tertiary)]
                     hover:text-[var(--text-primary)]
                     hover:bg-[var(--bg-card)]
                     disabled:opacity-40 disabled:cursor-not-allowed
                     transition-all duration-150"
          title="快速新建方案"
        >
          {isQuickCreating
            ? <Loader2 size={13} className="animate-spin" />
            : <Plus size={13} />
          }
        </button>
      </div>

      {/* 快速创建输入框（height 过渡） */}
      <div
        className="overflow-hidden transition-all duration-200 ease-out"
        style={{ maxHeight: showQuickInput ? '120px' : '0px' }}
      >
        <div className="pb-2 space-y-1.5">
          <div className="flex items-center gap-1.5">
            <input
              autoFocus={showQuickInput}
              type="text"
              value={quickNameInput}
              onChange={(e) => setQuickNameInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter')  handleQuickCreate()
                if (e.key === 'Escape') {
                  setShowQuickInput(false)
                  setQuickNameInput('')
                }
              }}
              placeholder="方案名称"
              className="flex-1 min-w-0 px-2.5 py-1.5 rounded-lg text-xs outline-none
                         border border-[var(--border-default)]
                         bg-[var(--bg-input)] text-[var(--text-primary)]
                         placeholder:text-[var(--text-tertiary)]
                         focus:border-[var(--accent-border)]
                         focus:ring-1 focus:ring-[var(--accent-glow)]
                         transition-all duration-150"
            />
            <button
              type="button"
              disabled={isQuickCreating || !quickNameInput.trim()}
              onClick={handleQuickCreate}
              className="shrink-0 px-2.5 py-1.5 rounded-lg text-xs font-medium
                         bg-[var(--accent)] text-white
                         hover:opacity-90 active:opacity-80
                         disabled:opacity-40 disabled:cursor-not-allowed
                         transition-all duration-150"
            >
              {isQuickCreating
                ? <Loader2 size={11} className="animate-spin" />
                : '创建'
              }
            </button>
          </div>

          {quickCreateError && (
            <p className="text-[11px] text-red-400 px-0.5 leading-snug">
              {quickCreateError}
            </p>
          )}
        </div>
      </div>

      {/* 下拉触发器 */}
      <div className="relative">
        <button
          type="button"
          onClick={() => setIsDropdownOpen((v) => !v)}
          className="w-full flex items-center justify-between gap-2
                     px-2.5 py-2 rounded-xl text-xs
                     border border-[var(--border-default)]
                     bg-[var(--bg-input)]
                     hover:border-[var(--border-strong)]
                     hover:bg-[var(--bg-card)]
                     transition-all duration-150"
        >
          <div className="flex items-center gap-2 min-w-0">
            <FolderOpen
              size={13}
              className="shrink-0 text-[var(--text-tertiary)]"
            />
            <span
              className={cn(
                'truncate',
                activeProject
                  ? 'text-[var(--text-primary)] font-medium'
                  : 'text-[var(--text-tertiary)]',
              )}
            >
              {activeProject ? activeProject.name : '请选择方案'}
            </span>
          </div>
          <ChevronDown
            size={13}
            className={cn(
              'shrink-0 text-[var(--text-tertiary)] transition-transform duration-200',
              isDropdownOpen && 'rotate-180',
            )}
          />
        </button>

        {/* 下拉面板 */}
        {isDropdownOpen && (
          <div
            className="absolute top-full left-0 right-0 mt-1 z-50
                       rounded-xl border border-[var(--border-default)]
                       bg-[var(--bg-card)]/90 backdrop-blur-md
                       shadow-[0_8px_32px_rgba(0,0,0,0.25)]
                       overflow-hidden"
          >
            <div className="max-h-[240px] overflow-y-auto py-1">

              {/* 加载中 */}
              {isProjectsLoading && (
                <div className="flex items-center justify-center py-6">
                  <Loader2
                    size={16}
                    className="text-[var(--text-tertiary)] animate-spin"
                  />
                </div>
              )}

              {/* 暂无方案 */}
              {!isProjectsLoading && projects.length === 0 && (
                <div className="py-6 text-center">
                  <p className="text-xs text-[var(--text-tertiary)]">
                    暂无方案
                  </p>
                </div>
              )}

              {/* 方案列表 */}
              {!isProjectsLoading && projects.map((project) => {
                const isActive = activeProject?.projectID === project.projectID
                return (
                  <button
                    key={project.projectID}
                    type="button"
                    onClick={() => {
                      setActiveProject(project)
                      setIsDropdownOpen(false)
                    }}
                    className={cn(
                      'w-full flex items-center justify-between gap-2',
                      'px-3 py-2 text-xs transition-all duration-100',
                      isActive
                        ? 'text-[var(--accent)] bg-[var(--accent)]/8'
                        : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-input)]',
                    )}
                  >
                    <span className="truncate font-medium text-left">
                      {project.name}
                    </span>
                    {isActive && (
                      <CheckCircle2
                        size={13}
                        className="shrink-0 text-[var(--accent)]"
                        strokeWidth={2.5}
                      />
                    )}
                  </button>
                )
              })}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function jobRoute(job: AIJob): string {
  const mode = WORKFLOW_MODE[job.workflowCode] ?? 'text'
  return `/app/generate/${mode}/jobs/${job.jobId}`
}

function jobTitle(job: AIJob): string {
  const text = job.prompt?.trim() || MODE_LABELS[WORKFLOW_MODE[job.workflowCode] ?? 'text']
  return text.length > 18 ? `${text.slice(0, 18)}…` : text
}

function jobStatusColor(status: string): string {
  const value = status.toLowerCase()
  if (['succeeded', 'completed', 'success'].includes(value)) return 'bg-emerald-400'
  if (['failed', 'error', 'timeout', 'cancelled', 'canceled'].includes(value)) return 'bg-red-400'
  return 'bg-amber-400 animate-pulse'
}

function isTerminalJob(status: string): boolean {
  return ['succeeded', 'completed', 'success', 'failed', 'error', 'timeout', 'cancelled', 'canceled']
    .includes(status.toLowerCase())
}

function RecentTasks({ collapsed, enabled }: { collapsed: boolean; enabled: boolean }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [jobs, setJobs] = useState<AIJob[]>([])
  const [loading, setLoading] = useState(false)
  const [openMenuJobId, setOpenMenuJobId] = useState('')
  const [deletingJobId, setDeletingJobId] = useState('')
  const [taskError, setTaskError] = useState('')

  const loadJobs = async () => {
    if (!enabled) {
      setJobs([])
      return
    }
    setLoading(true)
    try {
      const result = await aiApi.getJobs(1, 20)
      setJobs(result.items)
    } catch {
      setJobs([])
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadJobs()
    // 进入其他页面或任务完成后回到侧栏时刷新最近记录。
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, location.pathname])

  useEffect(() => {
    if (!openMenuJobId) return
    const closeMenu = () => setOpenMenuJobId('')
    window.addEventListener('click', closeMenu)
    return () => window.removeEventListener('click', closeMenu)
  }, [openMenuJobId])

  const deleteJob = async (job: AIJob) => {
    if (!isTerminalJob(job.status) || deletingJobId) return
    setDeletingJobId(job.jobId)
    setTaskError('')
    try {
      await aiApi.deleteJob(job.jobId)
      setJobs((current) => current.filter((item) => item.jobId !== job.jobId))
      setOpenMenuJobId('')
      if (location.pathname.includes(`/jobs/${job.jobId}`)) {
        const mode = WORKFLOW_MODE[job.workflowCode] ?? 'text'
        navigate(`/app/generate/${mode}`)
      }
    } catch (error) {
      setTaskError(error instanceof Error ? error.message : '任务记录删除失败')
    } finally {
      setDeletingJobId('')
    }
  }

  if (collapsed) {
    const latest = jobs[0]
    return (
      <div className="relative flex justify-center group px-2">
        <button
          type="button"
          disabled={!latest}
          onClick={() => latest && navigate(jobRoute(latest))}
          className="w-10 h-10 flex items-center justify-center rounded-xl text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)] disabled:opacity-40 transition-colors"
        >
          <History size={16} />
        </button>
        <div className="absolute left-full top-1/2 -translate-y-1/2 ml-2 z-50 px-2.5 py-1.5 rounded-lg whitespace-nowrap bg-[var(--bg-card)] border border-[var(--border-default)] text-xs text-[var(--text-primary)] shadow-lg opacity-0 group-hover:opacity-100 pointer-events-none transition-opacity">
          {latest ? `最近任务：${jobTitle(latest)}` : '暂无任务记录'}
        </div>
      </div>
    )
  }

  return (
    <section className="px-2 min-h-0 h-full flex flex-col">
      <div className="flex items-center justify-between px-1 mb-1.5">
        <span className="text-[10px] font-semibold text-[var(--text-tertiary)] uppercase tracking-widest">最近任务</span>
        <button type="button" onClick={() => void loadJobs()} disabled={loading || !enabled} className="p-1 rounded-md text-[var(--text-tertiary)] hover:text-[var(--text-primary)] disabled:opacity-40">
          <RefreshCw size={11} className={loading ? 'animate-spin' : ''} />
        </button>
      </div>
      <div className="flex-1 min-h-0 overflow-y-auto space-y-1 pr-0.5 pb-2">
        {jobs.map((job) => {
          const mode = WORKFLOW_MODE[job.workflowCode] ?? 'text'
          return (
            <div key={job.jobId} className="relative rounded-lg hover:bg-[var(--bg-card)] transition-colors group">
              <button
                type="button"
                onClick={() => navigate(jobRoute(job))}
                className="w-full pl-2.5 pr-8 py-2 text-left"
                title={job.prompt || job.workflowCode}
              >
                <div className="flex items-center gap-2">
                  <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${jobStatusColor(job.status)}`} />
                  <span className="flex-1 min-w-0 text-xs text-[var(--text-secondary)] group-hover:text-[var(--text-primary)] truncate">{jobTitle(job)}</span>
                </div>
                <div className="pl-3.5 mt-1 flex items-center justify-between gap-2 text-[10px] text-[var(--text-tertiary)]">
                  <span>{MODE_LABELS[mode]}</span>
                  <span>{job.createdAt ? new Date(job.createdAt).toLocaleDateString() : ''}</span>
                </div>
              </button>
              <button
                type="button"
                aria-label="任务操作"
                onClick={(event) => {
                  event.stopPropagation()
                  setOpenMenuJobId((current) => current === job.jobId ? '' : job.jobId)
                  setTaskError('')
                }}
                className="absolute right-1.5 top-1.5 w-6 h-6 rounded-md flex items-center justify-center text-[var(--text-tertiary)] opacity-60 group-hover:opacity-100 focus:opacity-100 hover:text-[var(--text-primary)] hover:bg-[var(--bg-input)] transition-all"
              >
                <MoreHorizontal size={14} />
              </button>
              {openMenuJobId === job.jobId ? (
                <div
                  className="absolute right-1.5 top-8 z-50 w-32 p-1 rounded-lg bg-[var(--bg-card)] border border-[var(--border-default)] shadow-xl animate-soft-pop origin-top-right"
                  onClick={(event) => event.stopPropagation()}
                >
                  <button type="button" onClick={() => navigate(jobRoute(job))} className="w-full px-2.5 py-2 rounded-md flex items-center gap-2 text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-input)]">
                    <ExternalLink size={12} />打开任务
                  </button>
                  <button
                    type="button"
                    disabled={!isTerminalJob(job.status) || deletingJobId === job.jobId}
                    onClick={() => void deleteJob(job)}
                    title={!isTerminalJob(job.status) ? '运行中的任务需先取消或等待结束' : undefined}
                    className="w-full px-2.5 py-2 rounded-md flex items-center gap-2 text-xs text-red-400 hover:bg-red-500/10 disabled:text-[var(--text-tertiary)] disabled:hover:bg-transparent disabled:cursor-not-allowed"
                  >
                    {deletingJobId === job.jobId ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />}
                    删除记录
                  </button>
                </div>
              ) : null}
            </div>
          )
        })}
        {!loading && jobs.length === 0 ? (
          <p className="px-2.5 py-3 text-[11px] text-[var(--text-tertiary)]">{enabled ? '暂无任务记录' : '登录后查看任务'}</p>
        ) : null}
        {taskError ? <p className="px-2.5 py-2 text-[10px] text-red-400">{taskError}</p> : null}
      </div>
    </section>
  )
}

/* ─────────────────────────────────────────
   主组件
───────────────────────────────────────── */
export function Sidebar() {
  const [collapsed, setCollapsed] = useState(false)
  const {
    resolvedTheme,
    toggleTheme,
    user,
    logout,
    openLoginModal,
    authUser,
  } = useAppStore()
  const navigate = useNavigate()

  return (
    <aside
      className={cn(
        'relative flex flex-col h-screen shrink-0',
        'bg-[var(--bg-sidebar)] border-r border-[var(--border-subtle)]',
        'transition-[width] duration-300 ease-out',
        collapsed ? 'w-16' : 'w-[220px]',
      )}
    >
      {/* ── Logo ── */}
      <div
        className={cn(
          'flex items-center h-14 px-4 border-b border-[var(--border-subtle)] shrink-0',
          collapsed ? 'justify-center' : 'gap-2.5',
        )}
      >
        <button
          onClick={() => navigate('/')}
          className="flex items-center gap-2.5 group"
        >
          <div
            className="w-7 h-7 rounded-lg bg-[var(--accent)] flex items-center justify-center
                       shrink-0 shadow-[0_0_10px_var(--accent-glow)]
                       group-hover:shadow-[0_0_16px_var(--accent-glow)] transition-all"
          >
            <Sparkles size={14} className="text-white" />
          </div>
          {!collapsed && (
            <span className="text-sm font-semibold text-[var(--text-primary)] truncate tracking-tight">
              DesignAI
            </span>
          )}
        </button>
      </div>

      {/* ── 折叠切换按钮 ── */}
      <button
        onClick={() => setCollapsed((v) => !v)}
        className={cn(
          'absolute -right-3 top-[52px] z-10',
          'w-6 h-6 rounded-full',
          'bg-[var(--bg-card)] border border-[var(--border-default)]',
          'flex items-center justify-center',
          'text-[var(--text-secondary)] hover:text-[var(--text-primary)]',
          'shadow-[var(--shadow-card)] transition-all hover:scale-110',
        )}
      >
        {collapsed
          ? <ChevronRight size={11} />
          : <ChevronLeft  size={11} />
        }
      </button>

      {/* ── 导航项 ── */}
      <nav className="flex flex-col gap-1 p-2 pt-3">
        {!collapsed && (
          <div className="px-2 pb-1">
            <span className="text-[10px] font-semibold text-[var(--text-tertiary)] uppercase tracking-widest">
              工具
            </span>
          </div>
        )}

        {NAV_ITEMS.map((item) => {
          const Icon = item.icon
          return (
            <NavLink
              key={item.to}
              to={item.to}
              title={collapsed ? item.label : undefined}
              className={({ isActive }) =>
                cn(
                  'relative flex items-center rounded-xl transition-all duration-200 group',
                  collapsed ? 'h-10 w-10 justify-center mx-auto' : 'gap-3 px-3 h-9',
                  isActive
                    ? [
                        'bg-[var(--bg-card)] text-[var(--text-primary)]',
                        'border border-[var(--accent-border)] shadow-[0_0_8px_var(--accent-glow)]',
                        'after:absolute after:left-0 after:top-1/2 after:-translate-y-1/2',
                        'after:w-0.5 after:h-4 after:rounded-full after:bg-[var(--accent)]',
                      ].join(' ')
                    : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
                )
              }
            >
              <Icon size={16} className="shrink-0" />
              {!collapsed && (
                <span className="text-sm font-medium truncate">{item.label}</span>
              )}
            </NavLink>
          )
        })}

        {authUser?.role?.toLowerCase() === 'administrator' && (
          <NavLink
            to="/app/admin"
            title={collapsed ? '网站管理' : undefined}
            className={({ isActive }) =>
              cn(
                'relative flex items-center rounded-xl transition-all duration-200 group',
                collapsed ? 'h-10 w-10 justify-center mx-auto' : 'gap-3 px-3 h-9',
                isActive
                  ? 'bg-[var(--bg-card)] text-[var(--text-primary)] border border-[var(--accent-border)] shadow-[0_0_8px_var(--accent-glow)] after:absolute after:left-0 after:top-1/2 after:-translate-y-1/2 after:w-0.5 after:h-4 after:rounded-full after:bg-[var(--accent)]'
                  : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
              )
            }
          >
            <ShieldCheck size={16} className="shrink-0" />
            {!collapsed && <span className="text-sm font-medium truncate">网站管理</span>}
          </NavLink>
        )}
      </nav>

      {/* ════════ 当前方案选择器 ════════ */}
      <div className="px-0 py-3">
        {/* 分割线 */}
        <div className="border-t border-[var(--border-subtle)] mb-3" />
        <ProjectSelector collapsed={collapsed} />
      </div>

      <div className="border-t border-[var(--border-subtle)] pt-3 flex-1 min-h-0 overflow-hidden">
        <RecentTasks collapsed={collapsed} enabled={Boolean(user)} />
      </div>

      {/* ── 底部操作区 ── */}
      <div
        className={cn(
          'mt-auto border-t border-[var(--border-subtle)] p-2 flex flex-col gap-1',
          collapsed && 'items-center',
        )}
      >
        {/* 主题切换 */}
        <button
          onClick={toggleTheme}
          title={collapsed
            ? (resolvedTheme === 'dark' ? '切换亮色' : '切换暗色')
            : undefined
          }
          className={cn(
            'flex items-center rounded-xl transition-all',
            'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
            collapsed ? 'w-10 h-10 justify-center' : 'gap-3 px-3 h-9 w-full',
          )}
        >
          {resolvedTheme === 'dark'
            ? <Sun  size={15} className="shrink-0" />
            : <Moon size={15} className="shrink-0" />
          }
          {!collapsed && (
            <span className="text-sm">
              {resolvedTheme === 'dark' ? '亮色模式' : '暗色模式'}
            </span>
          )}
        </button>

        {/* 设置（占位） */}
        <button
          className={cn(
            'flex items-center rounded-xl transition-all',
            'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
            collapsed ? 'w-10 h-10 justify-center' : 'gap-3 px-3 h-9 w-full',
          )}
        >
          <Settings size={15} className="shrink-0" />
          {!collapsed && <span className="text-sm">设置</span>}
        </button>

        {/* 用户区域 */}
        <div
          className={cn(
            'mt-1 pt-1 border-t border-[var(--border-subtle)]',
            collapsed && 'w-full flex justify-center',
          )}
        >
          {user ? (
            <div
              className={cn(
                'flex items-center rounded-xl',
                collapsed ? 'justify-center' : 'gap-2.5 px-2 py-2',
              )}
            >
              <div
                className="w-7 h-7 rounded-full bg-[var(--accent)] flex items-center
                           justify-center shrink-0 shadow-[0_0_8px_var(--accent-glow)]"
              >
                <User size={12} className="text-white" />
              </div>
              {!collapsed && (
                <>
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-medium text-[var(--text-primary)] truncate">
                      {user.name}
                    </p>
                    <p className="text-[10px] text-[var(--text-tertiary)] truncate">
                      {user.email}
                    </p>
                  </div>
                  <button
                    onClick={logout}
                    title="退出登录"
                    className="text-[var(--text-tertiary)] hover:text-red-400
                               transition-colors p-1 rounded-lg hover:bg-[var(--bg-card)]"
                  >
                    <LogOut size={13} />
                  </button>
                </>
              )}
            </div>
          ) : (
            <button
              onClick={openLoginModal}
              className={cn(
                'flex items-center rounded-xl transition-all',
                'text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
                collapsed ? 'w-10 h-10 justify-center' : 'gap-3 px-3 h-9 w-full',
              )}
            >
              <User size={15} className="shrink-0" />
              {!collapsed && <span className="text-sm">登录</span>}
            </button>
          )}
        </div>
      </div>
    </aside>
  )
}
