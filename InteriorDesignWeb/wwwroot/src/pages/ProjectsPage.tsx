// src/pages/ProjectsPage.tsx
import { useState, useEffect, useMemo, useCallback } from 'react'
import ReactDOM from 'react-dom'
import { useNavigate } from 'react-router-dom'
import {
  Search,
  Plus,
  FolderOpen,
  Trash2,
  AlertTriangle,
  Loader2,
} from 'lucide-react'
import { projectsApi } from '../api/modules/projects'
import type { Project } from '../api/modules/projects'
import ProjectDrawer from '../components/ProjectDrawer'

/* ─────────────────────────────────────────
   工具：格式化日期
───────────────────────────────────────── */
function formatDate(iso: string): string {
  try {
    return new Date(iso).toISOString().slice(0, 10)
  } catch {
    return iso
  }
}

/* ─────────────────────────────────────────
   封面渐变色（按索引取模）
───────────────────────────────────────── */
const COVER_GRADIENTS = [
  'from-purple-500/20 via-indigo-500/15 to-blue-500/20',
  'from-emerald-500/20 via-teal-500/15 to-cyan-500/20',
  'from-amber-500/20 via-orange-500/15 to-rose-500/20',
  'from-rose-500/20 via-pink-500/15 to-fuchsia-500/20',
  'from-cyan-500/20 via-sky-500/15 to-blue-500/20',
  'from-violet-500/20 via-purple-500/15 to-indigo-500/20',
]

/* ─────────────────────────────────────────
   骨架屏卡片
───────────────────────────────────────── */
function SkeletonCard() {
  return (
    <div
      className="rounded-2xl overflow-hidden animate-pulse
                 border border-[var(--border-subtle)] bg-[var(--bg-card)]"
    >
      {/* 封面占位 */}
      <div className="h-40 bg-[var(--bg-input)]" />
      {/* 内容占位 */}
      <div className="p-4 space-y-3">
        <div className="h-4 bg-[var(--border-default)] rounded-lg w-3/4" />
        <div className="h-3 bg-[var(--border-subtle)] rounded-lg w-full" />
        <div className="h-3 bg-[var(--border-subtle)] rounded-lg w-2/3" />
        <div className="h-3 bg-[var(--border-subtle)] rounded-lg w-1/3 mt-1" />
        <div className="h-9 bg-[var(--border-subtle)] rounded-xl mt-3" />
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   方案卡片
───────────────────────────────────────── */
type ProjectCardProps = {
  project: Project
  index: number
  onOpen: () => void
  onDelete: () => void
}

function ProjectCard({ project, index, onOpen, onDelete }: ProjectCardProps) {
  const gradient = COVER_GRADIENTS[index % COVER_GRADIENTS.length]
  const hasDesc  = !!project.description?.trim()

  return (
    <div
      onClick={onOpen}
      className="group relative rounded-2xl overflow-hidden cursor-pointer
                 border border-[var(--border-subtle)] bg-[var(--bg-card)]
                 transition-all duration-300 ease-out
                 hover:-translate-y-0.5
                 hover:shadow-[0_12px_40px_rgba(0,0,0,0.25)]
                 hover:border-[var(--border-strong)]"
    >
      {/* ── 封面区域 ── */}
      <div
        className={`relative h-40 bg-gradient-to-br ${gradient}
                    overflow-hidden flex items-center justify-center`}
      >
        <FolderOpen
          size={48}
          className="text-[var(--text-tertiary)]/30 transition-transform
                     duration-300 group-hover:scale-110"
          strokeWidth={1.2}
        />
        {/* hover 叠加层 */}
        <div
          className="absolute inset-0 bg-black/0 group-hover:bg-black/10
                     transition-colors duration-300"
        />
      </div>

      {/* ── 信息区域 ── */}
      <div className="p-4">
        {/* 方案名称 */}
        <h3
          className="text-sm font-semibold text-[var(--text-primary)] truncate mb-1.5"
          title={project.name}
        >
          {project.name}
        </h3>

        {/* 描述（最多两行截断） */}
        <p
          className={`text-xs leading-relaxed line-clamp-2 mb-2
                      ${hasDesc
                        ? 'text-[var(--text-secondary)]'
                        : 'text-[var(--text-tertiary)] italic'
                      }`}
        >
          {hasDesc ? project.description : '暂无描述'}
        </p>

        {/* 创建时间 */}
        <p className="text-[11px] text-[var(--text-tertiary)] mb-3">
          {formatDate(project.createdAt)}
        </p>

        {/* 操作区 */}
        <div className="flex items-center gap-2">
          {/* 进入方案 */}
          <button
            onClick={(e) => { e.stopPropagation(); onOpen() }}
            className="flex-1 flex items-center justify-center gap-1.5
                       px-3 py-2 rounded-xl text-xs font-medium
                       border border-[var(--border-default)]
                       bg-[var(--bg-input)] text-[var(--text-primary)]
                       hover:border-[var(--accent)]/50
                       hover:bg-[var(--accent)]/8
                       transition-all duration-150"
          >
            <FolderOpen size={13} />
            进入方案
          </button>

          {/* 删除按钮 */}
          <button
            onClick={(e) => { e.stopPropagation(); onDelete() }}
            title="删除方案"
            className="flex items-center justify-center p-2 rounded-xl
                       border border-[var(--border-default)]
                       bg-[var(--bg-input)] text-[var(--text-tertiary)]
                       hover:text-red-400
                       hover:border-red-400/30
                       hover:bg-red-400/8
                       transition-all duration-150"
          >
            <Trash2 size={14} />
          </button>
        </div>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   删除确认 Modal（Portal）
───────────────────────────────────────── */
type DeleteModalProps = {
  target:     Project
  isDeleting: boolean
  deleteError: string | null
  onConfirm:  () => void
  onCancel:   () => void
}

function DeleteModal({
  target,
  isDeleting,
  deleteError,
  onConfirm,
  onCancel,
}: DeleteModalProps) {
  return ReactDOM.createPortal(
    <div className="fixed inset-0 z-[500] flex items-center justify-center p-4">
      {/* 蒙层 */}
      <div
        className="absolute inset-0 bg-black/65 backdrop-blur-sm
                   animate-[fadeIn_250ms_ease-out]"
        onClick={() => { if (!isDeleting) onCancel() }}
      />

      {/* 弹窗卡片 */}
      <div
        className="relative w-full max-w-sm rounded-2xl p-6
                   bg-[var(--bg-card)] border border-[var(--border-default)]
                   shadow-[0_24px_80px_rgba(0,0,0,0.5)]
                   animate-[scaleIn_250ms_ease-out]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* 警告图标 */}
        <div className="flex justify-center mb-4">
          <div
            className="w-12 h-12 rounded-full flex items-center justify-center
                       bg-red-500/10 border border-red-500/20"
          >
            <AlertTriangle size={22} className="text-red-400" strokeWidth={1.8} />
          </div>
        </div>

        {/* 标题 */}
        <h3 className="text-base font-semibold text-[var(--text-primary)] text-center mb-2">
          确认删除方案
        </h3>

        {/* 内容 */}
        <div className="text-sm text-[var(--text-secondary)] text-center space-y-1 mb-5">
          <p>
            删除方案「
            <span className="font-medium text-[var(--text-primary)]">
              {target.name}
            </span>
            」后将无法恢复
          </p>
          <p className="text-xs text-[var(--text-tertiary)]">
            方案内的图片关联关系也将同时删除
          </p>
        </div>

        {/* 错误提示 */}
        {deleteError && (
          <div
            className="flex items-start gap-2 px-3 py-2.5 mb-4 rounded-xl
                       bg-red-500/10 border border-red-500/20"
          >
            <AlertTriangle size={14} className="text-red-400 shrink-0 mt-0.5" />
            <p className="text-xs text-red-400 leading-relaxed">{deleteError}</p>
          </div>
        )}

        {/* 按钮区（右对齐） */}
        <div className="flex items-center justify-end gap-3">
          {/* 取消 */}
          <button
            onClick={onCancel}
            disabled={isDeleting}
            className="px-4 py-2 rounded-xl text-sm font-medium
                       border border-[var(--border-default)]
                       bg-[var(--bg-input)] text-[var(--text-primary)]
                       hover:bg-[var(--bg-card)] hover:border-[var(--border-strong)]
                       disabled:opacity-40 disabled:cursor-not-allowed
                       transition-all duration-150"
          >
            取消
          </button>

          {/* 确认删除 */}
          <button
            onClick={onConfirm}
            disabled={isDeleting}
            className="inline-flex items-center gap-2
                       px-4 py-2 rounded-xl text-sm font-medium
                       bg-red-500 hover:bg-red-400 text-white
                       disabled:opacity-60 disabled:cursor-not-allowed
                       transition-all duration-150"
          >
            {isDeleting ? (
              <>
                <Loader2 size={14} className="animate-spin" />
                删除中...
              </>
            ) : (
              '确认删除'
            )}
          </button>
        </div>
      </div>

      {/* 内联动画 keyframe */}
      <style>{`
        @keyframes fadeIn  { from { opacity: 0; } to { opacity: 1; } }
        @keyframes scaleIn { from { opacity: 0; transform: scale(0.94); }
                             to   { opacity: 1; transform: scale(1); } }
      `}</style>
    </div>,
    document.body,
  )
}

/* ─────────────────────────────────────────
   主页面
───────────────────────────────────────── */
export default function ProjectsPage() {
  const navigate = useNavigate()

  /* ── 状态 ── */
  const [projects, setProjects]               = useState<Project[]>([])
  const [isLoading, setIsLoading]             = useState<boolean>(true)
  const [searchKeyword, setSearchKeyword]     = useState<string>('')
  const [deleteTarget, setDeleteTarget]       = useState<Project | null>(null)
  const [isDeleting, setIsDeleting]           = useState<boolean>(false)
  const [deleteError, setDeleteError]         = useState<string | null>(null)
  const [selectedProject, setSelectedProject] = useState<Project | null>(null)

  /* ── 初始加载 ── */
  const loadProjects = useCallback(async () => {
    setIsLoading(true)
    try {
      const result = await projectsApi.getUserProjects()
      setProjects(result)
    } catch (err) {
      console.error('[ProjectsPage] 加载方案列表失败:', err)
      setProjects([])
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    loadProjects()
  }, [loadProjects])

  /* ── 过滤 ── */
  const filteredProjects = useMemo<Project[]>(() => {
    const kw = searchKeyword.trim().toLowerCase()
    if (!kw) return projects
    return projects.filter(
      (p) =>
        p.name.toLowerCase().includes(kw) ||
        (p.description ?? '').toLowerCase().includes(kw),
    )
  }, [projects, searchKeyword])

  /* ── 删除 ── */
  const handleDeleteConfirm = useCallback(async () => {
    if (!deleteTarget) return

    setIsDeleting(true)
    setDeleteError(null)

    try {
      await projectsApi.deleteProject(deleteTarget.projectID)
      // 从列表中移除
      setProjects((prev) =>
        prev.filter((p) => p.projectID !== deleteTarget.projectID),
      )
      // 若抽屉正好打开的是被删除的方案，关闭抽屉
      if (selectedProject?.projectID === deleteTarget.projectID) {
        setSelectedProject(null)
      }
      setDeleteTarget(null)
    } catch (err) {
      const msg = err instanceof Error ? err.message : '删除失败，请稍后重试'
      setDeleteError(msg)
    } finally {
      setIsDeleting(false)
    }
  }, [deleteTarget, selectedProject])

  const handleDeleteCancel = useCallback(() => {
    if (isDeleting) return
    setDeleteTarget(null)
    setDeleteError(null)
  }, [isDeleting])

  /* ── 辅助状态 ── */
  const hasProjects    = projects.length > 0
  const hasFiltered    = filteredProjects.length > 0
  const isSearching    = searchKeyword.trim().length > 0

  /* ─────────────────────────────────────────
     渲染
  ───────────────────────────────────────── */
  return (
    <>
      <div className="min-h-full bg-[var(--bg-base)] px-6 pt-8 pb-12">

        {/* ════════ 顶部操作栏 ════════ */}
        <div className="flex items-center justify-between gap-4 mb-8 flex-wrap">
          {/* 左侧：标题 */}
          <h1 className="text-2xl font-bold text-[var(--text-primary)] tracking-tight shrink-0">
            我的方案
          </h1>

          {/* 右侧：搜索 + 新建 */}
          <div className="flex items-center gap-3">
            {/* 搜索框 */}
            <div className="relative">
              <div
                className="absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none
                           text-[var(--text-tertiary)]"
              >
                <Search size={15} />
              </div>
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="搜索方案名称..."
                className="w-52 pl-10 pr-4 py-2.5 rounded-xl text-sm
                           bg-[var(--bg-card)] border border-[var(--border-default)]
                           text-[var(--text-primary)]
                           placeholder:text-[var(--text-tertiary)]
                           outline-none
                           focus:border-[var(--accent-border)]
                           focus:ring-1 focus:ring-[var(--accent-glow)]
                           transition-all duration-150"
              />
            </div>

            {/* 新建方案按钮 */}
            <button
              onClick={() => navigate('/app/new')}
              className="inline-flex items-center gap-2 px-4 py-2.5
                         rounded-xl text-sm font-semibold
                         bg-[var(--accent)] text-white
                         hover:opacity-90 active:opacity-80
                         transition-opacity duration-150 shrink-0"
            >
              <Plus size={15} />
              新建方案
            </button>
          </div>
        </div>

        {/* ════════ 加载骨架屏 ════════ */}
        {isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5">
            {Array.from({ length: 3 }).map((_, i) => (
              <SkeletonCard key={i} />
            ))}
          </div>
        )}

        {/* ════════ 无方案空状态 ════════ */}
        {!isLoading && !hasProjects && (
          <div className="flex flex-col items-center justify-center py-36">
            <div
              className="w-20 h-20 rounded-2xl mb-5 flex items-center justify-center
                         bg-[var(--bg-card)] border border-[var(--border-subtle)]"
            >
              <FolderOpen
                size={36}
                strokeWidth={1.2}
                className="text-[var(--text-tertiary)] opacity-60"
              />
            </div>
            <h3 className="text-base font-semibold text-[var(--text-primary)] mb-2">
              还没有方案
            </h3>
            <p className="text-sm text-[var(--text-secondary)] mb-6 text-center max-w-xs">
              创建第一个方案，开始你的室内设计之旅
            </p>
            <button
              onClick={() => navigate('/app/new')}
              className="inline-flex items-center gap-2 px-5 py-2.5
                         rounded-xl text-sm font-semibold
                         bg-[var(--accent)] text-white
                         hover:opacity-90 transition-opacity"
            >
              <Plus size={15} />
              新建方案
            </button>
          </div>
        )}

        {/* ════════ 搜索无结果 ════════ */}
        {!isLoading && hasProjects && isSearching && !hasFiltered && (
          <div className="flex flex-col items-center justify-center py-36">
            <Search
              size={40}
              strokeWidth={1.2}
              className="text-[var(--text-tertiary)] opacity-40 mb-4"
            />
            <p className="text-sm font-medium text-[var(--text-secondary)]">
              未找到与「
              <span className="text-[var(--text-primary)]">{searchKeyword}</span>
              」相关的方案
            </p>
            <button
              onClick={() => setSearchKeyword('')}
              className="mt-4 text-sm text-[var(--accent)] hover:underline underline-offset-2"
            >
              清除搜索
            </button>
          </div>
        )}

        {/* ════════ 方案卡片 Grid ════════ */}
        {!isLoading && hasFiltered && (
          <>
            {/* 数量提示 */}
            <p className="text-xs text-[var(--text-tertiary)] mb-4">
              共 {filteredProjects.length} 个方案
              {isSearching && <span className="ml-1">（已过滤）</span>}
            </p>

            <div
              className="grid grid-cols-1 md:grid-cols-2
                         lg:grid-cols-3 xl:grid-cols-4 gap-5"
            >
              {filteredProjects.map((project, idx) => (
                <ProjectCard
                  key={project.projectID}
                  project={project}
                  index={idx}
                  onOpen={() => setSelectedProject(project)}
                  onDelete={() => {
                    setDeleteError(null)
                    setDeleteTarget(project)
                  }}
                />
              ))}
            </div>
          </>
        )}
      </div>

      {/* ════════ 方案详情抽屉 ════════ */}
      <ProjectDrawer
        project={selectedProject}
        onClose={() => setSelectedProject(null)}
      />

      {/* ════════ 删除确认 Modal ════════ */}
      {deleteTarget && (
        <DeleteModal
          target={deleteTarget}
          isDeleting={isDeleting}
          deleteError={deleteError}
          onConfirm={handleDeleteConfirm}
          onCancel={handleDeleteCancel}
        />
      )}
    </>
  )
}