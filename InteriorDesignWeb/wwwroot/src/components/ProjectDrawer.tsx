// src/components/ProjectDrawer.tsx
import { useState, useEffect, useRef, useMemo, useCallback } from 'react'
import ReactDOM from 'react-dom'
import { X, LayoutGrid, Rows, Trash2, Loader2, AlertCircle } from 'lucide-react'
import { projectsApi } from '../api/modules/projects'
import type { NormalizedProjectImage } from '../api/modules/projects'
import type { Project } from '../api/modules/projects'
import ImageLightbox from './ImageLightbox'
import type { ImageItem } from './ImageLightbox'

/* ─────────────────────────────────────────
   房间名称映射
───────────────────────────────────────── */
const ROOM_LABELS: Record<string, string> = {
  bedroom:       '卧室',
  living_room:   '客厅',
  bathroom:      '卫生间',
  kitchen:       '厨房',
  study:         '书房',
  balcony:       '阳台',
  dining_room:   '餐厅',
  entrance:      '玄关',
  unclassified:  '未分类',
}

const getRoomLabel = (room: string): string => ROOM_LABELS[room] ?? room

/* ─────────────────────────────────────────
   类型定义
───────────────────────────────────────── */
type ViewMode = 'filter' | 'group'

interface TagItem {
  id:    string
  label: string
  count: number
}

interface LbImage {
  id:     string
  src:    string
  label:  string
  source: 'ai' | 'custom'
}

interface Props {
  project: Project | null
  onClose: () => void
}

/* ─────────────────────────────────────────
   图片卡片子组件
───────────────────────────────────────── */
interface ImageCardProps {
  image:      NormalizedProjectImage
  projectId:  string
  compact?:   boolean
  onClick:    () => void
  onRemove:   () => void
  isRemoving: boolean
}

function ImageCard({
  image,
  projectId,
  compact = false,
  onClick,
  onRemove,
  isRemoving,
}: ImageCardProps) {
  const [imgError, setImgError] = useState(false)
  const [thumbnailSrc, setThumbnailSrc] = useState('')

  const containerClass = compact
    ? 'w-40 h-[120px] flex-shrink-0'
    : 'aspect-[4/3] w-full'

  useEffect(() => {
    let disposed = false
    setImgError(false)
    setThumbnailSrc('')

    projectsApi.fetchProjectImageMedia(projectId, image.relationID, 'thumbnail')
      .then((url) => {
        if (!disposed) setThumbnailSrc(url)
      })
      .catch(() => {
        if (!disposed) setImgError(true)
      })

    return () => { disposed = true }
  }, [image.relationID, projectId])

  return (
    <div
      onClick={onClick}
      className={`relative group overflow-hidden rounded-xl cursor-pointer
                  ${containerClass}`}
    >
      {/* 占位色块 */}
      <div
        className={`absolute inset-0 transition-transform duration-200
                    group-hover:scale-105
                    ${image.isAi
                      ? 'bg-[color:var(--color-accent)]/15'
                      : 'bg-neutral-500/25'
                    }`}
      />

      {/* 真实缩略图（使用构造后的接口地址） */}
      {!imgError && thumbnailSrc && (
        <img
          src={thumbnailSrc}
          alt={getRoomLabel(image.room)}
          loading="lazy"
          onError={() => setImgError(true)}
          className="absolute inset-0 w-full h-full object-cover
                     transition-transform duration-200 group-hover:scale-105"
        />
      )}

      {/* 来源徽章（右上角） */}
      <div className="absolute top-1.5 right-1.5 z-10">
        <span
          className={`text-[10px] font-semibold px-1.5 py-0.5 rounded-md
            ${image.isAi
              ? 'bg-[var(--accent)]/80 text-white'
              : 'bg-black/40 text-white/80'
            }`}
        >
          {image.isAi ? 'AI' : '自定义'}
        </span>
      </div>

      {/* Hover 遮罩 */}
      <div
        className="absolute inset-0 z-20
                   bg-black/0 group-hover:bg-black/50
                   opacity-0 group-hover:opacity-100
                   transition-all duration-200"
      />

      {/* 移除按钮（右下角） */}
      <div
        className="absolute bottom-2 right-2 z-30
                   opacity-0 group-hover:opacity-100
                   transition-opacity duration-200"
      >
        <button
          title="移除图片"
          disabled={isRemoving}
          onClick={(e) => {
            e.stopPropagation()
            onRemove()
          }}
          className={`p-1.5 rounded-lg border border-white/20 backdrop-blur-sm
                      text-white/80 hover:text-white
                      transition-all duration-150
                      ${isRemoving
                        ? 'bg-black/40 pointer-events-none'
                        : 'bg-black/40 hover:bg-red-500/70 cursor-pointer'
                      }`}
        >
          {isRemoving
            ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
            : <Trash2   className="h-3.5 w-3.5" />
          }
        </button>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   骨架屏
───────────────────────────────────────── */
function SkeletonCard({ compact = false }: { compact?: boolean }) {
  return (
    <div
      className={`animate-pulse rounded-xl bg-white/5 flex-shrink-0
                  ${compact ? 'w-40 h-[120px]' : 'aspect-[4/3] w-full'}`}
    />
  )
}

/* ─────────────────────────────────────────
   空状态
───────────────────────────────────────── */
function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div
        className="w-14 h-14 rounded-full mb-4 flex items-center justify-center
                   bg-[var(--bg-card)] border border-[var(--border-default)]"
      >
        <LayoutGrid className="h-6 w-6 text-[var(--text-tertiary)]" />
      </div>
      <p className="text-sm text-[var(--text-secondary)]">{message}</p>
    </div>
  )
}

/* ─────────────────────────────────────────
   主组件
───────────────────────────────────────── */
export default function ProjectDrawer({ project, onClose }: Props) {
  /* ── UI 状态 ── */
  const [displayProject, setDisplayProject] = useState<Project | null>(null)
  const [viewMode, setViewMode]       = useState<ViewMode>('filter')
  const [selectedTag, setSelectedTag] = useState<string>('all')
  const [images, setImages]           = useState<NormalizedProjectImage[]>([])
  const [isLoading, setIsLoading]     = useState<boolean>(false)
  const [removingId, setRemovingId]   = useState<string | null>(null)
  const [removeError, setRemoveError] = useState<string | null>(null)

  /* ── Lightbox 状态 ── */
  const [lbIndex, setLbIndex]   = useState<number>(-1)
  const [lbImages, setLbImages] = useState<LbImage[]>([])

  /* ── 动画状态 ── */
  const [isMounted, setIsMounted] = useState<boolean>(false)
  const [isVisible, setIsVisible] = useState<boolean>(false)

  const closeTimerRef      = useRef<ReturnType<typeof setTimeout> | null>(null)
  const removeErrorTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  /* ─────────────────────────────────────────
     打开 / 关闭动画编排
  ───────────────────────────────────────── */
  useEffect(() => {
    if (project) {
      // 清除可能残留的关闭计时器
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current)

      setIsMounted(true)
      setDisplayProject(project)
      setSelectedTag('all')
      setViewMode('filter')
      setLbIndex(-1)
      setLbImages([])
      setRemoveError(null)
      document.body.style.overflow = 'hidden'

      // 下一帧触发 CSS 过渡
      requestAnimationFrame(() => {
        requestAnimationFrame(() => setIsVisible(true))
      })

      loadImages(project.projectID)
    } else if (displayProject) {
      handleClose()
    }

    return () => {
      if (closeTimerRef.current)       clearTimeout(closeTimerRef.current)
      if (removeErrorTimerRef.current) clearTimeout(removeErrorTimerRef.current)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [project])

  /* ─────────────────────────────────────────
     数据加载
  ───────────────────────────────────────── */
  const loadImages = async (projectId: string) => {
    setIsLoading(true)
    try {
      const result = await projectsApi.getProjectImages(projectId)
      setImages(result)
    } catch (err) {
      console.error('[ProjectDrawer] 加载图片失败:', err)
      setImages([])
    } finally {
      setIsLoading(false)
    }
  }

  /* ─────────────────────────────────────────
     关闭流程
  ───────────────────────────────────────── */
  const handleClose = useCallback(() => {
    setIsVisible(false)
    setLbIndex(-1)

    closeTimerRef.current = setTimeout(() => {
      setIsMounted(false)
      setDisplayProject(null)
      setImages([])
      setSelectedTag('all')
      setViewMode('filter')
      setLbImages([])
      setRemoveError(null)
      setRemovingId(null)
      document.body.style.overflow = ''
    }, 300)

    onClose()
  }, [onClose])

  /* ─────────────────────────────────────────
     派生数据
  ───────────────────────────────────────── */
  const groupedImages = useMemo(
    () => projectsApi.groupImagesByRoom(images),
    [images],
  )

  const tags = useMemo<TagItem[]>(() => {
    const roomKeys = Array.from(groupedImages.keys())
      .filter((k) => k !== 'unclassified')
      .sort()

    const unclassifiedCount = groupedImages.get('unclassified')?.length ?? 0

    return [
      { id: 'all', label: '全部', count: images.length },
      ...roomKeys.map((key) => ({
        id:    key,
        label: getRoomLabel(key),
        count: groupedImages.get(key)?.length ?? 0,
      })),
      ...(unclassifiedCount > 0
        ? [{ id: 'unclassified', label: '未分类', count: unclassifiedCount }]
        : []),
    ]
  }, [groupedImages, images.length])

  const filteredImages = useMemo<NormalizedProjectImage[]>(() => {
    if (selectedTag === 'all') return images
    return groupedImages.get(selectedTag) ?? []
  }, [selectedTag, images, groupedImages])

  const sortedGroupKeys = useMemo<string[]>(() => {
    const keys = Array.from(groupedImages.keys()).filter((k) => k !== 'unclassified').sort()
    if (groupedImages.has('unclassified') && (groupedImages.get('unclassified')?.length ?? 0) > 0) {
      keys.push('unclassified')
    }
    return keys
  }, [groupedImages])

  /* ─────────────────────────────────────────
     移除图片
  ───────────────────────────────────────── */
  const handleRemoveImage = useCallback(async (image: NormalizedProjectImage) => {
    if (!displayProject) return

    setRemovingId(image.relationID)
    setRemoveError(null)

    try {
      await projectsApi.removeImageFromProject(displayProject.projectID, image.relationID)

      setImages((prev) => prev.filter((img) => img.relationID !== image.relationID))

      // 同步更新 lbImages
      if (lbIndex >= 0) {
        setLbImages((prev) => {
          const next = prev.filter((lb) => lb.id !== image.relationID)
          const removedIdx = prev.findIndex((lb) => lb.id === image.relationID)
          if (removedIdx !== -1 && lbIndex >= removedIdx) {
            setLbIndex((i) => Math.max(0, i - 1))
          }
          if (next.length === 0) setLbIndex(-1)
          return next
        })
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : '移除失败，请稍后重试'
      setRemoveError(msg)

      // 2 秒后自动清除错误
      if (removeErrorTimerRef.current) clearTimeout(removeErrorTimerRef.current)
      removeErrorTimerRef.current = setTimeout(() => setRemoveError(null), 2000)
    } finally {
      setRemovingId(null)
    }
  }, [displayProject, lbIndex])

  /* ─────────────────────────────────────────
     Lightbox 工具
  ───────────────────────────────────────── */
  const buildLbImages = useCallback(
    (list: NormalizedProjectImage[]): LbImage[] =>
      list.map((img) => ({
        id:     img.relationID,
        src:    '',
        label:  getRoomLabel(img.room),
        source: img.isAi ? 'ai' : 'custom',
      })),
    [],
  )

  const loadOriginal = useCallback(
    (index: number, list: LbImage[]) => {
      const item = list[index]
      if (!item || !displayProject || item.src.startsWith('blob:')) return

      projectsApi
        .fetchProjectImageMedia(displayProject.projectID, item.id, 'original')
        .then((blobUrl) => {
          setLbImages((prev) =>
            prev.map((lb, i) => (i === index ? { ...lb, src: blobUrl } : lb)),
          )
        })
        .catch(() => {})
    },
    [displayProject],
  )

  const openLightbox = useCallback(
    (clickedIndex: number, list: NormalizedProjectImage[]) => {
      const lbList = buildLbImages(list)
      setLbImages(lbList)
      setLbIndex(clickedIndex)
      loadOriginal(clickedIndex, lbList)
    },
    [buildLbImages, loadOriginal],
  )

  const handleLbIndexChange = useCallback(
    (newIndex: number) => {
      setLbIndex(newIndex)
      loadOriginal(newIndex, lbImages)
    },
    [lbImages, loadOriginal],
  )

  /* ─────────────────────────────────────────
     空状态文案
  ───────────────────────────────────────── */
  const getEmptyMessage = (tagId: string): string => {
    if (tagId === 'all')          return '该方案还没有图片'
    if (tagId === 'unclassified') return '没有未分类的图片'
    return `「${getRoomLabel(tagId)}」还没有图片`
  }

  /* ─────────────────────────────────────────
     不挂载时不渲染
  ───────────────────────────────────────── */
  if (!isMounted || !displayProject) return null

  return ReactDOM.createPortal(
    <div className="fixed inset-0 z-[200]">

      {/* ── 背景蒙层 ── */}
      <div
        className="absolute inset-0 bg-black/70
                   transition-opacity duration-200"
        style={{ opacity: isVisible ? 1 : 0 }}
        onClick={handleClose}
      />

      {/* ── 抽屉主体 ── */}
      <div
        className="absolute top-0 right-0 h-full flex flex-col
                   bg-[var(--bg-base)] border-l border-[var(--border-default)]
                   shadow-xl transition-transform duration-[250ms] ease-out will-change-transform"
        style={{
          width:     'min(82vw, 1000px)',
          minWidth:  '600px',
          transform: isVisible ? 'translateX(0)' : 'translateX(100%)',
        }}
      >

        {/* ════ 顶部 Header ════ */}
        <div className="flex-shrink-0 px-6 pt-6 pb-4 border-b border-[var(--border-default)]">
          <div className="flex items-start justify-between gap-4">

            {/* 左侧：标题 + 描述 */}
            <div className="min-w-0 flex-1">
              <h2 className="text-xl font-semibold text-[var(--text-primary)] truncate">
                {displayProject.name}
              </h2>
              {displayProject.description && (
                <p className="mt-1 text-sm text-[var(--text-secondary)] truncate">
                  {displayProject.description}
                </p>
              )}
            </div>

            {/* 右侧：视图切换 + 关闭 */}
            <div className="flex items-center gap-3 flex-shrink-0">

              {/* Segmented Control */}
              <div
                className="flex items-center gap-0.5 p-1 rounded-xl
                           bg-[var(--bg-card)] border border-[var(--border-default)]"
              >
                {(
                  [
                    { key: 'filter' as const, Icon: LayoutGrid, label: '筛选' },
                    { key: 'group'  as const, Icon: Rows,        label: '分组' },
                  ] as const
                ).map(({ key, Icon, label }) => (
                  <button
                    key={key}
                    onClick={() => {
                      setViewMode(key)
                      setSelectedTag('all')
                    }}
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg
                                text-xs font-medium transition-all duration-200
                                ${
                                  viewMode === key
                                    ? 'bg-[var(--accent)]/15 text-[var(--text-primary)] border border-[var(--accent)]/30'
                                    : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)]'
                                }`}
                  >
                    <Icon className="h-3.5 w-3.5" />
                    <span>{label}</span>
                  </button>
                ))}
              </div>

              {/* 关闭按钮 */}
              <button
                onClick={handleClose}
                className="p-2 rounded-xl border border-transparent
                           text-[var(--text-secondary)] hover:text-[var(--text-primary)]
                           hover:bg-[var(--bg-card)] hover:border-[var(--border-default)]
                           transition-all duration-150"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
          </div>

          {/* 移除错误提示条 */}
          {removeError && (
            <div
              className="flex items-center justify-between gap-2 mt-3
                         px-3 py-2 rounded-xl
                         bg-red-500/10 border border-red-500/20"
            >
              <div className="flex items-center gap-2">
                <AlertCircle size={14} className="text-red-400 shrink-0" />
                <p className="text-xs text-red-400">{removeError}</p>
              </div>
              <button
                onClick={() => setRemoveError(null)}
                className="text-red-400/60 hover:text-red-400 transition-colors shrink-0"
              >
                <X size={14} />
              </button>
            </div>
          )}
        </div>

        {/* ════ 标签筛选栏（filter 模式下展开） ════ */}
        <div
          className="flex-shrink-0 overflow-hidden transition-all duration-300 ease-out
                     border-b border-[var(--border-default)]"
          style={{
            maxHeight: viewMode === 'filter' ? '60px' : '0px',
            opacity:   viewMode === 'filter' ? 1 : 0,
          }}
        >
          <div
            className="flex items-center gap-2 px-6 py-3
                       overflow-x-auto whitespace-nowrap
                       [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
          >
            {isLoading
              ? [1, 2, 3].map((i) => (
                  <div
                    key={i}
                    className="animate-pulse h-7 w-16 rounded-full
                               bg-[var(--bg-card)] flex-shrink-0"
                  />
                ))
              : tags.map((tag) => {
                  const isActive = selectedTag === tag.id
                  return (
                    <button
                      key={tag.id}
                      onClick={() => setSelectedTag(tag.id)}
                      className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full
                                  text-xs font-medium flex-shrink-0
                                  border transition-all duration-200
                                  ${
                                    isActive
                                      ? 'bg-[var(--accent)] border-[var(--accent)] text-white'
                                      : `bg-transparent border-[var(--border-default)]
                                         text-[var(--text-secondary)]
                                         hover:text-[var(--text-primary)]
                                         hover:border-[var(--border-strong)]
                                         hover:bg-[var(--bg-card)]`
                                  }`}
                    >
                      <span>{tag.label}</span>
                      <span
                        className={`px-1 py-0.5 rounded text-[10px] leading-none
                          ${isActive ? 'bg-white/25' : 'bg-[var(--bg-input)]'}`}
                      >
                        {tag.count}
                      </span>
                    </button>
                  )
                })}
          </div>
        </div>

        {/* ════ 内容区（可滚动） ════ */}
        <div className="flex-1 overflow-y-auto p-6">

          {/* ── 加载状态 ── */}
          {isLoading && (
            <div className="grid grid-cols-4 gap-3">
              {Array.from({ length: 8 }).map((_, i) => (
                <SkeletonCard key={i} />
              ))}
            </div>
          )}

          {/* ── 筛选视图 ── */}
          {!isLoading && viewMode === 'filter' && (
            filteredImages.length === 0
              ? <EmptyState message={getEmptyMessage(selectedTag)} />
              : (
                <div className="grid grid-cols-4 gap-3">
                  {filteredImages.map((img, idx) => (
                    <ImageCard
                      key={img.relationID}
                      image={img}
                      projectId={displayProject.projectID}
                      isRemoving={removingId === img.relationID}
                      onClick={() => openLightbox(idx, filteredImages)}
                      onRemove={() => handleRemoveImage(img)}
                    />
                  ))}
                </div>
              )
          )}

          {/* ── 分组视图 ── */}
          {!isLoading && viewMode === 'group' && (
            <div className="space-y-0">
              {sortedGroupKeys.map((key, groupIdx) => {
                const groupImgs  = groupedImages.get(key) ?? []
                const isLast     = groupIdx === sortedGroupKeys.length - 1

                return (
                  <div key={key}>
                    {/* 分组标题行 */}
                    <div className="flex items-center justify-between py-3">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium text-[var(--text-primary)]">
                          {getRoomLabel(key)}
                        </span>
                        <span
                          className="text-xs text-[var(--text-tertiary)] px-1.5 py-0.5
                                     rounded bg-[var(--bg-card)] border border-[var(--border-subtle)]"
                        >
                          {groupImgs.length} 张
                        </span>
                      </div>
                    </div>

                    {/* 图片横向列表 */}
                    {groupImgs.length === 0 ? (
                      <div
                        className="mb-1 py-5 text-center text-xs text-[var(--text-tertiary)]
                                   bg-[var(--bg-card)] rounded-xl border border-[var(--border-subtle)]"
                      >
                        该房间暂无图片
                      </div>
                    ) : (
                      <div
                        className="flex gap-3 overflow-x-auto pb-2
                                   [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
                      >
                        {groupImgs.map((img, idx) => (
                          <ImageCard
                            key={img.relationID}
                            image={img}
                            projectId={displayProject.projectID}
                            compact
                            isRemoving={removingId === img.relationID}
                            onClick={() => openLightbox(idx, groupImgs)}
                            onRemove={() => handleRemoveImage(img)}
                          />
                        ))}
                      </div>
                    )}

                    {/* 分割线 */}
                    {!isLast && (
                      <div className="border-b border-[var(--border-subtle)] my-3" />
                    )}
                  </div>
                )
              })}

              {/* 无任何分组时的空状态 */}
              {sortedGroupKeys.length === 0 && (
                <EmptyState message="该方案还没有图片" />
              )}
            </div>
          )}
        </div>
      </div>

      {/* ════ ImageLightbox ════ */}
      <ImageLightbox
        images={lbImages as ImageItem[]}
        currentIndex={lbIndex}
        onClose={() => setLbIndex(-1)}
        onIndexChange={handleLbIndexChange}
      />
    </div>,
    document.body,
  )
}
