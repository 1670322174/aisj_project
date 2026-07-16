// 作用：AI 生成工作台，接入文生图、图生图和图生视频的完整业务链路。
// 支持工作流动态加载、原图上传、任务路由恢复、进度轮询、COS 结果展示和加入项目。
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  AlertCircle,
  Check,
  CheckCircle2,
  Clipboard,
  Film,
  FolderPlus,
  ImageIcon,
  Loader2,
  Maximize2,
  Play,
  RefreshCw,
  RotateCcw,
  Sparkles,
  Wand2,
  X,
} from 'lucide-react'
import { aiApi, type AIJob, type AIJobResult, type WorkflowOption } from '@/api/modules/ai'
import { refreshAuthSession } from '@/api/client'
import { imagesApi } from '@/api/modules/images'
import { projectsApi, type Room } from '@/api/modules/projects'
import AssetPickerModal from '@/components/ai/AssetPickerModal'
import ImageInputField from '@/components/ai/ImageInputField'
import ImageLightbox, { type ImageItem } from '@/components/ImageLightbox'
import { Button } from '@/components/ui/Button'
import {
  ASPECT_RATIOS,
  DEFAULT_NEGATIVE_PROMPT,
  IMAGE_RESOLUTIONS,
  MODE_LABELS,
  ROOM_TYPES,
  STYLE_TAGS,
  VIDEO_RESOLUTIONS,
  WORKFLOW_MODE,
  modeFromRoute,
  roomLabel,
  type GenerationMode,
} from '@/features/ai/config'
import type { GenerationAsset, GenerationFormState } from '@/features/ai/types'
import { useAppStore } from '@/store/useAppStore'
import { cn } from '@/utils/cn'

type ImageSlot = 'sourceImage' | 'referenceImage' | 'firstFrame' | 'lastFrame'

type FormAssets = Record<ImageSlot, GenerationAsset | null>

type ToastState = { type: 'success' | 'error' | 'info'; message: string } | null

const INITIAL_FORM: GenerationFormState = {
  prompt: '',
  negativePrompt: '',
  roomType: '',
  aspectRatio: 'auto',
  resolution: '1K',
  batchSize: 1,
  seed: '',
  duration: 6,
}

function stripSystemNegativePrompt(value: string): string {
  const systemPrompt = DEFAULT_NEGATIVE_PROMPT.replace(/,+\s*$/, '').trim()
  const normalized = value.trim()
  if (!normalized.toLowerCase().startsWith(systemPrompt.toLowerCase())) {
    return normalized
  }
  return normalized.slice(systemPrompt.length).replace(/^\s*,+\s*/, '').trim()
}

const INITIAL_ASSETS: FormAssets = {
  sourceImage: null,
  referenceImage: null,
  firstFrame: null,
  lastFrame: null,
}

const SUCCESS_STATUSES = new Set(['succeeded', 'completed', 'success'])
const FAILED_STATUSES = new Set(['failed', 'error', 'timeout', 'cancelled', 'canceled'])
const RUNNING_STATUSES = new Set(['created', 'queued', 'running', 'processing', 'uploading'])

function isSuccess(status?: string): boolean {
  return SUCCESS_STATUSES.has((status ?? '').toLowerCase())
}

function isFailed(status?: string): boolean {
  return FAILED_STATUSES.has((status ?? '').toLowerCase())
}

function isRunning(status?: string): boolean {
  return RUNNING_STATUSES.has((status ?? '').toLowerCase())
}

function draftKey(mode: GenerationMode): string {
  return `ai-generation-draft:${mode}`
}

function buildFileName(asset: GenerationAsset, blob: Blob): string {
  const fromLabel = asset.label.replace(/[\\/:*?"<>|]/g, '-').trim()
  if (fromLabel.includes('.')) return fromLabel
  const extension = blob.type.includes('png') ? 'png' : blob.type.includes('webp') ? 'webp' : 'jpg'
  return `${fromLabel || 'input-image'}.${extension}`
}

async function fetchAssetAsFile(asset: GenerationAsset): Promise<File> {
  if (asset.file) return asset.file

  let url = asset.fullUrl || ''
  if (asset.source === 'gallery' && asset.imageId) {
    url = imagesApi.getOriginalUrl(asset.imageId)
  } else if (asset.source === 'project' && !asset.isAi && asset.imageId) {
    url = imagesApi.getOriginalUrl(asset.imageId)
  }

  if (!url) throw new Error(`无法读取“${asset.label}”的原图地址`)

  let response = await fetch(url, {
    credentials: 'include',
  })
  if (response.status === 401 && await refreshAuthSession()) {
    response = await fetch(url, { credentials: 'include' })
  }
  if (!response.ok) throw new Error(`读取“${asset.label}”失败：HTTP ${response.status}`)

  const blob = await response.blob()
  return new File([blob], buildFileName(asset, blob), { type: blob.type || 'image/jpeg' })
}

export default function GeneratePage() {
  const navigate = useNavigate()
  const params = useParams<{ mode?: string; jobId?: string }>()
  const mode = modeFromRoute(params.mode)
  const routeJobId = params.jobId ?? ''
  const previousRouteJobIdRef = useRef(routeJobId)

  const {
    authUser,
    activeProject,
    projects,
    loadProjects,
  } = useAppStore()

  const [workflowOptions, setWorkflowOptions] = useState<WorkflowOption[]>([])
  const [selectedWorkflowCode, setSelectedWorkflowCode] = useState('')
  const [optionsLoading, setOptionsLoading] = useState(true)
  const [form, setForm] = useState<GenerationFormState>(INITIAL_FORM)
  const [selectedStyles, setSelectedStyles] = useState<string[]>([])
  const [assets, setAssets] = useState<FormAssets>(INITIAL_ASSETS)
  const [pickerOpen, setPickerOpen] = useState(false)
  const [pickerTarget, setPickerTarget] = useState<ImageSlot>('sourceImage')

  const [submitting, setSubmitting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [job, setJob] = useState<AIJob | null>(null)
  const [results, setResults] = useState<AIJobResult[]>([])
  const [resultOriginalUrls, setResultOriginalUrls] = useState<Record<number, string>>({})
  const [pageError, setPageError] = useState('')
  const [toast, setToast] = useState<ToastState>(null)

  const [rooms, setRooms] = useState<Room[]>([])
  const [selectedRoomId, setSelectedRoomId] = useState('')
  const [addingResultId, setAddingResultId] = useState<number | null>(null)
  const [addedResultIds, setAddedResultIds] = useState<Set<number>>(new Set())

  const [lightboxIndex, setLightboxIndex] = useState(-1)
  const [videoPreviewUrl, setVideoPreviewUrl] = useState('')
  const toastTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const workflowsForMode = useMemo(
    () => workflowOptions.filter((option) => WORKFLOW_MODE[option.workflowCode] === mode),
    [mode, workflowOptions],
  )

  const selectedWorkflow = useMemo(
    () => workflowsForMode.find((option) => option.workflowCode === selectedWorkflowCode) ?? workflowsForMode[0] ?? null,
    [selectedWorkflowCode, workflowsForMode],
  )

  const isAdmin = authUser?.role === 'Administrator'
  const taskBusy = submitting || (job ? isRunning(job.status) : false)
  const outputType = selectedWorkflow?.outputType ?? (mode === 'video' ? 'video' : 'image')

  const requiredInputs = useMemo(
    () => new Set(selectedWorkflow?.requiredInputs ?? []),
    [selectedWorkflow?.requiredInputs],
  )
  const allInputs = useMemo(
    () => new Set([...(selectedWorkflow?.requiredInputs ?? []), ...(selectedWorkflow?.optionalInputs ?? [])]),
    [selectedWorkflow?.optionalInputs, selectedWorkflow?.requiredInputs],
  )

  const needsSource = allInputs.has('sourceImage')
  const needsReference = allInputs.has('referenceImage')
  const needsFirstFrame = allInputs.has('firstFrame')
  const needsLastFrame = allInputs.has('lastFrame')
  const supportsNegativePrompt = allInputs.has('negativePrompt') || mode !== 'video'
  const supportsBatch = allInputs.has('batchSize') || mode === 'text'
  const supportsDuration = allInputs.has('duration') || mode === 'video'

  const showToast = useCallback((next: ToastState) => {
    setToast(next)
    if (toastTimerRef.current) clearTimeout(toastTimerRef.current)
    if (next) toastTimerRef.current = setTimeout(() => setToast(null), 2600)
  }, [])

  useEffect(() => () => {
    if (toastTimerRef.current) clearTimeout(toastTimerRef.current)
  }, [])

  useEffect(() => {
    if (projects.length === 0) loadProjects().catch(() => undefined)
  }, [loadProjects, projects.length])

  useEffect(() => {
    let active = true
    setOptionsLoading(true)
    aiApi.getWorkflowOptions()
      .then((options) => {
        if (!active) return
        setWorkflowOptions(options)
      })
      .catch((error) => {
        if (!active) return
        setPageError(error instanceof Error ? error.message : '工作流加载失败')
      })
      .finally(() => {
        if (active) setOptionsLoading(false)
      })
    return () => { active = false }
  }, [])

  useEffect(() => {
    const returningFromJob = Boolean(previousRouteJobIdRef.current) && !routeJobId
    previousRouteJobIdRef.current = routeJobId

    if (routeJobId) {
      setForm({ ...INITIAL_FORM, resolution: mode === 'video' ? '720p' : '1K' })
      setSelectedStyles([])
      setSelectedWorkflowCode('')
      setAssets(INITIAL_ASSETS)
      setPageError('')
      return
    }

    const saved = localStorage.getItem(draftKey(mode))
    if (saved) {
      try {
        const parsed = JSON.parse(saved) as {
          form?: Partial<GenerationFormState>
          selectedStyles?: string[]
          workflowCode?: string
        }
        setForm({
          ...INITIAL_FORM,
          ...parsed.form,
          prompt: returningFromJob ? '' : parsed.form?.prompt ?? '',
          negativePrompt: returningFromJob ? '' : stripSystemNegativePrompt(parsed.form?.negativePrompt ?? ''),
          resolution: mode === 'video' ? parsed.form?.resolution || '720p' : parsed.form?.resolution || '1K',
        })
        setSelectedStyles(parsed.selectedStyles ?? [])
        setSelectedWorkflowCode(parsed.workflowCode ?? '')
      } catch {
        setForm({ ...INITIAL_FORM, resolution: mode === 'video' ? '720p' : '1K' })
      }
    } else {
      setForm({ ...INITIAL_FORM, resolution: mode === 'video' ? '720p' : '1K' })
      setSelectedStyles([])
      setSelectedWorkflowCode('')
    }
    setAssets(INITIAL_ASSETS)
    setPageError('')
    if (!routeJobId) {
      setJob(null)
      setResults([])
      setResultOriginalUrls({})
    }
  }, [mode, routeJobId])

  useEffect(() => {
    if (workflowsForMode.length === 0) return
    if (!workflowsForMode.some((option) => option.workflowCode === selectedWorkflowCode)) {
      setSelectedWorkflowCode(workflowsForMode[0].workflowCode)
    }
  }, [selectedWorkflowCode, workflowsForMode])

  useEffect(() => {
    if (routeJobId) return
    localStorage.setItem(draftKey(mode), JSON.stringify({ form, selectedStyles, workflowCode: selectedWorkflowCode }))
  }, [form, mode, routeJobId, selectedStyles, selectedWorkflowCode])

  useEffect(() => {
    if (!activeProject) {
      setRooms([])
      setSelectedRoomId('')
      return
    }
    projectsApi.getProjectRooms(activeProject.projectID).then(setRooms).catch(() => setRooms([]))
  }, [activeProject])

  useEffect(() => {
    if (!routeJobId) return
    let disposed = false
    let timer: ReturnType<typeof setTimeout> | null = null

    async function refresh() {
      try {
        const currentJob = await aiApi.getJob(routeJobId)
        if (disposed) return
        setJob(currentJob)
        setSelectedWorkflowCode(currentJob.workflowCode)
        setForm((current) => ({
          ...current,
          prompt: current.prompt || currentJob.prompt || '',
          negativePrompt: current.negativePrompt || stripSystemNegativePrompt(currentJob.negativePrompt || ''),
        }))
        setPageError('')

        if (isSuccess(currentJob.status)) {
          const currentResults = await aiApi.getJobResults(routeJobId)
          if (!disposed) setResults(currentResults)
          return
        }

        if (isFailed(currentJob.status)) return
        timer = setTimeout(refresh, 2500)
      } catch (error) {
        if (!disposed) {
          setPageError(error instanceof Error ? error.message : '任务状态查询失败')
          timer = setTimeout(refresh, 5000)
        }
      }
    }

    void refresh()
    return () => {
      disposed = true
      if (timer) clearTimeout(timer)
    }
  }, [routeJobId])

  function changeMode(nextMode: GenerationMode) {
    if (taskBusy) {
      showToast({ type: 'info', message: '当前任务执行中，暂时不能切换生成类型' })
      return
    }
    navigate(`/app/generate/${nextMode}`)
  }

  function updateForm<K extends keyof GenerationFormState>(key: K, value: GenerationFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  function toggleStyle(style: string) {
    setSelectedStyles((current) => current.includes(style) ? current.filter((item) => item !== style) : [...current, style])
  }

  function openAssetPicker(target: ImageSlot) {
    setPickerTarget(target)
    setPickerOpen(true)
  }

  function setAsset(slot: ImageSlot, asset: GenerationAsset | null) {
    setAssets((current) => ({ ...current, [slot]: asset }))
  }

  function validateSubmission(): string | null {
    if (!selectedWorkflow) return '请选择一个可用工作流'
    if (requiredInputs.has('prompt') && !form.prompt.trim()) return '请输入提示词'
    if (requiredInputs.has('sourceImage') && !assets.sourceImage) return '请添加原始空间图'
    if (requiredInputs.has('referenceImage') && !assets.referenceImage) return '请添加风格参考图'
    if (requiredInputs.has('firstFrame') && !assets.firstFrame) return '请添加首帧图片'
    if (requiredInputs.has('lastFrame') && !assets.lastFrame) return '请添加尾帧图片'
    return null
  }

  function composedPrompt(): string {
    const additions: string[] = []
    if (form.roomType) additions.push(`房间类型：${roomLabel(form.roomType)}`)
    if (selectedStyles.length > 0) additions.push(`设计风格：${selectedStyles.join('、')}`)
    return [form.prompt.trim(), ...additions].filter(Boolean).join('。')
  }

  async function uploadAsset(slot: ImageSlot, asset: GenerationAsset, index: number, total: number): Promise<string> {
    const file = await fetchAssetAsFile(asset)
    const result = await aiApi.uploadInputImage(file, slot, (progress) => {
      const aggregate = Math.round(((index + progress / 100) / total) * 100)
      setUploadProgress(aggregate)
    })
    return result.name
  }

  const submitCurrentForm = useCallback(async () => {
    const validation = validateSubmission()
    if (validation) {
      setPageError(validation)
      return
    }
    if (!selectedWorkflow) return

    setSubmitting(true)
    setUploadProgress(0)
    setPageError('')
    setResults([])
    setResultOriginalUrls({})

    try {
      const items: Array<[ImageSlot, GenerationAsset]> = (Object.entries(assets) as Array<[ImageSlot, GenerationAsset | null]>)
        .filter((entry): entry is [ImageSlot, GenerationAsset] => Boolean(entry[1]))

      const uploaded = new Map<ImageSlot, string>()
      for (let index = 0; index < items.length; index += 1) {
        const [slot, asset] = items[index]
        uploaded.set(slot, await uploadAsset(slot, asset, index, items.length))
      }

      const negativePrompt = supportsNegativePrompt
        ? (form.negativePrompt.trim() || null)
        : null

      const numericSeed = form.seed.trim() ? Number(form.seed) : Math.floor(Math.random() * 2_147_483_647)
      const parameters: Record<string, unknown> = {
        seed: Number.isFinite(numericSeed) ? numericSeed : Math.floor(Math.random() * 2_147_483_647),
        resolution: form.resolution,
        width: form.aspectRatio === '3:4' ? 1024 : form.aspectRatio === '4:3' ? 1536 : 1024,
        height: form.aspectRatio === '3:4' ? 1536 : form.aspectRatio === '4:3' ? 1024 : 1024,
        batchSize: Math.min(4, Math.max(1, form.batchSize)),
        sizePreset: form.resolution,
        duration: form.duration,
        generateAudio: false,
      }
      // “自动”表示沿用各工作流 JSON 自己的默认比例，不能把字符串 auto
      // 强行写入不支持该枚举值的节点（例如简单文生图默认使用 1:1）。
      if (form.aspectRatio !== 'auto') parameters.aspectRatio = form.aspectRatio

      const submitted = await aiApi.submitGeneration({
        workflowCode: selectedWorkflow.workflowCode,
        modelCode: null,
        prompt: composedPrompt(),
        negativePrompt,
        projectId: activeProject ? Number(activeProject.projectID) : null,
        roomId: selectedRoomId ? Number(selectedRoomId) : null,
        sourceImageName: uploaded.get('sourceImage') ?? null,
        referenceImageName: uploaded.get('referenceImage') ?? null,
        firstFrameImageName: uploaded.get('firstFrame') ?? null,
        lastFrameImageName: uploaded.get('lastFrame') ?? null,
        inputImages: Object.fromEntries(uploaded),
        parameters,
      })

      const nextPath = `/app/generate/${mode}/jobs/${submitted.jobId}`
      navigate(nextPath)
      showToast({ type: 'success', message: '任务已提交，正在生成' })
    } catch (error) {
      setPageError(error instanceof Error ? error.message : '任务提交失败')
    } finally {
      setSubmitting(false)
      setUploadProgress(0)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeProject, assets, form, mode, navigate, selectedRoomId, selectedStyles, selectedWorkflow, showToast, supportsNegativePrompt])

  async function handleCancel() {
    if (!job) return
    try {
      await aiApi.cancelJob(job.jobId)
      setJob({ ...job, status: 'cancelled' })
      showToast({ type: 'info', message: '任务已取消' })
    } catch (error) {
      showToast({ type: 'error', message: error instanceof Error ? error.message : '取消任务失败' })
    }
  }

  async function handleAddToProject(result: AIJobResult) {
    if (!activeProject) {
      showToast({ type: 'info', message: '请先在左侧选择当前项目' })
      return
    }
    setAddingResultId(result.aiImageID)
    try {
      await projectsApi.addAiImageToProject(
        activeProject.projectID,
        result.aiImageID,
        selectedRoomId ? Number(selectedRoomId) : null,
      )
      setAddedResultIds((current) => new Set(current).add(result.aiImageID))
      showToast({ type: 'success', message: `已加入“${activeProject.name}”` })
    } catch (error) {
      showToast({ type: 'error', message: error instanceof Error ? error.message : '加入项目失败' })
    } finally {
      setAddingResultId(null)
    }
  }

  async function handleEditAgain(result: AIJobResult) {
    try {
      const url = await aiApi.fetchJobResultMedia(result.jobId, result.aiImageID, 'original')
      const asset: GenerationAsset = {
        source: 'history',
        id: String(result.aiImageID),
        aiImageId: result.aiImageID,
        isAi: true,
        label: `AI 结果 #${result.aiImageID}`,
        previewUrl: url,
        fullUrl: url,
      }

      if (mode === 'image') {
        setAsset('sourceImage', asset)
        const sourceWorkflow = workflowsForMode.find((option) => option.requiredInputs.includes('sourceImage'))
        if (sourceWorkflow) setSelectedWorkflowCode(sourceWorkflow.workflowCode)
        navigate('/app/generate/image')
      } else {
        localStorage.setItem('ai-edit-source', JSON.stringify(asset))
        navigate('/app/generate/image')
      }
      showToast({ type: 'info', message: '已将结果带入图生图页面' })
    } catch (error) {
      showToast({ type: 'error', message: error instanceof Error ? error.message : '结果文件读取失败' })
    }
  }

  async function handlePreviewResult(result: AIJobResult, index: number) {
    try {
      const url = resultOriginalUrls[result.aiImageID]
        ?? await aiApi.fetchJobResultMedia(result.jobId, result.aiImageID, 'original')
      setResultOriginalUrls((current) => ({ ...current, [result.aiImageID]: url }))

      if (outputType === 'video') {
        setVideoPreviewUrl(url)
      } else {
        setLightboxIndex(index)
      }
    } catch (error) {
      showToast({
        type: 'error',
        message: error instanceof Error ? error.message : '结果文件加载失败',
      })
    }
  }

  useEffect(() => {
    if (mode !== 'image') return
    const raw = localStorage.getItem('ai-edit-source')
    if (!raw) return
    try {
      const asset = JSON.parse(raw) as GenerationAsset
      setAsset('sourceImage', asset)
      localStorage.removeItem('ai-edit-source')
    } catch {
      localStorage.removeItem('ai-edit-source')
    }
  }, [mode])

  async function copyPrompt() {
    try {
      await navigator.clipboard.writeText(job?.prompt || composedPrompt())
      showToast({ type: 'success', message: '提示词已复制' })
    } catch {
      showToast({ type: 'error', message: '复制失败，请手动复制' })
    }
  }

  const lightboxImages = useMemo<ImageItem[]>(
    () => results.map((result) => ({
      id: String(result.aiImageID),
      src: resultOriginalUrls[result.aiImageID] ?? '',
      label: selectedWorkflow?.name || 'AI 生成结果',
      source: 'ai',
    })),
    [resultOriginalUrls, results, selectedWorkflow?.name],
  )

  const statusText = job ? formatStatus(job.status) : ''
  const progress = job ? Math.min(100, Math.max(job.progressValue || 0, isSuccess(job.status) ? 100 : 5)) : uploadProgress

  return (
    <div className="h-full min-h-0 flex flex-col relative">
      <div className="flex-1 min-h-0 flex overflow-hidden">
        <aside className="w-[340px] shrink-0 flex flex-col border-r border-[var(--border-subtle)] bg-[var(--bg-base)]">
          <div className="p-4 border-b border-[var(--border-subtle)]">
            <div className="grid grid-cols-3 rounded-xl bg-[var(--bg-input)] border border-[var(--border-subtle)] p-1">
              {([
                { key: 'text' as const, icon: Wand2, label: '文生图' },
                { key: 'image' as const, icon: ImageIcon, label: '图生图' },
                { key: 'video' as const, icon: Film, label: '图生视频' },
              ]).map((item) => {
                const Icon = item.icon
                return (
                  <button
                    key={item.key}
                    type="button"
                    onClick={() => changeMode(item.key)}
                    className={cn(
                      'flex items-center justify-center gap-1 py-2 text-xs rounded-lg transition-all',
                      mode === item.key
                        ? 'bg-[var(--bg-card)] text-[var(--text-primary)] border border-[var(--border-subtle)] shadow-sm'
                        : 'text-[var(--text-secondary)] hover:text-[var(--text-primary)]',
                    )}
                  >
                    <Icon size={13} />{item.label}
                  </button>
                )
              })}
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-4 space-y-5">
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-xs font-medium text-[var(--text-secondary)]">选择功能</label>
                {mode === 'video' ? <span className="text-[10px] text-amber-400">测试中</span> : null}
              </div>
              {optionsLoading ? (
                <div className="py-6 flex justify-center"><Loader2 size={16} className="animate-spin text-[var(--text-tertiary)]" /></div>
              ) : (
                <div className="space-y-2">
                  {workflowsForMode.map((option) => (
                    <button
                      key={option.workflowCode}
                      type="button"
                      disabled={taskBusy}
                      onClick={() => {
                        setSelectedWorkflowCode(option.workflowCode)
                        setAssets(INITIAL_ASSETS)
                      }}
                      className={cn(
                        'w-full p-3 rounded-xl border text-left transition-all',
                        selectedWorkflow?.workflowCode === option.workflowCode
                          ? 'border-[var(--accent-border)] bg-[var(--accent-glow)]'
                          : 'border-[var(--border-default)] bg-[var(--bg-input)] hover:border-[var(--border-strong)]',
                      )}
                    >
                      <p className="text-xs font-semibold text-[var(--text-primary)]">{option.name}</p>
                      <p className="text-[10px] leading-relaxed text-[var(--text-tertiary)] mt-1">{option.description}</p>
                    </button>
                  ))}
                </div>
              )}
            </section>

            {needsSource ? (
              <ImageInputField
                label="原始空间图"
                description="点击开始生成时才上传至 ComfyUI"
                value={assets.sourceImage}
                required={requiredInputs.has('sourceImage')}
                disabled={taskBusy}
                onChange={(asset) => setAsset('sourceImage', asset)}
                onOpenLibrary={() => openAssetPicker('sourceImage')}
              />
            ) : null}

            {needsReference ? (
              <ImageInputField
                label="风格参考图"
                value={assets.referenceImage}
                required={requiredInputs.has('referenceImage')}
                disabled={taskBusy}
                onChange={(asset) => setAsset('referenceImage', asset)}
                onOpenLibrary={() => openAssetPicker('referenceImage')}
              />
            ) : null}

            {needsFirstFrame ? (
              <ImageInputField
                label="首帧图片"
                value={assets.firstFrame}
                required={requiredInputs.has('firstFrame')}
                disabled={taskBusy}
                onChange={(asset) => setAsset('firstFrame', asset)}
                onOpenLibrary={() => openAssetPicker('firstFrame')}
              />
            ) : null}

            {needsLastFrame ? (
              <ImageInputField
                label="尾帧图片"
                value={assets.lastFrame}
                required={requiredInputs.has('lastFrame')}
                disabled={taskBusy}
                onChange={(asset) => setAsset('lastFrame', asset)}
                onOpenLibrary={() => openAssetPicker('lastFrame')}
              />
            ) : null}

            <section className="space-y-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                提示词{requiredInputs.has('prompt') ? <span className="text-red-400 ml-1">*</span> : null}
              </label>
              <textarea
                value={form.prompt}
                disabled={taskBusy}
                onChange={(event) => updateForm('prompt', event.target.value.slice(0, 1000))}
                placeholder={mode === 'text' ? '描述你想生成的室内空间...' : mode === 'video' ? '描述镜头运动和空间漫游效果...' : '描述需要对图片进行的修改...'}
                rows={5}
                className="w-full rounded-xl text-sm resize-none p-3 bg-[var(--bg-input)] border border-[var(--border-default)] text-[var(--text-primary)] focus:outline-none focus:border-[var(--accent-border)]"
              />
              <p className="text-right text-[10px] text-[var(--text-tertiary)]">{form.prompt.length}/1000</p>
            </section>

            {supportsNegativePrompt ? (
              <section className="space-y-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">补充负面提示词 <span className="font-normal text-[var(--text-tertiary)]">（可为空）</span></label>
                <textarea
                  value={form.negativePrompt}
                  disabled={taskBusy}
                  onChange={(event) => updateForm('negativePrompt', event.target.value)}
                  placeholder="填写你希望额外排除的内容"
                  rows={3}
                  className="w-full rounded-xl text-xs resize-none p-3 bg-[var(--bg-input)] border border-[var(--border-default)] text-[var(--text-primary)] focus:outline-none focus:border-[var(--accent-border)]"
                />
              </section>
            ) : null}

            <section className="space-y-2">
              <label className="text-xs font-medium text-[var(--text-secondary)]">风格标签</label>
              <div className="flex flex-wrap gap-1.5">
                {STYLE_TAGS.map((style) => (
                  <button
                    key={style}
                    type="button"
                    disabled={taskBusy}
                    onClick={() => toggleStyle(style)}
                    className={cn(
                      'px-2.5 py-1 text-[11px] rounded-full border transition-colors',
                      selectedStyles.includes(style)
                        ? 'border-[var(--accent-border)] bg-[var(--accent-glow)] text-[var(--text-primary)]'
                        : 'border-[var(--border-default)] text-[var(--text-secondary)]',
                    )}
                  >
                    {style}
                  </button>
                ))}
              </div>
            </section>

            <div className="grid grid-cols-2 gap-3">
              <SelectField label="大小" value={form.resolution} disabled={taskBusy} onChange={(value) => updateForm('resolution', value)} options={mode === 'video' ? VIDEO_RESOLUTIONS : IMAGE_RESOLUTIONS} />
              <SelectField label="比例" value={form.aspectRatio} disabled={taskBusy} onChange={(value) => updateForm('aspectRatio', value)} options={ASPECT_RATIOS} />
              {supportsBatch && mode !== 'video' ? (
                <SelectField label="数量" value={String(form.batchSize)} disabled={taskBusy} onChange={(value) => updateForm('batchSize', Number(value))} options={[1, 2, 3, 4].map((value) => ({ value: String(value), label: `${value} 张` }))} />
              ) : null}
              <SelectField label="房间类型" value={form.roomType} disabled={taskBusy} onChange={(value) => updateForm('roomType', value)} options={ROOM_TYPES} />
              {supportsDuration && mode === 'video' ? (
                <SelectField label="时长" value={String(form.duration)} disabled={taskBusy} onChange={(value) => updateForm('duration', Number(value))} options={[{ value: '5', label: '5 秒' }, { value: '6', label: '6 秒' }, { value: '7', label: '7 秒' }]} />
              ) : null}
            </div>

            <section className="space-y-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Seed <span className="font-normal text-[var(--text-tertiary)]">（留空随机）</span></label>
              <input
                value={form.seed}
                disabled={taskBusy}
                inputMode="numeric"
                onChange={(event) => updateForm('seed', event.target.value.replace(/\D/g, ''))}
                placeholder="随机"
                className="w-full h-9 px-3 rounded-lg bg-[var(--bg-input)] border border-[var(--border-default)] text-sm text-[var(--text-primary)]"
              />
            </section>
          </div>

          <div className="p-4 border-t border-[var(--border-subtle)]">
            <Button variant="primary" size="lg" className="w-full" disabled={taskBusy || !selectedWorkflow} onClick={() => void submitCurrentForm()}>
              {submitting ? <><Loader2 size={15} className="animate-spin" />上传中 {uploadProgress}%</> : taskBusy ? <><Loader2 size={15} className="animate-spin" />任务执行中</> : <><Sparkles size={15} />开始生成</>}
            </Button>
          </div>
        </aside>

        <main className="flex-1 min-w-0 overflow-y-auto p-6">
          {pageError ? (
            <div className="mb-4 p-3 rounded-xl border border-red-500/20 bg-red-500/10 flex items-start gap-2 text-sm text-red-300">
              <AlertCircle size={16} className="mt-0.5 shrink-0" />
              <div className="flex-1">
                <p>{isAdmin ? pageError : simplifyError(pageError)}</p>
              </div>
              <button type="button" onClick={() => setPageError('')}><X size={14} /></button>
            </div>
          ) : null}

          {job ? (
            <TaskProgressCard
              job={job}
              statusText={statusText}
              progress={progress}
              onCancel={() => void handleCancel()}
              onRetry={() => void submitCurrentForm()}
              onCopyPrompt={() => void copyPrompt()}
              isAdmin={Boolean(isAdmin)}
            />
          ) : null}

          {results.length > 0 ? (
            <section className="mt-5">
              <div className="flex items-end justify-between gap-4 mb-4">
                <div>
                  <h2 className="text-sm font-semibold text-[var(--text-primary)]">生成结果</h2>
                </div>
                <div className="flex gap-2">
                  {activeProject ? (
                    <select
                      value={selectedRoomId}
                      onChange={(event) => setSelectedRoomId(event.target.value)}
                      className="h-9 px-3 rounded-lg bg-[var(--bg-input)] border border-[var(--border-default)] text-xs text-[var(--text-primary)]"
                    >
                      <option value="">加入项目时不指定房间</option>
                      {rooms.map((room) => <option key={room.roomID} value={room.roomID}>{room.name}</option>)}
                    </select>
                  ) : null}
                  <Button variant="outline" size="sm" onClick={() => void submitCurrentForm()}><RefreshCw size={13} />同参数重生成</Button>
                </div>
              </div>

              <div className={cn('grid gap-4', outputType === 'video' ? 'grid-cols-1' : 'grid-cols-2')}>
                {results.map((result, index) => (
                  <ResultCard
                    key={result.aiImageID}
                    result={result}
                    outputType={outputType}
                    activeProjectName={activeProject?.name ?? ''}
                    adding={addingResultId === result.aiImageID}
                    added={addedResultIds.has(result.aiImageID)}
                    onPreview={() => void handlePreviewResult(result, index)}
                    onAdd={() => void handleAddToProject(result)}
                    onEdit={() => void handleEditAgain(result)}
                    onRegenerate={() => void submitCurrentForm()}
                    onCopy={() => void copyPrompt()}
                  />
                ))}
              </div>
            </section>
          ) : job && isSuccess(job.status) ? (
            <div className="mt-5 p-8 rounded-2xl border border-[var(--border-default)] text-center text-sm text-[var(--text-tertiary)]">
              任务已完成，但暂未读取到结果文件。请稍后刷新页面重试。
            </div>
          ) : !job ? (
            <EmptyWorkspace mode={mode} />
          ) : null}
        </main>
      </div>

      <AssetPickerModal
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(asset) => setAsset(pickerTarget, asset)}
      />

      <ImageLightbox images={lightboxImages} currentIndex={lightboxIndex} onClose={() => setLightboxIndex(-1)} onIndexChange={setLightboxIndex} />

      {videoPreviewUrl ? (
        <div className="fixed inset-0 z-[120] bg-black/85 backdrop-blur-sm flex items-center justify-center p-6" onClick={() => setVideoPreviewUrl('')}>
          <button type="button" className="absolute top-5 right-5 p-2 rounded-full bg-white/10 text-white" onClick={() => setVideoPreviewUrl('')}><X size={18} /></button>
          <video src={videoPreviewUrl} controls autoPlay className="max-w-[92vw] max-h-[84vh] rounded-2xl shadow-2xl" onClick={(event) => event.stopPropagation()} />
        </div>
      ) : null}

      {toast ? (
        <div className={cn(
          'fixed right-5 bottom-5 z-[130] px-4 py-3 rounded-xl border shadow-xl text-sm flex items-center gap-2',
          toast.type === 'success' ? 'bg-emerald-950 border-emerald-700 text-emerald-200' : toast.type === 'error' ? 'bg-red-950 border-red-700 text-red-200' : 'bg-[var(--bg-card)] border-[var(--border-default)] text-[var(--text-primary)]',
        )}>
          {toast.type === 'success' ? <CheckCircle2 size={15} /> : toast.type === 'error' ? <AlertCircle size={15} /> : <Sparkles size={15} />}
          {toast.message}
        </div>
      ) : null}
    </div>
  )
}

function SelectField({
  label,
  value,
  options,
  disabled,
  onChange,
}: {
  label: string
  value: string
  options: Array<{ value: string; label: string }>
  disabled: boolean
  onChange: (value: string) => void
}) {
  return (
    <label className="space-y-1.5">
      <span className="text-xs font-medium text-[var(--text-secondary)]">{label}</span>
      <select
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="w-full h-9 px-2 rounded-lg bg-[var(--bg-input)] border border-[var(--border-default)] text-xs text-[var(--text-primary)]"
      >
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  )
}

function TaskProgressCard({
  job,
  statusText,
  progress,
  onCancel,
  onRetry,
  onCopyPrompt,
  isAdmin,
}: {
  job: AIJob
  statusText: string
  progress: number
  onCancel: () => void
  onRetry: () => void
  onCopyPrompt: () => void
  isAdmin: boolean
}) {
  return (
    <section className="rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3 min-w-0">
          <div className={cn(
            'w-10 h-10 rounded-xl flex items-center justify-center shrink-0',
            isSuccess(job.status) ? 'bg-emerald-500/15 text-emerald-400' : isFailed(job.status) ? 'bg-red-500/15 text-red-400' : 'bg-[var(--accent-glow)] text-[var(--accent-border)]',
          )}>
            {isSuccess(job.status) ? <Check size={18} /> : isFailed(job.status) ? <AlertCircle size={18} /> : <Loader2 size={18} className="animate-spin" />}
          </div>
          <div className="min-w-0">
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">{statusText}</h2>
            {isFailed(job.status) && job.errorMessage ? (
              <p className="text-xs text-red-400 mt-2">{isAdmin ? job.errorMessage : simplifyError(job.errorMessage)}</p>
            ) : null}
          </div>
        </div>
        <div className="flex gap-2 shrink-0">
          <Button variant="outline" size="sm" onClick={onCopyPrompt}><Clipboard size={13} />复制提示词</Button>
          {isRunning(job.status) ? <Button variant="outline" size="sm" onClick={onCancel}>取消</Button> : null}
          {isFailed(job.status) ? <Button variant="primary" size="sm" onClick={onRetry}><RotateCcw size={13} />重试</Button> : null}
        </div>
      </div>

      {!isFailed(job.status) ? (
        <div className="mt-4">
          <div className="h-2 rounded-full bg-[var(--bg-input)] overflow-hidden">
            <div className="h-full rounded-full bg-[var(--accent)] transition-all duration-500" style={{ width: `${progress}%` }} />
          </div>
          <div className={`flex mt-1.5 text-[10px] text-[var(--text-tertiary)] ${isSuccess(job.status) ? 'justify-end' : 'justify-between'}`}>
            {!isSuccess(job.status) ? <span>正在等待 ComfyUI 返回结果</span> : null}
            <span>{progress}%</span>
          </div>
        </div>
      ) : null}
    </section>
  )
}

function ResultCard({
  result,
  outputType,
  activeProjectName,
  adding,
  added,
  onPreview,
  onAdd,
  onEdit,
  onRegenerate,
  onCopy,
}: {
  result: AIJobResult
  outputType: 'image' | 'video'
  activeProjectName: string
  adding: boolean
  added: boolean
  onPreview: () => void
  onAdd: () => void
  onEdit: () => void
  onRegenerate: () => void
  onCopy: () => void
}) {
  const authExpiresAt = useAppStore((state) => state.authUser?.expiresAt ?? 0)
  const [preview, setPreview] = useState('')
  const [previewError, setPreviewError] = useState(false)

  useEffect(() => {
    let disposed = false
    setPreview('')
    setPreviewError(false)

    aiApi.fetchJobResultMedia(
      result.jobId,
      result.aiImageID,
      outputType === 'video' ? 'original' : 'thumbnail',
    ).then((url) => {
      if (!disposed) setPreview(url)
    }).catch(() => {
      if (!disposed) setPreviewError(true)
    })

    return () => { disposed = true }
  }, [authExpiresAt, outputType, result.aiImageID, result.jobId])

  return (
    <article className="rounded-2xl overflow-hidden border border-[var(--border-default)] bg-[var(--bg-card)]">
      <button type="button" onClick={onPreview} className="relative block w-full bg-black/20 group">
        {outputType === 'video' ? (
          <div className="aspect-video relative">
            {preview ? (
              <video src={preview} muted preload="metadata" className="w-full h-full object-cover" />
            ) : (
              <ResultMediaPlaceholder failed={previewError} />
            )}
            <div className="absolute inset-0 flex items-center justify-center bg-black/20 group-hover:bg-black/35 transition-colors">
              <div className="w-12 h-12 rounded-full bg-black/55 border border-white/20 flex items-center justify-center text-white"><Play size={20} className="ml-0.5" /></div>
            </div>
          </div>
        ) : (
          <div className="aspect-[4/3] relative overflow-hidden">
            {preview ? (
              <img
                src={preview}
                alt="AI 生成结果"
                onError={() => setPreviewError(true)}
                className="w-full h-full object-cover group-hover:scale-[1.02] transition-transform"
              />
            ) : (
              <ResultMediaPlaceholder failed={previewError} />
            )}
            <div className="absolute inset-0 opacity-0 group-hover:opacity-100 bg-black/30 flex items-center justify-center transition-opacity"><Maximize2 size={22} className="text-white" /></div>
          </div>
        )}
      </button>
      <div className="p-3">
        <div className="grid grid-cols-2 gap-2">
          <button type="button" disabled={adding || added} onClick={onAdd} className="h-9 rounded-lg border border-[var(--border-default)] text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] flex items-center justify-center gap-1.5 disabled:opacity-60">
            {adding ? <Loader2 size={13} className="animate-spin" /> : added ? <Check size={13} /> : <FolderPlus size={13} />}
            {added ? '已加入项目' : activeProjectName ? `加入 ${activeProjectName}` : '选择项目后加入'}
          </button>
          {outputType === 'image' ? (
            <button type="button" onClick={onEdit} className="h-9 rounded-lg border border-[var(--border-default)] text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] flex items-center justify-center gap-1.5"><ImageIcon size={13} />再次编辑</button>
          ) : (
            <button type="button" onClick={onPreview} className="h-9 rounded-lg border border-[var(--border-default)] text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] flex items-center justify-center gap-1.5"><Play size={13} />播放视频</button>
          )}
          <button type="button" onClick={onRegenerate} className="h-9 rounded-lg border border-[var(--border-default)] text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] flex items-center justify-center gap-1.5"><RefreshCw size={13} />同参数生成</button>
          <button type="button" onClick={onCopy} className="h-9 rounded-lg border border-[var(--border-default)] text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] flex items-center justify-center gap-1.5"><Clipboard size={13} />复制提示词</button>
        </div>
      </div>
    </article>
  )
}

function ResultMediaPlaceholder({ failed }: { failed: boolean }) {
  return (
    <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-[var(--bg-input)] text-[var(--text-tertiary)]">
      {failed ? <AlertCircle size={22} /> : <Loader2 size={22} className="animate-spin" />}
      <span className="text-xs">{failed ? '图片加载失败，点击可重试大图' : '正在加载结果'}</span>
    </div>
  )
}

function EmptyWorkspace({ mode }: { mode: GenerationMode }) {
  return (
    <div className="h-full min-h-[520px] flex flex-col items-center justify-center text-center">
      <div className="w-16 h-16 rounded-2xl bg-[var(--bg-card)] border border-[var(--border-subtle)] flex items-center justify-center">
        {mode === 'video' ? <Film size={24} className="text-[var(--text-tertiary)]" /> : mode === 'image' ? <ImageIcon size={24} className="text-[var(--text-tertiary)]" /> : <Wand2 size={24} className="text-[var(--text-tertiary)]" />}
      </div>
      <h2 className="text-sm font-medium text-[var(--text-secondary)] mt-4">等待{MODE_LABELS[mode]}</h2>
      <p className="text-xs text-[var(--text-tertiary)] mt-1 max-w-sm">在左侧选择功能并填写参数。任务提交后会自动进入独立路由，刷新页面也能继续恢复进度。</p>
    </div>
  )
}

function formatStatus(status: string): string {
  const value = status.toLowerCase()
  if (value === 'created') return '任务已创建'
  if (value === 'queued') return '任务排队中'
  if (value === 'running' || value === 'processing') return 'AI 正在生成'
  if (value === 'uploading') return '正在保存结果'
  if (isSuccess(value)) return '生成完成'
  if (value === 'cancelled' || value === 'canceled') return '任务已取消'
  if (value === 'timeout') return '任务执行超时'
  if (isFailed(value)) return '生成失败'
  return status || '处理中'
}

function simplifyError(message: string): string {
  const lower = message.toLowerCase()
  if (lower.includes('unauthorized') || lower.includes('login')) return 'AI 服务认证失败，请联系管理员'
  if (lower.includes('connect') || lower.includes('refused') || lower.includes('无法连接') || lower.includes('failed to fetch') || lower.includes('network')) return '无法连接到服务端，请稍后重试'
  if (lower.includes('timeout') || lower.includes('超时')) return '生成时间过长，请稍后重试'
  return '生成失败，请稍后重试'
}
