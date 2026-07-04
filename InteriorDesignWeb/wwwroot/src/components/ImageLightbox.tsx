// src/components/ImageLightbox.tsx
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
} from 'react'
import ReactDOM from 'react-dom'
import {
  X,
  ChevronLeft,
  ChevronRight,
  ImageOff,
  Sparkles,
  Image as ImageIcon,
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

function SourceBadge({ source = 'default' }: SourceBadgeProps) {
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

export default function ImageLightbox({
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
              <SourceBadge source={currentItem?.source ?? 'default'} />
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