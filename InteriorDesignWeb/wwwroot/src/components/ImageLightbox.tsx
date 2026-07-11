// src/components/ImageLightbox.tsx
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react'
import ReactDOM from 'react-dom'
import {
  X,
  ChevronLeft,
  ChevronRight,
  ImageOff,
  Sparkles,
  Image as ImageIcon,
  Maximize2,
  Scan,
  ZoomIn,
  ZoomOut,
} from 'lucide-react'

export type ImageItem = {
  id: string
  src?: string
  label?: string
  source?: 'ai' | 'custom' | 'default'
}

type ImageLightboxProps = {
  images: ImageItem[]
  currentIndex: number
  onClose: () => void
  onIndexChange: (i: number) => void
}

type PlaceholderBlockProps = {
  source?: 'ai' | 'custom' | 'default'
  className?: string
}

type SourceBadgeProps = {
  source?: 'ai' | 'custom' | 'default'
}

type LightboxImageProps = {
  item: ImageItem
  slideClass: string
}

type ArrowButtonProps = {
  direction: 'left' | 'right'
  disabled: boolean
  onClick: () => void
}

type ImageStatus = 'loading' | 'loaded' | 'error' | 'placeholder'

function PlaceholderBlock({
  source = 'default',
  className = '',
}: PlaceholderBlockProps) {
  const colorMap: Record<'ai' | 'custom' | 'default', string> = {
    ai: 'bg-[color:var(--color-accent)]/15',
    custom: 'bg-neutral-500/25',
    default: 'bg-neutral-700/50',
  }

  return <div className={`${colorMap[source]} ${className} rounded-xl`} />
}

function LegacySourceBadge({ source = 'default' }: SourceBadgeProps) {
  if (source === 'ai') {
    return (
      <span
        className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium
                   bg-[color:var(--color-accent-subtle)]
                   text-white border border-[color:var(--color-accent)]/30"
      >
        <Sparkles size={12} />
        AI 生成
      </span>
    )
  }

  return (
    <span
      className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium
                 bg-white/10 text-white/80 border border-white/10"
    >
      <ImageIcon size={12} />
      自定义
    </span>
  )
}

function LightboxImage({ item, slideClass }: LightboxImageProps) {
  const [status, setStatus] = useState<ImageStatus>(
    item.src ? 'loading' : 'placeholder',
  )

  useEffect(() => {
    setStatus(item.src ? 'loading' : 'placeholder')
  }, [item.id, item.src])

  return (
    <div
      className={`relative flex items-center justify-center
                  max-w-[90vw] max-h-[80vh] min-w-[260px] min-h-[180px]
                  transition-all duration-300 ease-out ${slideClass}`}
    >
      {status === 'placeholder' && (
        <PlaceholderBlock
          source={item.source}
          className="w-[min(960px,90vw)] h-[min(72vh,720px)]"
        />
      )}

      {status === 'loading' && (
        <div
          className="w-[min(960px,90vw)] h-[min(72vh,720px)] rounded-xl
                     bg-[color:var(--color-bg-tertiary)] animate-pulse"
        />
      )}

      {status === 'error' && (
        <div
          className="flex flex-col items-center justify-center gap-3
                     w-[min(960px,90vw)] h-[min(72vh,720px)] rounded-xl
                     bg-[color:var(--color-bg-tertiary)]
                     text-[color:var(--color-text-muted)]"
        >
          <ImageOff size={42} strokeWidth={1.6} />
          <span className="text-sm">图片加载失败</span>
        </div>
      )}

      {item.src && (
        <img
          key={item.src}
          src={item.src}
          alt={item.label ?? '预览图'}
          draggable={false}
          onLoad={() => setStatus('loaded')}
          onError={() => setStatus('error')}
          className={`max-w-[90vw] max-h-[80vh] object-contain rounded-xl select-none
                      shadow-[0_24px_80px_rgba(0,0,0,0.45)]
                      transition-opacity duration-200
                      ${status === 'loaded' ? 'opacity-100' : 'opacity-0 absolute inset-0'}`}
        />
      )}
    </div>
  )
}

function ArrowButton({ direction, disabled, onClick }: ArrowButtonProps) {
  const Icon = direction === 'left' ? ChevronLeft : ChevronRight
  const position = direction === 'left' ? 'left-3 md:left-6' : 'right-3 md:right-6'

  return (
    <button
      type="button"
      aria-label={direction === 'left' ? '上一张' : '下一张'}
      disabled={disabled}
      onClick={onClick}
      className={`absolute top-1/2 -translate-y-1/2 ${position}
                  z-10 h-11 w-11 md:h-12 md:w-12 rounded-full
                  flex items-center justify-center
                  border border-white/10 bg-black/35 backdrop-blur-md
                  text-white shadow-lg transition-all duration-150
                  ${
                    disabled
                      ? 'opacity-30 cursor-not-allowed'
                      : 'opacity-90 hover:opacity-100 hover:scale-105 hover:bg-black/50 active:scale-95 cursor-pointer'
                  }`}
    >
      <Icon size={20} strokeWidth={2} />
    </button>
  )
}

function LegacyImageLightbox({
  images,
  currentIndex,
  onClose,
  onIndexChange,
}: ImageLightboxProps) {
  const [visible, setVisible] = useState<boolean>(false)
  const [animateIn, setAnimateIn] = useState<boolean>(false)
  const [slideClass, setSlideClass] = useState<string>('')

  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const slideTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const prevIndexRef = useRef<number>(-1)

  const isOpen = currentIndex >= 0
  const total = images.length

  const safeIndex = useMemo(() => {
    if (!isOpen || total === 0) return 0
    return Math.min(Math.max(currentIndex, 0), total - 1)
  }, [currentIndex, isOpen, total])

  const currentItem = total > 0 ? images[safeIndex] : null
  const showArrows = total > 1

  const goPrev = useCallback(() => {
    if (safeIndex > 0) onIndexChange(safeIndex - 1)
  }, [safeIndex, onIndexChange])

  const goNext = useCallback(() => {
    if (safeIndex < total - 1) onIndexChange(safeIndex + 1)
  }, [safeIndex, total, onIndexChange])

  useEffect(() => {
    if (isOpen) {
      if (closeTimerRef.current) {
        clearTimeout(closeTimerRef.current)
        closeTimerRef.current = null
      }

      setVisible(true)
      document.body.style.overflow = 'hidden'

      const raf1 = requestAnimationFrame(() => {
        const raf2 = requestAnimationFrame(() => {
          setAnimateIn(true)
        })

        return () => cancelAnimationFrame(raf2)
      })

      return () => {
        cancelAnimationFrame(raf1)
      }
    }

    setAnimateIn(false)
    closeTimerRef.current = setTimeout(() => {
      setVisible(false)
      document.body.style.overflow = ''
    }, 250)

    return () => {
      if (closeTimerRef.current) {
        clearTimeout(closeTimerRef.current)
        closeTimerRef.current = null
      }
    }
  }, [isOpen])

  useEffect(() => {
    return () => {
      document.body.style.overflow = ''
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current)
      if (slideTimerRef.current) clearTimeout(slideTimerRef.current)
    }
  }, [])

  useEffect(() => {
    if (!isOpen) {
      prevIndexRef.current = -1
      setSlideClass('')
      return
    }

    const prevIndex = prevIndexRef.current

    if (prevIndex !== -1 && prevIndex !== currentIndex) {
      setSlideClass(currentIndex > prevIndex ? 'lb-slide-from-right' : 'lb-slide-from-left')

      if (slideTimerRef.current) {
        clearTimeout(slideTimerRef.current)
      }

      slideTimerRef.current = setTimeout(() => {
        setSlideClass('')
      }, 300)
    } else {
      setSlideClass('')
    }

    prevIndexRef.current = currentIndex

    return () => {
      if (slideTimerRef.current) {
        clearTimeout(slideTimerRef.current)
        slideTimerRef.current = null
      }
    }
  }, [currentIndex, isOpen])

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (!isOpen) return

      if (e.key === 'Escape') {
        onClose()
        return
      }

      if (e.key === 'ArrowLeft') {
        goPrev()
        return
      }

      if (e.key === 'ArrowRight') {
        goNext()
      }
    },
    [isOpen, onClose, goPrev, goNext],
  )

  useEffect(() => {
    if (!isOpen) return

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, handleKeyDown])

  const handleBackdropClick = (e: ReactMouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) {
      onClose()
    }
  }

  if (!visible) return null

  return ReactDOM.createPortal(
    <>
      <style>{`
        @keyframes lb-slide-from-right {
          from { transform: translateX(28px); opacity: 0; }
          to { transform: translateX(0); opacity: 1; }
        }

        @keyframes lb-slide-from-left {
          from { transform: translateX(-28px); opacity: 0; }
          to { transform: translateX(0); opacity: 1; }
        }

        .lb-slide-from-right {
          animation: lb-slide-from-right 280ms ease-out both;
        }

        .lb-slide-from-left {
          animation: lb-slide-from-left 280ms ease-out both;
        }
      `}</style>

      <div
        role="dialog"
        aria-modal="true"
        onClick={handleBackdropClick}
        className={`fixed inset-0 z-[9999] flex items-center justify-center
                    bg-black/85 backdrop-blur-sm
                    transition-opacity duration-[250ms] ease-out
                    ${animateIn ? 'opacity-100' : 'opacity-0'}`}
      >
        <div
          className={`relative w-full flex flex-col items-center px-4
                      transition-all duration-[250ms] ease-out
                      ${animateIn ? 'scale-100' : 'scale-[0.92]'}`}
          onClick={(e: ReactMouseEvent<HTMLDivElement>) => e.stopPropagation()}
        >
          <button
            type="button"
            aria-label="关闭"
            onClick={onClose}
            className="absolute -top-12 right-4 md:right-8 z-20
                       h-10 w-10 rounded-full
                       flex items-center justify-center
                       border border-white/10 bg-white/10 backdrop-blur-sm
                       text-white/80 hover:text-white hover:bg-white/15
                       transition-all duration-150 cursor-pointer"
          >
            <X size={18} strokeWidth={2} />
          </button>

          <div className="relative flex items-center justify-center w-full px-12 md:px-20">
            {showArrows && (
              <ArrowButton
                direction="left"
                disabled={safeIndex === 0}
                onClick={goPrev}
              />
            )}

            {currentItem && (
              <LightboxImage
                key={`${currentItem.id}-${safeIndex}`}
                item={currentItem}
                slideClass={slideClass}
              />
            )}

            {showArrows && (
              <ArrowButton
                direction="right"
                disabled={safeIndex === total - 1}
                onClick={goNext}
              />
            )}
          </div>

          <div className="mt-5 flex flex-col items-center gap-2 px-4">
            {currentItem?.label ? (
              <p className="max-w-[70vw] text-center text-sm font-medium text-white/90 truncate">
                {currentItem.label}
              </p>
            ) : null}

            <div className="flex items-center gap-3">
              <span className="text-xs text-white/50 tabular-nums">
                {total > 0 ? `${safeIndex + 1} / ${total}` : '0 / 0'}
              </span>
              <LegacySourceBadge source={currentItem?.source ?? 'default'} />
            </div>
          </div>
        </div>
      </div>
    </>,
    document.body,
  )
}

/*
// 接入示例：
//
// 1. 父组件维护两个状态：
//    const [lbImages, setLbImages] = useState<ImageItem[]>([])
//    const [lbIndex, setLbIndex] = useState<number>(-1)
//
// 2. 点击图片卡片时：
//    setLbImages(当前上下文图片列表)
//    setLbIndex(点击图片在列表中的索引)
//
// 3. JSX 中放置组件：
//    <ImageLightbox
//      images={lbImages}
//      currentIndex={lbIndex}
//      onClose={() => setLbIndex(-1)}
//      onIndexChange={setLbIndex}
//    />
*/
type Props = {
  images: ImageItem[]
  currentIndex: number
  onClose: () => void
  onIndexChange: (i: number) => void
}
type Point = { x: number; y: number }
type Metrics = { width: number; height: number; naturalWidth: number }
type Status = 'loading' | 'loaded' | 'error' | 'placeholder'

const MIN_SCALE = 0.25
const MAX_SCALE = 5
const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value))
const distance = (points: Point[]) => Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y)
const midpoint = (points: Point[]): Point => ({ x: (points[0].x + points[1].x) / 2, y: (points[0].y + points[1].y) / 2 })

function SourceBadge({ source = 'default' }: { source?: ImageItem['source'] }) {
  return source === 'ai' ? (
    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-[var(--accent)]/20 text-white border border-[var(--accent-border)]">
      <Sparkles size={12} />AI 生成
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-white/10 text-white/80 border border-white/10">
      <ImageIcon size={12} />自定义
    </span>
  )
}

function Arrow({ direction, disabled, onClick }: { direction: 'left' | 'right'; disabled: boolean; onClick: () => void }) {
  const Icon = direction === 'left' ? ChevronLeft : ChevronRight
  return (
    <button
      type="button"
      aria-label={direction === 'left' ? '上一张' : '下一张'}
      disabled={disabled}
      onClick={onClick}
      className={`absolute top-1/2 -translate-y-1/2 z-30 h-10 w-10 md:h-12 md:w-12 rounded-full flex items-center justify-center border border-white/10 bg-black/40 backdrop-blur-xl text-white shadow-xl transition-all duration-200 ${direction === 'left' ? 'left-3 md:left-6' : 'right-3 md:right-6'} ${disabled ? 'opacity-20 cursor-not-allowed' : 'opacity-80 hover:opacity-100 hover:scale-105 hover:bg-black/60 active:scale-95'}`}
    >
      <Icon size={20} />
    </button>
  )
}

function PreviewImage({ item, scale, position, interacting, onReady }: {
  item: ImageItem
  scale: number
  position: Point
  interacting: boolean
  onReady: (metrics: Metrics) => void
}) {
  const [status, setStatus] = useState<Status>(item.src ? 'loading' : 'placeholder')
  useEffect(() => setStatus(item.src ? 'loading' : 'placeholder'), [item.id, item.src])

  return (
    <div
      data-lightbox-content
      className="relative flex items-center justify-center min-w-[240px] min-h-[180px]"
    >
      {status === 'placeholder' || status === 'loading' ? (
        <div className="w-[min(960px,88vw)] h-[min(72vh,720px)] rounded-2xl bg-white/5 animate-pulse" />
      ) : null}
      {status === 'error' ? (
        <div className="flex flex-col items-center justify-center gap-3 w-[min(960px,88vw)] h-[min(72vh,720px)] rounded-2xl bg-white/5 text-white/45">
          <ImageOff size={42} strokeWidth={1.5} /><span className="text-sm">图片加载失败</span>
        </div>
      ) : null}
      {item.src ? (
        <img
          key={item.src}
          src={item.src}
          alt={item.label ?? '预览图'}
          draggable={false}
          onLoad={(event) => {
            const image = event.currentTarget
            setStatus('loaded')
            onReady({ width: image.clientWidth, height: image.clientHeight, naturalWidth: image.naturalWidth })
          }}
          onError={() => setStatus('error')}
          style={{
            transform: `translate3d(${position.x}px, ${position.y}px, 0) scale(${scale})`,
            transformOrigin: 'center',
            transition: interacting ? 'none' : 'transform 160ms cubic-bezier(0.22,1,0.36,1), opacity 180ms ease',
          }}
          className={`max-w-[88vw] md:max-w-[84vw] max-h-[72vh] md:max-h-[78vh] object-contain rounded-xl select-none will-change-transform shadow-[0_14px_44px_rgba(0,0,0,0.42)] ${status === 'loaded' ? 'opacity-100' : 'opacity-0 absolute inset-0'}`}
        />
      ) : null}
    </div>
  )
}

export default function ImageLightbox({ images, currentIndex, onClose, onIndexChange }: Props) {
  const [visible, setVisible] = useState(false)
  const [animateIn, setAnimateIn] = useState(false)
  const [scale, setScale] = useState(1)
  const [position, setPosition] = useState<Point>({ x: 0, y: 0 })
  const [metrics, setMetrics] = useState<Metrics | null>(null)
  const [interacting, setInteracting] = useState(false)
  const viewportRef = useRef<HTMLDivElement>(null)
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pointersRef = useRef(new Map<number, Point>())
  const clickGuardRef = useRef({
    startPoint: { x: 0, y: 0 },
    startedOnContent: false,
    moved: false,
  })
  const gestureRef = useRef({
    mode: 'none' as 'none' | 'drag' | 'pinch',
    startPoint: { x: 0, y: 0 }, startPosition: { x: 0, y: 0 },
    startScale: 1, startDistance: 1, startMidpoint: { x: 0, y: 0 },
  })

  const isOpen = currentIndex >= 0
  const total = images.length
  const safeIndex = useMemo(() => total ? clamp(currentIndex, 0, total - 1) : 0, [currentIndex, total])
  const currentItem = total ? images[safeIndex] : null

  const constrain = useCallback((point: Point, nextScale: number): Point => {
    const viewport = viewportRef.current
    if (!viewport || !metrics) return nextScale <= 1 ? { x: 0, y: 0 } : point
    const maxX = Math.max(0, (metrics.width * nextScale - viewport.clientWidth + 32) / 2)
    const maxY = Math.max(0, (metrics.height * nextScale - viewport.clientHeight + 32) / 2)
    return { x: clamp(point.x, -maxX, maxX), y: clamp(point.y, -maxY, maxY) }
  }, [metrics])

  const zoomAround = useCallback((requested: number, anchor: Point = { x: 0, y: 0 }) => {
    const next = clamp(requested, MIN_SCALE, MAX_SCALE)
    const ratio = next / scale
    const moved = {
      x: anchor.x - (anchor.x - position.x) * ratio,
      y: anchor.y - (anchor.y - position.y) * ratio,
    }
    setScale(next)
    setPosition(constrain(moved, next))
  }, [constrain, position, scale])

  const fit = useCallback(() => { setScale(1); setPosition({ x: 0, y: 0 }) }, [])
  const actual = useCallback(() => {
    if (!metrics) return
    setScale(clamp(metrics.naturalWidth / Math.max(1, metrics.width), 1, MAX_SCALE))
    setPosition({ x: 0, y: 0 })
  }, [metrics])
  const goPrev = useCallback(() => { if (safeIndex > 0) onIndexChange(safeIndex - 1) }, [onIndexChange, safeIndex])
  const goNext = useCallback(() => { if (safeIndex < total - 1) onIndexChange(safeIndex + 1) }, [onIndexChange, safeIndex, total])

  useEffect(() => { fit(); setMetrics(null); pointersRef.current.clear() }, [currentItem?.id, fit])
  useEffect(() => {
    if (isOpen) {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current)
      setVisible(true)
      document.body.style.overflow = 'hidden'
      requestAnimationFrame(() => requestAnimationFrame(() => setAnimateIn(true)))
    } else {
      setAnimateIn(false)
      closeTimerRef.current = setTimeout(() => { setVisible(false); document.body.style.overflow = '' }, 220)
    }
  }, [isOpen])
  useEffect(() => () => { document.body.style.overflow = ''; if (closeTimerRef.current) clearTimeout(closeTimerRef.current) }, [])

  const keyHandler = useCallback((event: KeyboardEvent) => {
    if (!isOpen) return
    if (event.key === 'Escape') onClose()
    else if (event.key === 'ArrowLeft') goPrev()
    else if (event.key === 'ArrowRight') goNext()
    else if (event.key === '+' || event.key === '=') zoomAround(scale + 0.25)
    else if (event.key === '-') zoomAround(scale - 0.25)
    else if (event.key === '0' || event.key.toLowerCase() === 'f') fit()
    else if (event.key === '1') actual()
  }, [actual, fit, goNext, goPrev, isOpen, onClose, scale, zoomAround])
  useEffect(() => {
    if (!isOpen) return
    window.addEventListener('keydown', keyHandler)
    return () => window.removeEventListener('keydown', keyHandler)
  }, [isOpen, keyHandler])

  useEffect(() => {
    const viewport = viewportRef.current
    if (!isOpen || !visible || !viewport) return

    const handleWheel = (event: WheelEvent) => {
      event.preventDefault()
      const rect = viewport.getBoundingClientRect()
      zoomAround(scale * Math.exp(-event.deltaY * 0.0015), {
        x: event.clientX - rect.left - rect.width / 2,
        y: event.clientY - rect.top - rect.height / 2,
      })
    }

    viewport.addEventListener('wheel', handleWheel, { passive: false })
    return () => viewport.removeEventListener('wheel', handleWheel)
  }, [isOpen, scale, visible, zoomAround])

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if ((event.target as HTMLElement).closest('button')) return
    const target = event.target as HTMLElement
    clickGuardRef.current = {
      startPoint: { x: event.clientX, y: event.clientY },
      startedOnContent: Boolean(target.closest('[data-lightbox-content]')),
      moved: false,
    }
    event.currentTarget.setPointerCapture(event.pointerId)
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    const points = Array.from(pointersRef.current.values())
    setInteracting(true)
    gestureRef.current = points.length >= 2 ? {
      mode: 'pinch', startPoint: points[0], startPosition: position, startScale: scale,
      startDistance: Math.max(1, distance(points.slice(0, 2))), startMidpoint: midpoint(points.slice(0, 2)),
    } : { ...gestureRef.current, mode: 'drag', startPoint: points[0], startPosition: position, startScale: scale }
  }
  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!pointersRef.current.has(event.pointerId)) return
    const clickGuard = clickGuardRef.current
    if (Math.hypot(
      event.clientX - clickGuard.startPoint.x,
      event.clientY - clickGuard.startPoint.y,
    ) > 5) {
      clickGuard.moved = true
    }
    pointersRef.current.set(event.pointerId, { x: event.clientX, y: event.clientY })
    const points = Array.from(pointersRef.current.values())
    const gesture = gestureRef.current
    if (points.length >= 2 && gesture.mode === 'pinch') {
      const pair = points.slice(0, 2)
      const next = clamp(gesture.startScale * distance(pair) / gesture.startDistance, MIN_SCALE, MAX_SCALE)
      const ratio = next / gesture.startScale
      const rect = viewportRef.current?.getBoundingClientRect()
      const center = rect ? { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 } : { x: 0, y: 0 }
      const start = { x: gesture.startMidpoint.x - center.x, y: gesture.startMidpoint.y - center.y }
      const current = midpoint(pair)
      const anchor = { x: current.x - center.x, y: current.y - center.y }
      setScale(next)
      setPosition(constrain({
        x: anchor.x - (start.x - gesture.startPosition.x) * ratio,
        y: anchor.y - (start.y - gesture.startPosition.y) * ratio,
      }, next))
    } else if (points.length === 1 && gesture.mode === 'drag') {
      setPosition(constrain({
        x: gesture.startPosition.x + points[0].x - gesture.startPoint.x,
        y: gesture.startPosition.y + points[0].y - gesture.startPoint.y,
      }, scale))
    }
  }
  const onPointerEnd = (event: ReactPointerEvent<HTMLDivElement>) => {
    pointersRef.current.delete(event.pointerId)
    const points = Array.from(pointersRef.current.values())
    if (points.length === 1) {
      gestureRef.current = { ...gestureRef.current, mode: 'drag', startPoint: points[0], startPosition: position, startScale: scale }
    } else if (!points.length) {
      gestureRef.current.mode = 'none'
      setInteracting(false)
      setPosition((current) => constrain(current, scale))
    }
  }
  const onDoubleClick = (event: ReactMouseEvent<HTMLDivElement>) => {
    if (scale > 1.05) return fit()
    const rect = viewportRef.current?.getBoundingClientRect()
    if (rect) zoomAround(2, { x: event.clientX - rect.left - rect.width / 2, y: event.clientY - rect.top - rect.height / 2 })
  }
  const onViewportClick = (event: ReactMouseEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement
    const clickGuard = clickGuardRef.current
    const isContent = Boolean(target.closest('[data-lightbox-content], button'))
    const shouldClose = !clickGuard.moved
      && !clickGuard.startedOnContent
      && !isContent

    clickGuardRef.current = {
      startPoint: { x: 0, y: 0 },
      startedOnContent: false,
      moved: false,
    }

    if (shouldClose) onClose()
  }

  if (!visible) return null
  return ReactDOM.createPortal(
    <div role="dialog" aria-modal="true" className={`fixed inset-0 z-[9999] bg-[#05070b]/96 transition-opacity duration-150 ${animateIn ? 'opacity-100' : 'opacity-0'}`}>
      <div className="absolute inset-x-0 top-0 z-40 h-16 px-3 md:px-5 flex items-center justify-between bg-gradient-to-b from-black/55 to-transparent pointer-events-none">
        <div className="min-w-0 max-w-[38vw] text-sm text-white/80 truncate pointer-events-auto">{currentItem?.label ?? ''}</div>
        <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-1 p-1 rounded-xl border border-white/10 bg-black/75 shadow-lg pointer-events-auto">
          <button type="button" aria-label="缩小" onClick={() => zoomAround(scale - 0.25)} className="lb-tool"><ZoomOut size={15} /></button>
          <button type="button" title="适应窗口" onClick={fit} className="h-8 min-w-14 px-2 rounded-lg text-xs text-white/80 hover:bg-white/10 tabular-nums">{Math.round(scale * 100)}%</button>
          <button type="button" aria-label="放大" onClick={() => zoomAround(scale + 0.25)} className="lb-tool"><ZoomIn size={15} /></button>
          <span className="w-px h-4 bg-white/10 mx-0.5" />
          <button type="button" title="适应窗口（0）" onClick={fit} className="lb-tool"><Maximize2 size={15} /></button>
          <button type="button" title="原始尺寸（1）" onClick={actual} className="lb-tool hidden sm:flex"><Scan size={15} /></button>
        </div>
        <button type="button" aria-label="关闭" onClick={onClose} className="lb-tool !w-9 !h-9 pointer-events-auto"><X size={18} /></button>
      </div>
      <div
        ref={viewportRef}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerEnd}
        onPointerCancel={onPointerEnd}
        onDoubleClick={onDoubleClick}
        onClick={onViewportClick}
        className={`absolute inset-0 flex items-center justify-center overflow-hidden touch-none ${scale > 1 ? (interacting ? 'cursor-grabbing' : 'cursor-grab') : 'cursor-zoom-in'}`}
      >
        {total > 1 ? <Arrow direction="left" disabled={safeIndex === 0} onClick={goPrev} /> : null}
        {currentItem ? <PreviewImage item={currentItem} scale={scale} position={position} interacting={interacting} onReady={setMetrics} /> : null}
        {total > 1 ? <Arrow direction="right" disabled={safeIndex === total - 1} onClick={goNext} /> : null}
      </div>
      <div className="absolute inset-x-0 bottom-0 z-40 px-4 pb-[max(16px,env(safe-area-inset-bottom))] pt-12 flex justify-center bg-gradient-to-t from-black/60 to-transparent pointer-events-none">
        <div className="flex items-center gap-3 pointer-events-auto">
          <span className="text-xs text-white/55 tabular-nums">{total ? `${safeIndex + 1} / ${total}` : '0 / 0'}</span>
          <SourceBadge source={currentItem?.source} />
          <span className="hidden md:inline text-[11px] text-white/35">滚轮缩放 · 拖动查看 · 双击复位</span>
        </div>
      </div>
      <style>{`.lb-tool{width:32px;height:32px;display:flex;align-items:center;justify-content:center;border-radius:8px;color:rgba(255,255,255,.78);transition:color 160ms ease,background-color 160ms ease,transform 160ms ease}.lb-tool:hover{color:white;background:rgba(255,255,255,.1)}.lb-tool:active{transform:scale(.94)}`}</style>
    </div>,
    document.body,
  )
}
