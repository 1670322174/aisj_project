// src/pages/GalleryPage.tsx
import { useState, useRef, useCallback } from 'react'
import {
  Search,
  ChevronLeft,
  ChevronRight,
  Shuffle,
  Loader2,
  AlertCircle,
  ImageOff,
  BookmarkPlus,
  Check,
} from 'lucide-react'
import { imagesApi } from '../api/modules/images'
import type { NormalizedImage } from '../api/modules/images'
import { projectsApi } from '../api/modules/projects'
import { useAppStore } from '../store/useAppStore'
import ImageLightbox from '../components/ImageLightbox'
import type { ImageItem } from '../components/ImageLightbox'

/* ─────────────────────────────────────────
   会话状态类型
───────────────────────────────────────── */
type StackEntry = {
  token: string
  seed:  string
}

type Session = {
  seed:          string
  nextPageToken: string
  hasMore:       boolean
  prevStack:     StackEntry[]
  nextStack:     StackEntry[]
  pageIndex:     number
}

const INITIAL_SESSION: Session = {
  seed:          '',
  nextPageToken: '',
  hasMore:       false,
  prevStack:     [],
  nextStack:     [],
  pageIndex:     0,
}

const PAGE_SIZE = 16

/* ─────────────────────────────────────────
   添加状态类型
───────────────────────────────────────── */
type AddStatus = 'idle' | 'loading' | 'success' | 'error'

/* ─────────────────────────────────────────
   骨架屏卡片
───────────────────────────────────────── */
function SkeletonCard() {
  return (
    <div
      className="rounded-xl overflow-hidden
                 bg-[var(--bg-card)] border border-[var(--border-subtle)]"
    >
      <div className="relative w-full" style={{ paddingBottom: '75%' }}>
        <div className="absolute inset-0 animate-pulse bg-[var(--bg-input)]" />
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   图片卡片
───────────────────────────────────────── */
type ImageCardProps = {
  image:          NormalizedImage
  onClick:        () => void
  addStatus:      AddStatus
  onAddToProject: () => void
}

function ImageCard({ image, onClick, addStatus, onAddToProject }: ImageCardProps) {
  const [imgError, setImgError] = useState(false)
  const label = image.room || image.fileName || ''

  const renderAddButton = () => {
    const isLoading = addStatus === 'loading'
    const isSuccess = addStatus === 'success'
    const isError   = addStatus === 'error'

    const handleClick = (e: React.MouseEvent) => {
      e.stopPropagation()
      console.log('[AddBtn] clicked, addStatus=', addStatus)
      if (!isLoading && !isSuccess) {
        onAddToProject()
      }
    }

    return (
      /*
       * DEBUG 说明：
       * 1. 把按钮移出 paddingBottom 撑开层，直接放在卡片根节点（relative）下
       * 2. 加鲜艳背景色（bg-red-500），确认是否可见
       * 3. z-[999] 确认不被遮挡
       * 4. console.log 确认 onClick 是否触发
       */
      <div
        // ← DEBUG: 鲜艳背景，确认按钮位置
        className="absolute bottom-2 right-2 z-[999]"
        onClick={(e) => e.stopPropagation()} // 整个 wrapper 也阻止冒泡
      >
        <button
          onClick={handleClick}
          disabled={isLoading || isSuccess}
          className={`
            w-8 h-8 rounded-lg flex items-center justify-center
            shadow-lg border border-white/20
            transition-all duration-150
            ${isSuccess
              ? 'bg-emerald-500 text-white cursor-default pointer-events-none'
              : isLoading
                ? 'bg-black/60 text-white/50 cursor-not-allowed pointer-events-none'
                : isError
                  ? 'bg-red-500 hover:bg-red-400 text-white cursor-pointer'
                  : 'bg-black/60 hover:bg-black/80 text-white cursor-pointer'
            }
          `}
        >
          {isLoading && <Loader2 size={14} className="animate-spin" />}
          {isSuccess && <Check size={14} strokeWidth={2.5} />}
          {(addStatus === 'idle' || isError) && <BookmarkPlus size={14} strokeWidth={2} />}
        </button>

        {/* DEBUG: 红色小圆点，确认 DOM 节点存在且在视口内 */}
        <div className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-red-500 ring-1 ring-white" />
      </div>
    )
  }

  return (
    /*
     * DEBUG 关键修改：
     * 原来是 overflow-hidden，会裁剪绝对定位子元素
     * 改为 overflow-visible，让按钮不被裁剪
     * 同时在外层加一个 overflow-hidden 的装饰层只负责圆角裁图
     */
    <div
      // 外层：只负责布局、hover 效果，不裁剪
      className="group relative rounded-xl cursor-pointer
                 border border-[var(--border-subtle)] bg-[var(--bg-card)]
                 transition-all duration-300 ease-out
                 hover:-translate-y-0.5
                 hover:shadow-[0_8px_32px_rgba(0,0,0,0.28)]
                 hover:border-[var(--border-strong)]"
      onClick={onClick}
    >
      {/* 内层：负责图片圆角裁剪，overflow-hidden 只作用于图片层 */}
      <div className="rounded-xl overflow-hidden">
        <div className="relative w-full" style={{ paddingBottom: '75%' }}>

          {(imgError || !image.thumbnailUrl) && (
            <div className="absolute inset-0 flex items-center justify-center bg-[var(--accent)]/10">
              <ImageOff size={28} className="text-[var(--text-tertiary)] opacity-40" />
            </div>
          )}

          {image.thumbnailUrl && !imgError && (
            <img
              src={image.thumbnailUrl}
              alt={label}
              loading="lazy"
              onError={() => setImgError(true)}
              className="absolute inset-0 w-full h-full object-cover
                         transition-transform duration-300 group-hover:scale-[1.03]"
            />
          )}

          {label && (
            <div
              className="absolute inset-x-0 bottom-0 z-10
                         translate-y-full group-hover:translate-y-0
                         transition-transform duration-300 ease-out"
            >
              <div className="bg-gradient-to-t from-black/80 via-black/55 to-transparent pt-8 pb-3 px-3">
                <p className="text-white text-xs font-medium truncate leading-snug">
                  {label}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {/*
       * 按钮放在内层 overflow-hidden 的 div 之外、外层 div 之内
       * 这样不会被 overflow-hidden 裁剪，又在卡片的 relative 定位上下文中
       */}
      {renderAddButton()}
    </div>
  )
}

/* ─────────────────────────────────────────
   分页控制
───────────────────────────────────────── */
type PaginationProps = {
  pageIndex:  number
  canPrev:    boolean
  canNext:    boolean
  canRandom:  boolean
  isLoading:  boolean
  onPrev:     () => void
  onNext:     () => void
  onRandom:   () => void
}

function Pagination({
  pageIndex,
  canPrev,
  canNext,
  canRandom,
  isLoading,
  onPrev,
  onNext,
  onRandom,
}: PaginationProps) {
  const btnBase =
    `inline-flex items-center gap-1.5 px-4 py-2 rounded-lg
     text-sm font-medium border transition-all duration-150`

  const btnEnabled =
    `border-[var(--border-default)] bg-[var(--bg-card)]
     text-[var(--text-primary)]
     hover:bg-[var(--bg-input)] hover:border-[var(--border-strong)]`

  const btnDisabled =
    `border-[var(--border-subtle)] bg-[var(--bg-card)]
     text-[var(--text-primary)]
     opacity-40 cursor-not-allowed`

  const prevDisabled   = !canPrev   || isLoading
  const nextDisabled   = !canNext   || isLoading
  const randomDisabled = !canRandom || isLoading

  return (
    <div className="flex items-center justify-center gap-3 py-8">

      {/* 上一页 */}
      <button
        onClick={onPrev}
        disabled={prevDisabled}
        className={`${btnBase} ${prevDisabled ? btnDisabled : btnEnabled}`}
      >
        <ChevronLeft size={16} />
        上一页
      </button>

      {/* 页码 */}
      <span
        className="px-3 text-sm text-[var(--text-secondary)] tabular-nums select-none"
      >
        第{' '}
        <span className="text-[var(--text-primary)] font-semibold">
          {pageIndex}
        </span>{' '}
        页
      </span>

      {/* 下一页 */}
      <button
        onClick={onNext}
        disabled={nextDisabled}
        className={`${btnBase} ${nextDisabled ? btnDisabled : btnEnabled}`}
      >
        下一页
        <ChevronRight size={16} />
      </button>

      {/* 分割线 */}
      <div className="w-px h-6 bg-[var(--border-subtle)] mx-1" />

      {/* 随机按钮 */}
      <div className="relative group/random">
        <button
          onClick={onRandom}
          disabled={randomDisabled}
          className={`${btnBase} ${randomDisabled ? btnDisabled : btnEnabled}`}
        >
          <Shuffle size={15} />
          随机
        </button>
        {/* Tooltip */}
        {!randomDisabled && (
          <div
            className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2
                       px-2.5 py-1 rounded-lg text-xs whitespace-nowrap
                       bg-[var(--bg-base)] border border-[var(--border-default)]
                       text-[var(--text-secondary)] shadow-lg
                       opacity-0 group-hover/random:opacity-100
                       transition-opacity duration-150 pointer-events-none"
          >
            随机探索更多图片
          </div>
        )}
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   主页面
───────────────────────────────────────── */
export default function GalleryPage() {

  /* ── 搜索会话（useRef，不触发重渲染） ── */
  const sessionRef = useRef<Session>({ ...INITIAL_SESSION })

  /* ── UI 状态 ── */
  const [searchInput, setSearchInput] = useState<string>('')
  const [keyword, setKeyword]         = useState<string>('')
  const [images, setImages]           = useState<NormalizedImage[]>([])
  const [isLoading, setIsLoading]     = useState<boolean>(false)
  const [error, setError]             = useState<string | null>(null)
  const [pageIndex, setPageIndex]     = useState<number>(0)
  const [canPrev, setCanPrev]         = useState<boolean>(false)
  const [canNext, setCanNext]         = useState<boolean>(false)
  const [canRandom, setCanRandom]     = useState<boolean>(false)

  /* ── Lightbox 状态 ── */
  const [lbIndex, setLbIndex]   = useState<number>(-1)
  const [lbImages, setLbImages] = useState<ImageItem[]>([])

  /* ── 添加到方案状态 ── */
  const [addingMap, setAddingMap] = useState<Record<string, AddStatus>>({})

  /* ── Toast 状态 ── */
  const [toast, setToast] = useState<{
    message: string
    type:    'success' | 'error'
  } | null>(null)

  /* ── 从 store 读取当前激活方案 ── */
  const activeProject = useAppStore((s) => s.activeProject)

  /* ─────────────────────────────────────
     Toast 工具函数
  ───────────────────────────────────── */
  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ message, type })
    setTimeout(() => setToast(null), 2000)
  }, [])

  /* ─────────────────────────────────────
     handleAddToProject
  ───────────────────────────────────── */
  const handleAddToProject = useCallback(async (image: NormalizedImage) => {
    /* 未选择方案时提示 */
    if (!activeProject) {
      showToast('请先在侧边栏选择一个方案', 'error')
      return
    }

    /* 防重复点击 */
    if (addingMap[image.imageId] === 'loading') return

    setAddingMap((prev) => ({ ...prev, [image.imageId]: 'loading' }))

    try {
      await projectsApi.addImageToProject(
        activeProject.projectID,
        image.imageId,
        image.room ?? '',   // 中文房间名，如"客厅"；空时传空字符串降级处理
      )

      setAddingMap((prev) => ({ ...prev, [image.imageId]: 'success' }))
      showToast(`已添加到「${activeProject.name}」`, 'success')

      /* 2 秒后重置为 idle */
      setTimeout(() => {
        setAddingMap((prev) => ({ ...prev, [image.imageId]: 'idle' }))
      }, 2000)

    } catch (err) {
      const message = err instanceof Error ? err.message : '添加失败，请重试'
      setAddingMap((prev) => ({ ...prev, [image.imageId]: 'error' }))
      showToast(message, 'error')

      /* 2 秒后重置为 idle */
      setTimeout(() => {
        setAddingMap((prev) => ({ ...prev, [image.imageId]: 'idle' }))
      }, 2000)
    }
  }, [activeProject, addingMap, showToast])

  /* ─────────────────────────────────────
     updateButtonStates
  ───────────────────────────────────── */
  const updateButtonStates = useCallback(() => {
    const s = sessionRef.current
    setCanPrev(s.prevStack.length > 0)
    setCanNext(s.nextStack.length > 0 || s.hasMore)
    setCanRandom(s.hasMore)
    setPageIndex(s.pageIndex)
  }, [])

  /* ─────────────────────────────────────
     fetchPage
  ───────────────────────────────────── */
  const fetchPage = useCallback(async (opts: {
    pageToken:        string
    seed:             string
    isPush:           boolean
    keywordOverride?: string
  }) => {
    const { pageToken, seed, isPush, keywordOverride } = opts
    const s = sessionRef.current

    if (isPush) {
      s.prevStack.push({ token: s.nextPageToken, seed: s.seed })
      s.nextStack = []
    }

    setIsLoading(true)
    setError(null)

    try {
      const result = await imagesApi.searchImages({
        keyword:  keywordOverride ?? keyword,
        pageSize: PAGE_SIZE,
        seed,
        pageToken,
      })

      s.seed          = result.seed || seed
      s.nextPageToken = result.nextPageToken
      s.hasMore       = result.hasMore

      if (isPush) {
        s.pageIndex += 1
      }

      setImages(result.data)
      updateButtonStates()
    } catch (err) {
      const msg = err instanceof Error ? err.message : '请求失败，请稍后重试'
      setError(msg)
    } finally {
      setIsLoading(false)
    }
  }, [keyword, updateButtonStates])

  /* ─────────────────────────────────────
     handleSearch
  ───────────────────────────────────── */
  const handleSearch = useCallback(() => {
    const kw = searchInput.trim()
    if (!kw) return

    sessionRef.current = { ...INITIAL_SESSION }
    setKeyword(kw)
    setImages([])
    setError(null)
    setPageIndex(0)
    setCanPrev(false)
    setCanNext(false)
    setCanRandom(false)
    setAddingMap({})   // 切换搜索时清空添加状态

    fetchPage({
      pageToken:       '',
      seed:            '',
      isPush:          false,
      keywordOverride: kw,
    })
  }, [searchInput, fetchPage])

  /* ─────────────────────────────────────
     handlePrev
  ───────────────────────────────────── */
  const handlePrev = useCallback(() => {
    if (!canPrev || isLoading) return
    const s = sessionRef.current

    const prev = s.prevStack.pop()
    if (!prev) return

    s.nextStack.push({ token: s.nextPageToken, seed: s.seed })
    s.pageIndex -= 1

    fetchPage({ pageToken: prev.token, seed: prev.seed, isPush: false })
  }, [canPrev, isLoading, fetchPage])

  /* ─────────────────────────────────────
     handleNext
  ───────────────────────────────────── */
  const handleNext = useCallback(() => {
    if (!canNext || isLoading) return
    const s = sessionRef.current

    if (s.nextStack.length > 0) {
      const next = s.nextStack.pop()!
      s.prevStack.push({ token: s.nextPageToken, seed: s.seed })
      s.pageIndex += 1
      fetchPage({ pageToken: next.token, seed: next.seed, isPush: false })
    } else if (s.hasMore) {
      fetchPage({
        pageToken: s.nextPageToken,
        seed:      s.seed,
        isPush:    true,
      })
    }
  }, [canNext, isLoading, fetchPage])

  /* ─────────────────────────────────────
     handleRandom
  ───────────────────────────────────── */
  const handleRandom = useCallback(() => {
    if (!canRandom || isLoading) return
    const s = sessionRef.current

    const newSeed = Math.random().toString(36).slice(2, 10)

    s.prevStack.push({ token: s.nextPageToken, seed: s.seed })
    s.nextStack  = []
    s.pageIndex += 1

    fetchPage({ pageToken: '', seed: newSeed, isPush: false })
  }, [canRandom, isLoading, fetchPage])

  /* ─────────────────────────────────────
     原图异步加载（Lightbox 用）
  ───────────────────────────────────── */
  const loadOriginal = useCallback((index: number, list: ImageItem[]) => {
    const item = list[index]
    if (!item) return
    if (item.src?.startsWith('blob:')) return

    imagesApi
      .fetchOriginalAsBlob(item.id)
      .then((blobUrl) => {
        setLbImages((prev) =>
          prev.map((img, i) =>
            i === index ? { ...img, src: blobUrl } : img,
          ),
        )
      })
      .catch(() => {
        // 失败静默，保持缩略图
      })
  }, [])

  /* ─────────────────────────────────────
     打开 Lightbox
  ───────────────────────────────────── */
  const openLightbox = useCallback((index: number) => {
    const list: ImageItem[] = images.map((img) => ({
      id:     img.imageId,
      src:    img.thumbnailUrl || undefined,
      label:  img.room || img.fileName || '',
      source: 'ai' as const,
    }))
    setLbImages(list)
    setLbIndex(index)
    loadOriginal(index, list)
  }, [images, loadOriginal])

  /* ─────────────────────────────────────
     Lightbox 翻页时加载原图
  ───────────────────────────────────── */
  const handleLbIndexChange = useCallback((newIndex: number) => {
    setLbIndex(newIndex)
    loadOriginal(newIndex, lbImages)
  }, [lbImages, loadOriginal])

  /* ─────────────────────────────────────
     UI 辅助状态
  ───────────────────────────────────── */
  const isInitial   = keyword === '' && images.length === 0 && !isLoading && !error
  const isEmpty     = keyword !== '' && images.length === 0 && !isLoading && !error
  const showResults = images.length > 0 && !isLoading

  /* ─────────────────────────────────────
     渲染
  ───────────────────────────────────── */
  return (
    <div className="min-h-full bg-[var(--bg-base)]">
      <div className="max-w-7xl mx-auto px-6 pt-8 pb-4">

        {/* ════════ 页面标题 ════════ */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[var(--text-primary)] tracking-tight">
            图库搜索
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            搜索海量室内设计图片
          </p>
        </div>

        {/* ════════ 搜索区域（sticky 吸顶） ════════ */}
        <div
          className="sticky top-0 z-20 pt-2 pb-4 mb-6
                     bg-[var(--bg-base)]
                     border-b border-[var(--border-subtle)]"
        >
          <div className="flex items-center gap-2">
            {/* 输入框 */}
            <div className="relative flex-1">
              <div className="absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none
                              text-[var(--text-tertiary)]">
                <Search size={16} />
              </div>
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                placeholder="搜索室内设计图片，例如：北欧风客厅..."
                className="w-full pl-10 pr-4 py-2.5 rounded-xl text-sm
                           bg-[var(--bg-card)] border border-[var(--border-default)]
                           text-[var(--text-primary)]
                           placeholder:text-[var(--text-tertiary)]
                           outline-none
                           focus:border-[var(--accent-border)]
                           focus:ring-1 focus:ring-[var(--accent-glow)]
                           transition-all duration-150"
              />
            </div>

            {/* 搜索按钮 */}
            <button
              onClick={handleSearch}
              disabled={isLoading || !searchInput.trim()}
              className="shrink-0 inline-flex items-center gap-2
                         px-5 py-2.5 rounded-xl text-sm font-semibold
                         bg-[var(--accent)] text-white
                         hover:opacity-90 active:opacity-80
                         disabled:opacity-50 disabled:cursor-not-allowed
                         transition-all duration-150"
            >
              {isLoading ? (
                <>
                  <Loader2 size={15} className="animate-spin" />
                  搜索中
                </>
              ) : (
                <>
                  <Search size={15} />
                  搜索
                </>
              )}
            </button>
          </div>
        </div>

        {/* ════════ 初始引导状态 ════════ */}
        {isInitial && (
          <div
            className="flex flex-col items-center justify-center py-40
                       text-[var(--text-tertiary)]"
          >
            <div
              className="w-20 h-20 rounded-2xl mb-5 flex items-center justify-center
                         bg-[var(--bg-card)] border border-[var(--border-subtle)]"
            >
              <Search size={32} strokeWidth={1.4} className="opacity-50" />
            </div>
            <p className="text-base font-medium text-[var(--text-secondary)]">
              输入关键词搜索室内设计图片
            </p>
            <p className="text-sm mt-1.5 text-[var(--text-tertiary)]">
              支持风格、房间、场景等关键词
            </p>
          </div>
        )}

        {/* ════════ 加载骨架屏 ════════ */}
        {isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <SkeletonCard key={i} />
            ))}
          </div>
        )}

        {/* ════════ 错误状态 ════════ */}
        {error && !isLoading && (
          <div className="flex flex-col items-center justify-center py-32">
            <div
              className="w-16 h-16 rounded-2xl mb-4 flex items-center justify-center
                         bg-red-500/10 border border-red-500/20"
            >
              <AlertCircle size={28} className="text-red-400" strokeWidth={1.5} />
            </div>
            <p className="text-sm font-medium text-[var(--text-secondary)] mb-1">
              请求失败
            </p>
            <p className="text-xs text-[var(--text-tertiary)] mb-4 max-w-xs text-center">
              {error}
            </p>
            <button
              onClick={handleSearch}
              className="px-4 py-2 rounded-lg text-sm font-medium
                         border border-[var(--border-default)]
                         bg-[var(--bg-card)] text-[var(--text-primary)]
                         hover:bg-[var(--bg-input)] hover:border-[var(--border-strong)]
                         transition-all duration-150"
            >
              重试
            </button>
          </div>
        )}

        {/* ════════ 空结果状态 ════════ */}
        {isEmpty && (
          <div
            className="flex flex-col items-center justify-center py-40
                       text-[var(--text-tertiary)]"
          >
            <div
              className="w-16 h-16 rounded-2xl mb-4 flex items-center justify-center
                         bg-[var(--bg-card)] border border-[var(--border-subtle)]"
            >
              <Search size={26} strokeWidth={1.4} className="opacity-40" />
            </div>
            <p className="text-sm font-medium text-[var(--text-secondary)]">
              没有找到与「{keyword}」相关的图片
            </p>
            <p className="text-xs mt-1 text-[var(--text-tertiary)]">
              换个关键词试试看
            </p>
          </div>
        )}

        {/* ════════ 图片网格 ════════ */}
        {showResults && (
          <>
            {/* 结果信息栏 */}
            <div className="mb-4 flex items-center gap-2">
              <span className="text-xs text-[var(--text-tertiary)]">
                关键词：
              </span>
              <span
                className="text-xs font-medium px-2 py-0.5 rounded-full
                           bg-[var(--accent)]/10 text-[var(--accent)]
                           border border-[var(--accent)]/20"
              >
                {keyword}
              </span>
              <span className="text-xs text-[var(--text-tertiary)]">
                · 共 {images.length} 张
              </span>
            </div>

            {/* 网格 */}
            <div
              className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3
                         xl:grid-cols-4 gap-4"
            >
              {images.map((image, idx) => (
                <ImageCard
                  key={image.imageId}
                  image={image}
                  onClick={() => openLightbox(idx)}
                  addStatus={addingMap[image.imageId] ?? 'idle'}
                  onAddToProject={() => handleAddToProject(image)}
                />
              ))}
            </div>

            {/* 分页控制 */}
            <Pagination
              pageIndex={pageIndex}
              canPrev={canPrev}
              canNext={canNext}
              canRandom={canRandom}
              isLoading={isLoading}
              onPrev={handlePrev}
              onNext={handleNext}
              onRandom={handleRandom}
            />
          </>
        )}
      </div>

      {/* ════════ ImageLightbox ════════ */}
      <ImageLightbox
        images={lbImages}
        currentIndex={lbIndex}
        onClose={() => setLbIndex(-1)}
        onIndexChange={handleLbIndexChange}
      />

      {/* ════════ Toast 通知 ════════ */}
      {toast && (
        <div
          className={`fixed bottom-6 left-1/2 -translate-x-1/2 z-50
                      px-4 py-2.5 rounded-xl text-sm font-medium
                      text-white shadow-lg
                      transition-opacity duration-300
                      ${toast.type === 'success'
                        ? 'bg-emerald-500'
                        : 'bg-red-500'
                      }`}
        >
          {toast.message}
        </div>
      )}
    </div>
  )
}
