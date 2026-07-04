// src/pages/app/GeneratePage.tsx
import { useState } from 'react'
import { Wand2, Upload, ImageIcon, RefreshCw, Sparkles, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { cn } from '@/utils/cn'
import ImageLightbox, { ImageItem } from '@/components/ImageLightbox'

/* ─────────────────────────────────────────
   类型定义
───────────────────────────────────────── */
type TabKey = 'text' | 'image'

type ModelItem = {
  id: string
  name: string
  description: string
  badge?: string
}

type MockResult = {
  id: number
  hue: string
  label: string
}

/* ─────────────────────────────────────────
   常量
───────────────────────────────────────── */
const STYLE_TAGS = ['北欧极简', '日式侘寂', '法式轻奢', '工业复古', '现代简约', '美式乡村']

const MODELS: Record<TabKey, ModelItem[]> = {
  text: [
    {
      id: 'general',
      name: '通用模型',
      description: '适合各类室内场景，均衡输出质量与速度',
    },
  ],
  image: [
    {
      id: 'rough-to-fine',
      name: '毛坯房转精装',
      description: '将毛坯房照片一键生成精装修效果图',
    },
    {
      id: 'furniture-swap',
      name: '换家居软装',
      description: '替换家具与软装，快速预览不同搭配',
      badge: '待定',
    },
    {
      id: 'outpainting',
      name: 'AI 扩图',
      description: '智能扩展画面边界，延伸空间视野',
    },
    {
      id: 'sketch-to-render',
      name: '手稿转效果图',
      description: '将设计手稿或线稿渲染为真实效果图',
    },
    {
      id: 'lighting-transfer',
      name: '效果图灯光转换',
      description: '调整场景灯光氛围，白天 / 夜晚自由切换',
    },
  ],
}

const MOCK_RESULTS: MockResult[] = [
  { id: 1, hue: '220 18% 14%', label: '北欧风客厅方案 A' },
  { id: 2, hue: '200 15% 13%', label: '北欧风客厅方案 B' },
  { id: 3, hue: '210 20% 16%', label: '北欧风客厅方案 C' },
  { id: 4, hue: '215 16% 12%', label: '北欧风客厅方案 D' },
]

/* ─────────────────────────────────────────
   子组件：模型选择卡片
───────────────────────────────────────── */
type ModelCardProps = {
  model: ModelItem
  selected: boolean
  onClick: () => void
}

function ModelCard({ model, selected, onClick }: ModelCardProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'w-full text-left px-3 py-2.5 rounded-xl border transition-all duration-200',
        'flex items-start gap-2.5',
        selected
          ? 'border-[var(--accent-border)] bg-[var(--accent-glow)] shadow-[0_0_0_1px_var(--accent-border)]'
          : 'border-[var(--border-default)] bg-[var(--bg-input)] hover:border-[var(--border-strong)]',
      )}
    >
      {/* 选中指示点 */}
      <div
        className={cn(
          'mt-0.5 w-3.5 h-3.5 shrink-0 rounded-full border-2 transition-all duration-200',
          selected
            ? 'border-[var(--accent-border)] bg-[var(--accent-border)]'
            : 'border-[var(--border-strong)] bg-transparent',
        )}
      />

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span
            className={cn(
              'text-xs font-semibold leading-snug',
              selected ? 'text-[var(--text-primary)]' : 'text-[var(--text-secondary)]',
            )}
          >
            {model.name}
          </span>
          {model.badge && (
            <span
              className="inline-flex items-center px-1.5 py-0.5 rounded text-[9px] font-medium
                         bg-[var(--border-subtle)] text-[var(--text-tertiary)]
                         border border-[var(--border-default)]"
            >
              {model.badge}
            </span>
          )}
        </div>
        <p className="text-[11px] text-[var(--text-tertiary)] mt-0.5 leading-snug">
          {model.description}
        </p>
      </div>

      <ChevronRight
        size={14}
        className={cn(
          'shrink-0 mt-0.5 transition-opacity duration-200',
          selected ? 'text-[var(--accent-border)] opacity-100' : 'opacity-0',
        )}
      />
    </button>
  )
}

/* ─────────────────────────────────────────
   子组件：结果图片卡片
───────────────────────────────────────── */
/* ─────────────────────────────────────────
   子组件：结果图片卡片（DEBUG 版）
───────────────────────────────────────── */
type ResultCardProps = {
  result: MockResult
  onClick: () => void
  onRegenerate: () => void
}

function ResultCard({ result, onClick, onRegenerate }: ResultCardProps) {
  return (
    <div
      onClick={() => {
        console.log('[ResultCard] 外层 div 点击触发，result.id =', result.id)
        onClick()
      }}
      className="group relative rounded-2xl overflow-hidden border border-[var(--border-subtle)]
                 cursor-pointer transition-all duration-300
                 hover:border-[var(--border-strong)]
                 hover:shadow-[0_8px_32px_rgba(0,0,0,0.3)]
                 hover:-translate-y-0.5"
      style={{ aspectRatio: '4/3' }}
    >
      {/* 色块背景 */}
      <div
        className="absolute inset-0"
        style={{ background: `hsl(${result.hue})` }}
      >
        <div className="absolute inset-0 flex flex-col justify-between p-4">
          <div className="flex justify-end">
            <div className="w-12 h-1 rounded opacity-20 bg-white" />
          </div>
          <div className="flex flex-col gap-2">
            <div className="h-8 rounded-lg opacity-20 bg-white" />
            <div className="flex gap-2">
              <div className="flex-1 h-16 rounded-lg opacity-15 bg-white" />
              <div className="w-1/3 h-16 rounded-lg opacity-10 bg-white" />
            </div>
          </div>
        </div>
        <div className="absolute inset-0 bg-gradient-to-br from-transparent to-black/20" />
      </div>

      {/* AI 徽章 */}
      <div className="absolute top-2 left-2 z-10">
        <span
          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold
                     bg-[color:var(--color-accent)]/20 text-white
                     border border-white/20 backdrop-blur-sm"
        >
          <Sparkles size={9} />
          AI
        </span>
      </div>

      {/* 方案编号徽章 */}
      <div
        className="absolute top-2 right-2 z-10
                   w-5 h-5 rounded-full bg-black/40 backdrop-blur-sm
                   border border-white/20 flex items-center justify-center"
      >
        <span className="text-[10px] text-white font-medium">{result.id}</span>
      </div>

      {/* ⚠️ 关键：Hover 遮罩层挂载了 onClick + stopPropagation
          这里改为：遮罩层本身不阻止冒泡，只有操作按钮才 stopPropagation */}
      <div
        className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100
                   transition-all duration-300 flex flex-col justify-end p-3 z-20"
        onClick={() => {
          console.log('[ResultCard] 遮罩层 onClick 触发，准备调用 onClick()')
          // ⚠️ 不再 stopPropagation，让事件继续冒泡到外层 div
          onClick()
        }}
      >
        <p className="text-xs font-medium text-white mb-2">{result.label}</p>
        <div className="flex gap-2">
          <button
            className="flex-1 py-1.5 text-xs rounded-lg
                       bg-white/15 backdrop-blur-sm border border-white/20
                       text-white hover:bg-white/25 transition-colors"
            onClick={(e) => {
              console.log('[ResultCard] 下载按钮点击')
              e.stopPropagation()
              // TODO: 下载图片
            }}
          >
            下载
          </button>
          <button
            className="flex-1 py-1.5 text-xs rounded-lg
                       bg-white/15 backdrop-blur-sm border border-white/20
                       text-white hover:bg-white/25 transition-colors"
            onClick={(e) => {
              console.log('[ResultCard] 收藏按钮点击')
              e.stopPropagation()
              // TODO: 收藏图片
            }}
          >
            收藏
          </button>
          <button
            className="flex-1 py-1.5 text-xs rounded-lg
                       bg-white/15 backdrop-blur-sm border border-white/20
                       text-white hover:bg-white/25 transition-colors"
            onClick={(e) => {
              console.log('[ResultCard] 重生成按钮点击')
              e.stopPropagation()
              onRegenerate()
            }}
          >
            重生成
          </button>
        </div>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   主页面
───────────────────────────────────────── */
export default function GeneratePage() {
  /* ── 表单状态 ── */
  const [tab, setTab] = useState<TabKey>('text')
  const [prompt, setPrompt] = useState<string>('')
  const [selectedStyles, setSelectedStyles] = useState<string[]>([])
  const [selectedModel, setSelectedModel] = useState<string>('general')
  const [loading, setLoading] = useState<boolean>(false)
  const [results, setResults] = useState<MockResult[]>([])
  const [dragOver, setDragOver] = useState<boolean>(false)

  /* ── Lightbox 状态 ── */
  const [lbIndex, setLbIndex] = useState<number>(-1)
  const [lbImages, setLbImages] = useState<ImageItem[]>([])

  /* ── 切换 Tab 时重置模型选择 ── */
  const handleTabChange = (key: TabKey) => {
    setTab(key)
    setSelectedModel(MODELS[key][0].id)
  }

  /* ── 风格标签多选 ── */
  const toggleStyle = (style: string) => {
    setSelectedStyles((prev) =>
      prev.includes(style) ? prev.filter((s) => s !== style) : [...prev, style],
    )
  }

  /* ── 生成（Mock） ── */
  const handleGenerate = async () => {
    if (tab === 'text' && !prompt.trim()) return
    setLoading(true)
    setResults([])
    // TODO: 调用生成接口，传入 { tab, prompt, selectedStyles, selectedModel }
    await new Promise((r) => setTimeout(r, 2200))
    setResults(MOCK_RESULTS)
    setLoading(false)
  }

  /* ── 打开 Lightbox ── */
  const openLightbox = (clickedIndex: number) => {
    const currentModelName =
      MODELS[tab].find((m) => m.id === selectedModel)?.name ?? '生成结果'

    setLbImages(
      results.map((result) => ({
        id: String(result.id),
        src: undefined,             // Mock 阶段无真实图片
        label: `${currentModelName} · ${result.label}`,
        source: 'ai' as const,
      })),
    )
    setLbIndex(clickedIndex)
  }

  /* ── 当前 Tab 下的模型列表 ── */
  const currentModels = MODELS[tab]

  return (
    <div className="h-full flex flex-col">
      <div className="flex-1 flex overflow-hidden">

        {/* ════════ 左侧控制区 ════════ */}
        <div className="w-80 shrink-0 flex flex-col border-r border-[var(--border-subtle)] overflow-y-auto">

          {/* Tab 切换 */}
          <div className="p-4 border-b border-[var(--border-subtle)]">
            <div className="flex rounded-xl bg-[var(--bg-input)] border border-[var(--border-subtle)] p-1">
              {(
                [
                  { key: 'text' as const, icon: Wand2, label: '文生图' },
                  { key: 'image' as const, icon: ImageIcon, label: '图生图' },
                ]
              ).map((t) => {
                const Icon = t.icon
                return (
                  <button
                    key={t.key}
                    type="button"
                    onClick={() => handleTabChange(t.key)}
                    className={cn(
                      'flex-1 flex items-center justify-center gap-1.5 py-1.5 text-sm rounded-lg transition-all duration-200',
                      tab === t.key
                        ? 'bg-[var(--bg-card)] text-[var(--text-primary)] border border-[var(--border-subtle)] shadow-sm'
                        : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)]',
                    )}
                  >
                    <Icon size={13} />
                    {t.label}
                  </button>
                )
              })}
            </div>
          </div>

          <div className="flex-1 flex flex-col gap-5 p-4">

            {/* 输入区 */}
            {tab === 'text' ? (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)] tracking-wide">
                  描述你的空间
                </label>
                <textarea
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="例如：北欧极简风格客厅，大落地窗，原木色调，白色沙发，自然光线充足..."
                  rows={5}
                  className={cn(
                    'w-full rounded-xl text-sm resize-none p-3',
                    'bg-[var(--bg-input)] border border-[var(--border-default)]',
                    'text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)]',
                    'focus:outline-none focus:border-[var(--accent-border)] focus:ring-1 focus:ring-[var(--accent-glow)]',
                    'transition-all duration-200',
                  )}
                />
                <div className="flex justify-between text-[10px] text-[var(--text-tertiary)]">
                  <span>支持中英文描述</span>
                  <span>{prompt.length}/500</span>
                </div>
              </div>
            ) : (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)] tracking-wide">
                  上传参考图片
                </label>
                <div
                  className={cn(
                    'relative rounded-xl border-2 border-dashed p-6 text-center transition-all duration-200 cursor-pointer',
                    dragOver
                      ? 'border-[var(--accent-border)] bg-[var(--accent-glow)]'
                      : 'border-[var(--border-default)] hover:border-[var(--border-strong)] bg-[var(--bg-input)]',
                  )}
                  onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
                  onDragLeave={() => setDragOver(false)}
                  onDrop={(e) => {
                    e.preventDefault()
                    setDragOver(false)
                    // TODO: 处理图片上传
                  }}
                >
                  <div className="flex flex-col items-center gap-2">
                    <div
                      className="w-10 h-10 rounded-xl bg-[var(--bg-card)] border border-[var(--border-subtle)]
                                 flex items-center justify-center"
                    >
                      <Upload size={16} className="text-[var(--text-secondary)]" />
                    </div>
                    <div>
                      <p className="text-sm text-[var(--text-secondary)]">拖拽或点击上传</p>
                      <p className="text-xs text-[var(--text-tertiary)] mt-0.5">支持 JPG、PNG，最大 10MB</p>
                    </div>
                  </div>
                  <input
                    type="file"
                    className="absolute inset-0 opacity-0 cursor-pointer"
                    accept="image/*"
                    onChange={() => {
                      // TODO: 处理图片上传
                    }}
                  />
                </div>
                <textarea
                  placeholder="可选：补充描述调整方向..."
                  rows={3}
                  className={cn(
                    'w-full rounded-xl text-sm resize-none p-3 mt-1',
                    'bg-[var(--bg-input)] border border-[var(--border-default)]',
                    'text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)]',
                    'focus:outline-none focus:border-[var(--accent-border)] focus:ring-1 focus:ring-[var(--accent-glow)]',
                    'transition-all duration-200',
                  )}
                />
              </div>
            )}

            {/* ── 模型选择 ── */}
            <div className="flex flex-col gap-2">
              <label className="text-xs font-medium text-[var(--text-secondary)] tracking-wide">
                选择模型
                <span className="ml-1.5 text-[var(--text-tertiary)] font-normal">
                  ({currentModels.length} 个可用)
                </span>
              </label>
              <div className="flex flex-col gap-2">
                {currentModels.map((model) => (
                  <ModelCard
                    key={model.id}
                    model={model}
                    selected={selectedModel === model.id}
                    onClick={() => setSelectedModel(model.id)}
                  />
                ))}
              </div>
            </div>

            {/* ── 风格标签 ── */}
            <div className="flex flex-col gap-2">
              <label className="text-xs font-medium text-[var(--text-secondary)] tracking-wide">
                风格标签{' '}
                <span className="text-[var(--text-tertiary)] font-normal">(可多选)</span>
              </label>
              <div className="flex flex-wrap gap-2">
                {STYLE_TAGS.map((style) => (
                  <button
                    key={style}
                    type="button"
                    onClick={() => toggleStyle(style)}
                    className={cn(
                      'px-3 py-1 text-xs rounded-full border transition-all duration-200',
                      selectedStyles.includes(style)
                        ? 'border-[var(--accent-border)] bg-[var(--accent-glow)] text-[var(--text-primary)] shadow-[0_0_8px_var(--accent-glow)]'
                        : 'border-[var(--border-default)] text-[var(--text-secondary)] hover:border-[var(--border-strong)] hover:text-[var(--text-primary)] bg-[var(--bg-input)]',
                    )}
                  >
                    {style}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* 生成按钮 */}
          <div className="p-4 border-t border-[var(--border-subtle)]">
            <Button
              variant="primary"
              size="lg"
              className="w-full text-base"
              onClick={handleGenerate}
              disabled={loading || (tab === 'text' && !prompt.trim())}
            >
              {loading ? (
                <>
                  <RefreshCw size={15} className="animate-spin" />
                  生成中...
                </>
              ) : (
                <>
                  <Sparkles size={15} />
                  开始生成
                </>
              )}
            </Button>
            {tab === 'text' && !prompt.trim() && (
              <p className="text-center text-xs text-[var(--text-tertiary)] mt-2">
                请先输入设计描述
              </p>
            )}
          </div>
        </div>

        {/* ════════ 右侧结果区 ════════ */}
        <div className="flex-1 flex flex-col overflow-hidden">

          {/* 生成中 */}
          {loading && (
            <div className="flex-1 flex flex-col items-center justify-center gap-4">
              <div className="relative w-16 h-16">
                <div className="absolute inset-0 rounded-full border-2 border-[var(--border-subtle)]" />
                <div className="absolute inset-0 rounded-full border-t-2 border-[var(--accent)] animate-spin" />
                <div
                  className="absolute inset-2 rounded-full border-t border-[var(--accent)] animate-spin"
                  style={{ animationDirection: 'reverse', animationDuration: '1.5s' }}
                />
              </div>
              <div className="text-center">
                <p className="text-sm font-medium text-[var(--text-primary)]">AI 正在创作中</p>
                <p className="text-xs text-[var(--text-tertiary)] mt-1">通常需要 2-5 秒</p>
              </div>
            </div>
          )}

          {/* 有结果 */}
          {!loading && results.length > 0 && (
            <div className="flex-1 p-6 overflow-y-auto">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h3 className="text-sm font-medium text-[var(--text-primary)]">生成结果</h3>
                  <p className="text-xs text-[var(--text-tertiary)]">
                    共 {results.length} 个方案 ·{' '}
                    {MODELS[tab].find((m) => m.id === selectedModel)?.name}
                  </p>
                </div>
                <Button variant="outline" size="sm" onClick={handleGenerate}>
                  <RefreshCw size={13} />
                  重新生成
                </Button>
              </div>

              <div className="grid grid-cols-2 gap-4">
                {results.map((result, idx) => (
                  <ResultCard
                    key={result.id}
                    result={result}
                    onClick={() => openLightbox(idx)}
                    onRegenerate={handleGenerate}
                  />
                ))}
              </div>
            </div>
          )}

          {/* 空状态 */}
          {!loading && results.length === 0 && (
            <div className="flex-1 flex flex-col items-center justify-center gap-3 px-8 text-center">
              <div
                className="w-16 h-16 rounded-2xl bg-[var(--bg-card)] border border-[var(--border-subtle)]
                           flex items-center justify-center"
              >
                <Wand2 size={24} className="text-[var(--text-tertiary)]" />
              </div>
              <div>
                <p className="text-sm font-medium text-[var(--text-secondary)]">等待生成</p>
                <p className="text-xs text-[var(--text-tertiary)] mt-1 max-w-xs">
                  在左侧填写描述并选择风格，点击「开始生成」即可看到 AI 设计方案
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ════════ ImageLightbox（Portal，放在最外层） ════════ */}
      <ImageLightbox
        images={lbImages}
        currentIndex={lbIndex}
        onClose={() => setLbIndex(-1)}
        onIndexChange={setLbIndex}
      />
    </div>
  )
}