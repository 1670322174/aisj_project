// 作用：提供工作流图片输入槽，支持本地文件、拖拽和资产库选择。
import { useRef, useState, type ChangeEvent, type DragEvent } from 'react'
import { FolderOpen, ImagePlus, RotateCcw, Trash2, Upload } from 'lucide-react'
import type { GenerationAsset } from '@/features/ai/types'
import { cn } from '@/utils/cn'

type Props = {
  label: string
  description?: string
  value: GenerationAsset | null
  required?: boolean
  disabled?: boolean
  onChange: (asset: GenerationAsset | null) => void
  onOpenLibrary: () => void
}

const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const MAX_FILE_BYTES = 50 * 1024 * 1024

export default function ImageInputField({
  label,
  description,
  value,
  required = false,
  disabled = false,
  onChange,
  onOpenLibrary,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragOver, setDragOver] = useState(false)
  const [error, setError] = useState('')

  function selectFile(file?: File) {
    if (!file) return
    setError('')

    if (!ACCEPTED_TYPES.includes(file.type)) {
      setError('仅支持 JPG、PNG、WEBP 图片')
      return
    }
    if (file.size > MAX_FILE_BYTES) {
      setError('图片不能超过 50MB')
      return
    }

    const previewUrl = URL.createObjectURL(file)
    onChange({
      source: 'local',
      id: `${file.name}-${file.lastModified}`,
      label: file.name,
      previewUrl,
      file,
    })
  }

  function handleInput(event: ChangeEvent<HTMLInputElement>) {
    selectFile(event.target.files?.[0])
    event.target.value = ''
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setDragOver(false)
    if (disabled) return
    selectFile(event.dataTransfer.files?.[0])
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-2">
        <label className="text-xs font-medium text-[var(--text-secondary)]">
          {label}{required ? <span className="text-red-400 ml-1">*</span> : null}
        </label>
        {value ? <span className="text-[10px] text-[var(--text-tertiary)]">{sourceLabel(value.source)}</span> : null}
      </div>

      {value ? (
        <div className="relative rounded-xl overflow-hidden border border-[var(--border-default)] bg-[var(--bg-input)]">
          <div className="aspect-[16/10] bg-black/20">
            <img src={value.previewUrl} alt={value.label} className="w-full h-full object-cover" />
          </div>
          <div className="absolute inset-x-0 bottom-0 p-2 bg-gradient-to-t from-black/80 to-transparent flex items-end justify-between gap-2">
            <p className="text-[11px] text-white truncate">{value.label}</p>
            <div className="flex gap-1 shrink-0">
              <button
                type="button"
                disabled={disabled}
                onClick={() => inputRef.current?.click()}
                className="p-1.5 rounded-md bg-black/40 text-white hover:bg-black/60"
                title="重新选择本地图片"
              >
                <RotateCcw size={12} />
              </button>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onChange(null)}
                className="p-1.5 rounded-md bg-black/40 text-white hover:bg-red-500/70"
                title="移除图片"
              >
                <Trash2 size={12} />
              </button>
            </div>
          </div>
        </div>
      ) : (
        <div
          className={cn(
            'rounded-xl border-2 border-dashed p-5 text-center transition-colors',
            dragOver
              ? 'border-[var(--accent-border)] bg-[var(--accent-glow)]'
              : 'border-[var(--border-default)] bg-[var(--bg-input)]',
            disabled ? 'opacity-60' : 'hover:border-[var(--border-strong)]',
          )}
          onDragOver={(event) => {
            event.preventDefault()
            if (!disabled) setDragOver(true)
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
        >
          <ImagePlus size={20} className="mx-auto text-[var(--text-tertiary)]" />
          <p className="text-xs text-[var(--text-secondary)] mt-2">拖拽图片到这里</p>
          <p className="text-[10px] text-[var(--text-tertiary)] mt-1">保持原图，不压缩、不裁剪</p>
          <div className="flex justify-center gap-2 mt-3">
            <button
              type="button"
              disabled={disabled}
              onClick={() => inputRef.current?.click()}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] border border-[var(--border-default)] text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
            >
              <Upload size={12} /> 本地上传
            </button>
            <button
              type="button"
              disabled={disabled}
              onClick={onOpenLibrary}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] border border-[var(--border-default)] text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
            >
              <FolderOpen size={12} /> 资产库选择
            </button>
          </div>
        </div>
      )}

      <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={handleInput} />
      {description ? <p className="text-[10px] text-[var(--text-tertiary)]">{description}</p> : null}
      {error ? <p className="text-[11px] text-red-400">{error}</p> : null}
    </div>
  )
}

function sourceLabel(source: GenerationAsset['source']): string {
  if (source === 'gallery') return '图库'
  if (source === 'project') return '项目图片'
  if (source === 'history') return '历史生成'
  return '本地文件'
}
