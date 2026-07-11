// 作用：从图库、当前项目或历史生成结果中选择工作流输入图片。
import { useEffect, useMemo, useState } from 'react'
import ReactDOM from 'react-dom'
import { FolderOpen, History, Images, Loader2, Search, X } from 'lucide-react'
import { aiApi, type AIJob, type AIJobResult } from '@/api/modules/ai'
import { imagesApi, type NormalizedImage } from '@/api/modules/images'
import { projectsApi, type NormalizedProjectImage } from '@/api/modules/projects'
import { useAppStore } from '@/store/useAppStore'
import type { GenerationAsset } from '@/features/ai/types'
import { cn } from '@/utils/cn'

type PickerTab = 'gallery' | 'project' | 'history'

type Props = {
  open: boolean
  onClose: () => void
  onSelect: (asset: GenerationAsset) => void
}

export default function AssetPickerModal({ open, onClose, onSelect }: Props) {
  const { projects, activeProject, loadProjects } = useAppStore()
  const [tab, setTab] = useState<PickerTab>('gallery')
  const [keyword, setKeyword] = useState('')
  const [gallery, setGallery] = useState<NormalizedImage[]>([])
  const [selectedProjectId, setSelectedProjectId] = useState('')
  const [projectImages, setProjectImages] = useState<NormalizedProjectImage[]>([])
  const [projectPreviewUrls, setProjectPreviewUrls] = useState<Record<string, string>>({})
  const [jobs, setJobs] = useState<AIJob[]>([])
  const [historyResults, setHistoryResults] = useState<AIJobResult[]>([])
  const [historyPreviewUrls, setHistoryPreviewUrls] = useState<Record<number, string>>({})
  const [selectedHistoryJob, setSelectedHistoryJob] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!open) return
    if (projects.length === 0) loadProjects().catch(() => undefined)
    setSelectedProjectId((current) => current || activeProject?.projectID || projects[0]?.projectID || '')
  }, [activeProject?.projectID, loadProjects, open, projects])

  useEffect(() => {
    if (!open) return
    if (tab === 'gallery') {
      void loadGallery()
    } else if (tab === 'project' && selectedProjectId) {
      void loadProjectImages(selectedProjectId)
    } else if (tab === 'history') {
      void loadJobs()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, tab, selectedProjectId])

  async function loadGallery() {
    if (!keyword.trim()) {
      setGallery([])
      setError('')
      return
    }
    setLoading(true)
    setError('')
    try {
      const result = await imagesApi.searchImages({ keyword, pageSize: 24 })
      setGallery(result.data)
    } catch (err) {
      setError(err instanceof Error ? err.message : '图库加载失败')
    } finally {
      setLoading(false)
    }
  }

  async function loadProjectImages(projectId: string) {
    setLoading(true)
    setError('')
    try {
      const nextImages = await projectsApi.getProjectImages(projectId)
      setProjectImages(nextImages)
      setProjectPreviewUrls({})
      nextImages.forEach((image) => {
        projectsApi.fetchProjectImageMedia(projectId, image.relationID, 'thumbnail')
          .then((url) => setProjectPreviewUrls((current) => ({ ...current, [image.relationID]: url })))
          .catch(() => undefined)
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : '项目图片加载失败')
    } finally {
      setLoading(false)
    }
  }

  async function loadJobs() {
    setLoading(true)
    setError('')
    try {
      const result = await aiApi.getJobs(1, 30)
      setJobs(result.items.filter((job) => ['succeeded', 'completed', 'success'].includes(job.status.toLowerCase())))
    } catch (err) {
      setError(err instanceof Error ? err.message : '历史任务加载失败')
    } finally {
      setLoading(false)
    }
  }

  async function loadHistoryResults(jobId: string) {
    setSelectedHistoryJob(jobId)
    setLoading(true)
    setError('')
    try {
      const nextResults = (await aiApi.getJobResults(jobId))
        .filter((result) => result.sourceType.toLowerCase() !== 'video')
      setHistoryResults(nextResults)
      setHistoryPreviewUrls({})
      nextResults.forEach((result) => {
        aiApi.fetchJobResultMedia(jobId, result.aiImageID, 'thumbnail')
          .then((url) => setHistoryPreviewUrls((current) => ({ ...current, [result.aiImageID]: url })))
          .catch(() => undefined)
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : '历史结果加载失败')
    } finally {
      setLoading(false)
    }
  }

  const currentProjectName = useMemo(
    () => projects.find((project) => project.projectID === selectedProjectId)?.name ?? '项目',
    [projects, selectedProjectId],
  )

  async function selectProjectImage(image: NormalizedProjectImage) {
    try {
      const fullUrl = await projectsApi.fetchProjectImageMedia(
        selectedProjectId,
        image.relationID,
        'original',
      )
      onSelect({
        source: 'project',
        id: image.relationID,
        imageId: image.imageId,
        aiImageId: image.aiImageID ? Number(image.aiImageID) : undefined,
        isAi: image.isAi,
        label: image.fileName || currentProjectName,
        previewUrl: projectPreviewUrls[image.relationID] || fullUrl,
        fullUrl,
      })
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : '项目图片读取失败')
    }
  }

  async function selectHistoryResult(result: AIJobResult) {
    try {
      const fullUrl = await aiApi.fetchJobResultMedia(result.jobId, result.aiImageID, 'original')
      onSelect({
        source: 'history',
        id: String(result.aiImageID),
        aiImageId: result.aiImageID,
        isAi: true,
        label: `AI 结果 #${result.aiImageID}`,
        previewUrl: historyPreviewUrls[result.aiImageID] || fullUrl,
        fullUrl,
      })
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : '历史结果读取失败')
    }
  }

  if (!open) return null

  return ReactDOM.createPortal(
    <div className="fixed inset-0 z-[100] bg-black/70 backdrop-blur-sm flex items-center justify-center p-6" onMouseDown={onClose}>
      <div
        className="w-full max-w-5xl h-[min(720px,86vh)] rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] shadow-[var(--shadow-elevated)] flex flex-col overflow-hidden animate-slide-up"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="h-14 px-5 flex items-center justify-between border-b border-[var(--border-subtle)]">
          <div>
            <h2 className="text-sm font-semibold text-[var(--text-primary)]">选择输入图片</h2>
            <p className="text-[11px] text-[var(--text-tertiary)] mt-0.5">选择后将在点击生成时上传原始文件，不压缩</p>
          </div>
          <button type="button" onClick={onClose} className="p-2 rounded-lg hover:bg-[var(--bg-card-hover)] text-[var(--text-secondary)]">
            <X size={17} />
          </button>
        </div>

        <div className="px-5 pt-4 flex gap-2">
          {([
            { key: 'gallery' as const, label: '图库', icon: Images },
            { key: 'project' as const, label: '项目图片', icon: FolderOpen },
            { key: 'history' as const, label: '历史生成', icon: History },
          ]).map((item) => {
            const Icon = item.icon
            return (
              <button
                key={item.key}
                type="button"
                onClick={() => setTab(item.key)}
                className={cn(
                  'px-3 py-2 rounded-lg text-xs border flex items-center gap-1.5 transition-colors',
                  tab === item.key
                    ? 'border-[var(--accent-border)] bg-[var(--accent-glow)] text-[var(--text-primary)]'
                    : 'border-[var(--border-default)] text-[var(--text-secondary)] hover:text-[var(--text-primary)]',
                )}
              >
                <Icon size={13} />
                {item.label}
              </button>
            )
          })}
        </div>

        <div className="px-5 py-3 border-b border-[var(--border-subtle)]">
          {tab === 'gallery' && (
            <form
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault()
                void loadGallery()
              }}
            >
              <div className="relative flex-1">
                <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-tertiary)]" />
                <input
                  value={keyword}
                  onChange={(event) => setKeyword(event.target.value)}
                  placeholder="搜索图库图片"
                  className="w-full h-9 pl-9 pr-3 rounded-lg bg-[var(--bg-input)] border border-[var(--border-default)] text-sm text-[var(--text-primary)]"
                />
              </div>
              <button type="submit" className="px-4 h-9 rounded-lg bg-[var(--accent)] text-white text-xs">搜索</button>
            </form>
          )}

          {tab === 'project' && (
            <select
              value={selectedProjectId}
              onChange={(event) => setSelectedProjectId(event.target.value)}
              className="w-full h-9 px-3 rounded-lg bg-[var(--bg-input)] border border-[var(--border-default)] text-sm text-[var(--text-primary)]"
            >
              <option value="">请选择项目</option>
              {projects.map((project) => <option key={project.projectID} value={project.projectID}>{project.name}</option>)}
            </select>
          )}

          {tab === 'history' && selectedHistoryJob && (
            <button type="button" onClick={() => { setSelectedHistoryJob(''); setHistoryResults([]) }} className="text-xs text-[var(--accent-border)]">
              ← 返回历史任务
            </button>
          )}
        </div>

        <div className="flex-1 overflow-y-auto p-5">
          {loading && (
            <div className="h-full flex items-center justify-center gap-2 text-sm text-[var(--text-secondary)]">
              <Loader2 size={16} className="animate-spin" />加载中
            </div>
          )}
          {!loading && error && <div className="text-sm text-red-400 text-center py-10">{error}</div>}

          {!loading && !error && tab === 'gallery' && (
            <div className="grid grid-cols-4 gap-3">
              {gallery.map((image) => (
                <ImageChoice
                  key={image.imageId}
                  src={image.thumbnailUrl || imagesApi.getThumbnailUrl(image.imageId)}
                  label={image.fileName || image.room || '图库图片'}
                  onClick={() => {
                    onSelect({
                      source: 'gallery',
                      id: image.imageId,
                      imageId: image.imageId,
                      label: image.fileName || '图库图片',
                      previewUrl: image.thumbnailUrl || imagesApi.getThumbnailUrl(image.imageId),
                    })
                    onClose()
                  }}
                />
              ))}
              {gallery.length === 0 && <Empty text={keyword.trim() ? '没有找到图库图片' : '输入关键词搜索图库'} />}
            </div>
          )}

          {!loading && !error && tab === 'project' && (
            <div className="grid grid-cols-4 gap-3">
              {projectImages.map((image) => (
                <ImageChoice
                  key={image.relationID}
                  src={projectPreviewUrls[image.relationID] || ''}
                  label={image.fileName || currentProjectName}
                  onClick={() => void selectProjectImage(image)}
                />
              ))}
              {projectImages.length === 0 && <Empty text={selectedProjectId ? '该项目暂无图片' : '请先选择项目'} />}
            </div>
          )}

          {!loading && !error && tab === 'history' && !selectedHistoryJob && (
            <div className="flex flex-col gap-2">
              {jobs.map((job) => (
                <button
                  key={job.jobId}
                  type="button"
                  onClick={() => void loadHistoryResults(job.jobId)}
                  className="w-full p-3 rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] text-left hover:border-[var(--border-strong)]"
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-sm text-[var(--text-primary)] truncate">{job.prompt || job.workflowCode}</span>
                    <span className="text-[10px] text-[var(--text-tertiary)] shrink-0">{job.imageCount} 个结果</span>
                  </div>
                  <p className="text-[11px] text-[var(--text-tertiary)] mt-1">{new Date(job.createdAt).toLocaleString()}</p>
                </button>
              ))}
              {jobs.length === 0 && <Empty text="暂无可用历史结果" />}
            </div>
          )}

          {!loading && !error && tab === 'history' && selectedHistoryJob && (
            <div className="grid grid-cols-4 gap-3">
              {historyResults.map((result) => (
                <ImageChoice
                  key={result.aiImageID}
                  src={historyPreviewUrls[result.aiImageID] || ''}
                  label={`AI 结果 #${result.aiImageID}`}
                  onClick={() => void selectHistoryResult(result)}
                />
              ))}
              {historyResults.length === 0 && <Empty text="该任务暂无图片结果" />}
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body,
  )
}

function ImageChoice({ src, label, onClick }: { src: string; label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="group text-left rounded-xl overflow-hidden border border-[var(--border-subtle)] bg-[var(--bg-input)] hover:border-[var(--accent-border)]">
      <div className="aspect-[4/3] bg-[var(--bg-base)] overflow-hidden">
        {src ? <img src={src} alt={label} className="w-full h-full object-cover group-hover:scale-[1.03] transition-transform" /> : null}
      </div>
      <p className="p-2 text-[11px] text-[var(--text-secondary)] truncate">{label}</p>
    </button>
  )
}

function Empty({ text }: { text: string }) {
  return <div className="col-span-full py-16 text-center text-sm text-[var(--text-tertiary)]">{text}</div>
}
