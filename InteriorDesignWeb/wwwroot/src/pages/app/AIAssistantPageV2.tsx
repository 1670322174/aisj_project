import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Bot, Check, ChevronRight, Clock3, FolderOpen, History, Image as ImageIcon, Loader2, MessageSquarePlus, Paperclip, Send, Sparkles, Trash2, WandSparkles, X } from 'lucide-react'
import { assistantApi, type AssistantAction, type AssistantAgentRun, type AssistantConversationDetail, type AssistantConversationSummary } from '@/api/modules/assistant'
import { aiApi, type AIJob, type AIJobResult } from '@/api/modules/ai'
import { projectsApi, type Project, type Room } from '@/api/modules/projects'
import ImageLightbox, { type ImageItem } from '@/components/ImageLightbox'
import { AuthenticatedMediaError } from '@/api/media'
import { ApiRequestError } from '@/api/client'
import { cn } from '@/utils/cn'
import { useAppStore } from '@/store/useAppStore'

const panel = 'rounded-[22px] border border-[var(--border-default)] bg-[var(--bg-card)] shadow-[var(--shadow-card)]'
const input = 'w-full rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3.5 py-2.5 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)] focus:border-[var(--accent-border)] focus:outline-none focus:ring-2 focus:ring-[var(--accent)]/10'
const secondary = 'inline-flex items-center justify-center gap-2 rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3 py-2.5 text-xs font-semibold text-[var(--text-secondary)] transition-colors hover:border-[var(--border-strong)] hover:text-[var(--text-primary)] disabled:opacity-50'
const emptyDetail = (): AssistantConversationDetail => ({
  conversation: { conversationId: 0, title: '', status: 'active', projectId: null, projectName: '', roomId: null, roomName: '', createdAt: '', updatedAt: '' },
  brief: { roomType: '', area: '', style: '', colors: [], materials: [], requirements: [], lighting: '', constraints: [], missingFields: [] },
  messages: [], actions: [], agentRuns: [], agentArtifacts: [], attachments: [], rooms: [],
})
const requestId = () => globalThis.crypto?.randomUUID?.().replaceAll('-', '') ?? `${Date.now()}${Math.random().toString(16).slice(2)}`
const terminal = (status: string) => ['succeeded', 'failed', 'cancelled', 'timeout'].includes(status.toLowerCase())
const statusText = (status: string) => ({ created: '准备中', queued: '排队中', running: '生成中', processing: '处理中', uploading: '保存结果', succeeded: '已完成', failed: '生成失败', cancelled: '已取消', timeout: '已超时' })[status.toLowerCase()] ?? status
const thinkingText = (seconds: number) => seconds < 3
  ? '正在理解设计需求…'
  : seconds < 8
    ? '正在整理方案与专业提示词…'
    : '正在校验生成参数，请稍候…'
const agentName = (id: string) => ({ orchestrator: '前台协调', designer: '室内设计', 'prompt-engineer': '提示词工程', vision: '视觉分析', 'result-evaluator': '结果评估', 'materials-budget': '材料预算', 'iteration-analyst': '迭代分析' })[id] ?? (id || 'AI Agent')
const roomStatusText = (status: string) => ({ not_started: '未开始', analyzing: '分析中', design_ready: '方案完成', generation_ready: '待生成', generating: '生成中', completed: '已完成', needs_revision: '待修改' })[status] ?? status
const attachmentKindText = (kind: string) => ({ unclassified: '待识别', room_photo: '房间照片', rough_room: '毛坯房', style_reference: '风格参考', floor_plan: '户型图', material_reference: '材料图', generated_result: '生成结果', unknown: '其他图片' })[kind] ?? kind

function formatAssistantError(reason: unknown, stage: string, clientRequestId = '') {
  const message = reason instanceof Error ? reason.message : '发生未知错误'
  const details = [`${stage}：${message}`]
  if (reason instanceof ApiRequestError) {
    if (reason.code) details.push(`错误码：${reason.code}`)
    if (reason.requestId) details.push(`服务端追踪 ID：${reason.requestId}`)
  }
  if (clientRequestId) details.push(`助手请求 ID：${clientRequestId}`)
  return details.join('\n')
}

function InlineText({ children }: { children: string }) {
  return <>{children.split(/(\*\*[^*]+\*\*)/g).map((part, index) => part.startsWith('**') && part.endsWith('**') ? <strong key={index} className="font-semibold text-[var(--text-primary)]">{part.slice(2, -2)}</strong> : <span key={index}>{part}</span>)}</>
}

function FormattedMessage({ content }: { content: string }) {
  const output: ReactNode[] = []
  let list: string[] = []
  const flush = () => {
    if (!list.length) return
    output.push(<ul key={`list-${output.length}`} className="my-2.5 space-y-1.5">{list.map((line, index) => <li key={index} className="flex gap-2.5"><span className="mt-[0.7em] h-1.5 w-1.5 shrink-0 rounded-full bg-[var(--accent)]" /><span><InlineText>{line}</InlineText></span></li>)}</ul>)
    list = []
  }
  content.replace(/\r/g, '').split('\n').forEach((raw, index) => {
    const line = raw.trim()
    const match = line.match(/^(?:[-•]|\d+[.)、])\s*(.+)$/)
    if (match) { list.push(match[1]); return }
    flush()
    if (!line) { output.push(<div key={`space-${index}`} className="h-2" />); return }
    const heading = /^#{1,3}\s+/.test(line) || (line.length <= 18 && /[：:]$/.test(line))
    output.push(<p key={index} className={cn(heading && 'mt-2 font-semibold text-[var(--text-primary)]')}><InlineText>{line.replace(/^#{1,3}\s+/, '')}</InlineText></p>)
  })
  flush()
  return <div className="space-y-1">{output}</div>
}

function BriefItem({ label, value }: { label: string; value: string | string[] }) {
  const values = Array.isArray(value) ? value.filter(Boolean) : value ? [value] : []
  if (!values.length) return null
  return <div className="grid grid-cols-[52px_minmax(0,1fr)] gap-3 text-xs leading-5"><span className="pt-1 text-[var(--text-tertiary)]">{label}</span><div className="flex flex-wrap gap-1.5">{values.map((item) => <span key={item} className="rounded-lg border border-[var(--border-subtle)] bg-[var(--bg-input)] px-2 py-1 text-[var(--text-secondary)]">{item}</span>)}</div></div>
}

function GenerationDraft({ action, bound, executing, onExecute }: { action: AssistantAction; bound: boolean; executing: boolean; onExecute: (prompt: string, negative: string, autoAdd: boolean) => void }) {
  const [prompt, setPrompt] = useState(action.prompt)
  const [negative, setNegative] = useState(action.negativePrompt)
  const [autoAdd, setAutoAdd] = useState(action.autoAddToProject)
  useEffect(() => { setPrompt(action.prompt); setNegative(action.negativePrompt); setAutoAdd(action.autoAddToProject) }, [action])
  return <section className="border-t border-[var(--border-subtle)] px-5 py-5">
    <div className="flex items-start justify-between gap-3"><div><p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]"><WandSparkles size={16} className="text-[var(--accent)]" />待生成方案</p><p className="mt-1 text-xs leading-5 text-[var(--text-tertiary)]">可调整提示词，确认后才会扣除生图额度。</p></div><span className="rounded-full border border-amber-400/25 bg-amber-400/10 px-2 py-1 text-[10px] font-medium text-amber-300">等待确认</span></div>
    <details className="mt-4 rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)]" open><summary className="cursor-pointer px-3.5 py-3 text-xs font-medium text-[var(--text-secondary)]">专业提示词</summary><div className="border-t border-[var(--border-subtle)] p-3"><textarea value={prompt} onChange={(event) => setPrompt(event.target.value)} rows={7} className={cn(input, 'resize-y text-xs leading-5')} /><label className="mt-3 block text-[11px] text-[var(--text-tertiary)]">负面提示词<textarea value={negative} onChange={(event) => setNegative(event.target.value)} rows={3} className={cn(input, 'mt-1.5 resize-y text-xs leading-5')} /></label></div></details>
    <label className="mt-3 flex items-start gap-2 text-xs leading-5 text-[var(--text-secondary)]"><input type="checkbox" checked={autoAdd} onChange={(event) => setAutoAdd(event.target.checked)} className="mt-1" />生成完成后自动加入当前方案房间</label>
    {!bound && <p className="mt-3 rounded-xl border border-amber-400/20 bg-amber-400/8 p-3 text-xs leading-5 text-amber-200">请先在工作台顶部选择方案和房间，再确认生成。</p>}
    <button disabled={!bound || executing || !prompt.trim()} onClick={() => onExecute(prompt, negative, autoAdd)} className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl bg-[var(--accent)] px-4 py-3 text-sm font-semibold text-white shadow-[0_8px_24px_var(--accent-glow)] disabled:cursor-not-allowed disabled:opacity-45">{executing ? <Loader2 size={16} className="animate-spin" /> : <Sparkles size={16} />}{executing ? '正在创建任务…' : '确认并生成效果图'}</button>
  </section>
}

export default function AIAssistantPageV2() {
  const { conversationId } = useParams()
  const navigate = useNavigate()
  const authExpiresAt = useAppStore((state) => state.authUser?.expiresAt ?? 0)
  const activeId = Number(conversationId || 0)
  const [conversations, setConversations] = useState<AssistantConversationSummary[]>([])
  const [detail, setDetail] = useState<AssistantConversationDetail>(emptyDetail())
  const [projects, setProjects] = useState<Project[]>([])
  const [rooms, setRooms] = useState<Room[]>([])
  const [message, setMessage] = useState('')
  const [pendingUser, setPendingUser] = useState('')
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [thinkingSeconds, setThinkingSeconds] = useState(0)
  const [activeRun, setActiveRun] = useState<AssistantAgentRun | null>(null)
  const [executing, setExecuting] = useState(false)
  const [evaluatingResult, setEvaluatingResult] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [error, setError] = useState('')
  const [mediaError, setMediaError] = useState('')
  const [progressMode, setProgressMode] = useState<'connecting' | 'push' | 'polling'>('connecting')
  const [mediaRetryKey, setMediaRetryKey] = useState(0)
  const [job, setJob] = useState<AIJob | null>(null)
  const [results, setResults] = useState<Array<AIJobResult & { preview: string; original?: string }>>([])
  const [pendingAttachmentIds, setPendingAttachmentIds] = useState<number[]>([])
  const [attachmentPreviews, setAttachmentPreviews] = useState<Record<number, string>>({})
  const [uploadingAttachments, setUploadingAttachments] = useState(false)
  const [lightboxIndex, setLightboxIndex] = useState(-1)
  const messagesEnd = useRef<HTMLDivElement>(null)
  const attachmentInput = useRef<HTMLInputElement>(null)
  const refreshConversations = useCallback(() => assistantApi.getConversations().then(setConversations), [])
  const loadDetail = useCallback(async (id: number) => {
    if (!id) { setDetail(emptyDetail()); setJob(null); setResults([]); setLoading(false); return }
    setLoading(true); setError('')
    try {
      const value = await assistantApi.getConversation(id)
      setDetail(value)
      setPendingAttachmentIds(value.attachments.filter((item) => item.messageId === null).map((item) => item.attachmentId))
      setActiveRun(value.agentRuns[0] ?? null)
      const latest = [...value.actions].reverse().find((item) => item.jobId)
      if (latest?.jobId) setJob(await aiApi.getJob(latest.jobId)); else { setJob(null); setResults([]) }
    } catch (reason) { setError(formatAssistantError(reason, '加载对话')) } finally { setLoading(false) }
  }, [])

  useEffect(() => { void refreshConversations(); projectsApi.getUserProjects().then(setProjects).catch(() => setProjects([])) }, [refreshConversations])
  useEffect(() => { void loadDetail(activeId); setHistoryOpen(false) }, [activeId, loadDetail])
  useEffect(() => { const id = detail.conversation.projectId; if (!id) { setRooms([]); return } projectsApi.getProjectRooms(String(id)).then(setRooms).catch(() => setRooms([])) }, [detail.conversation.projectId])
  useEffect(() => {
    if (!activeId) return
    let active = true
    detail.attachments.forEach((item) => {
      if (attachmentPreviews[item.attachmentId]) return
      assistantApi.fetchAttachmentMedia(activeId, item.attachmentId)
        .then((url) => { if (active) setAttachmentPreviews((current) => ({ ...current, [item.attachmentId]: url })) })
        .catch((reason) => { if (active) setError(reason instanceof Error ? reason.message : '附件预览加载失败') })
    })
    return () => { active = false }
  }, [activeId, detail.attachments, attachmentPreviews])
  useEffect(() => { messagesEnd.current?.scrollIntoView({ behavior: 'auto', block: 'end' }) }, [detail.messages.length, pendingUser, sending])
  useEffect(() => {
    if (!sending) { setThinkingSeconds(0); return }
    const timer = window.setInterval(() => setThinkingSeconds((value) => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [sending])
  useEffect(() => {
    const jobId = job?.jobId
    if (!jobId) return
    let active = true; let timer: number | undefined; let emptyResultAttempts = 0; let pushConnected = false
    const poll = async () => {
      try {
        const current = await aiApi.getJob(jobId)
        if (!active) return
        setJob(current)
        if (current.status.toLowerCase() === 'succeeded') {
          const raw = await aiApi.getJobResults(jobId)
          if (!raw.length && emptyResultAttempts < 8) {
            emptyResultAttempts += 1
            timer = window.setTimeout(poll, 2000)
            return
          }
          const media = await Promise.all(raw.map(async (item) => ({
            ...item,
            preview: await aiApi.fetchJobResultMedia(jobId, item.aiImageID, 'thumbnail')
              .catch((reason) => {
                if (reason instanceof AuthenticatedMediaError && reason.status === 401) throw reason
                return aiApi.fetchJobResultMedia(jobId, item.aiImageID, 'original')
              }),
          })))
          if (active) { setMediaError(''); setResults(media) }
        }
        if (!terminal(current.status)) timer = window.setTimeout(poll, pushConnected ? 10000 : 2500)
      } catch (reason) { if (active) { const message = reason instanceof Error ? reason.message : '任务状态或结果图片获取失败'; setError(message); if (reason instanceof AuthenticatedMediaError) setMediaError(message) } }
    }
    setProgressMode('connecting')
    const unsubscribe = aiApi.subscribeJobProgress(jobId, (update) => {
      if (!active || update.jobId !== jobId) return
      setJob((current) => current?.jobId === jobId ? {
        ...current,
        status: update.status,
        progressValue: update.progressValue,
        errorMessage: update.errorMessage,
        updatedAt: update.updatedAt || current.updatedAt,
      } : current)
      if (terminal(update.status)) {
        if (timer) window.clearTimeout(timer)
        timer = window.setTimeout(poll, 0)
      }
    }, (connected) => {
      pushConnected = connected
      if (active) setProgressMode(connected ? 'push' : 'polling')
    })
    void poll()
    return () => { active = false; unsubscribe(); if (timer) window.clearTimeout(timer) }
  }, [job?.jobId, authExpiresAt, mediaRetryKey])

  const proposed = useMemo(() => [...detail.actions].reverse().find((item) => !item.jobId && ['proposed', 'failed'].includes(item.status)), [detail.actions])
  const currentAction = useMemo(() => [...detail.actions].reverse().find((item) => item.jobId && item.jobId === job?.jobId), [detail.actions, job?.jobId])
  const evaluation = useMemo(() => {
    for (const artifact of detail.agentArtifacts) {
      if (artifact.artifactType !== 'result_evaluation') continue
      try {
        const parsed = JSON.parse(artifact.contentJson) as Record<string, unknown>
        if (!currentAction || Number(parsed.actionId) === currentAction.actionId) return parsed
      } catch { /* Ignore a malformed historical artifact. */ }
    }
    return null
  }, [currentAction, detail.agentArtifacts])
  const visibleRun = activeRun ?? detail.agentRuns[0] ?? null
  const latestAgentEvent = visibleRun?.events[visibleRun.events.length - 1]
  const bound = Boolean(detail.conversation.projectId && detail.conversation.roomId)
  const lightbox: ImageItem[] = results.map((item) => ({ id: String(item.aiImageID), src: item.original ?? item.preview, label: 'AI 设计助手生成结果', source: 'ai' }))

  async function createConversation() { try { const created = await assistantApi.createConversation(); await refreshConversations(); navigate(`/app/assistant/${created.conversation.conversationId}`) } catch (reason) { setError(formatAssistantError(reason, '创建对话')) } }
  async function submit(event: FormEvent) {
    event.preventDefault(); const content = message.trim() || (pendingAttachmentIds.length ? '请分析我上传的图片，并据此给出设计方案。' : ''); if (!content || sending || uploadingAttachments) return
    const attachmentIds = [...pendingAttachmentIds]
    let id = activeId; const clientRequestId = requestId(); let polling = true; let pollTimer: number | undefined; setSending(true); setActiveRun(null); setError(''); setMessage(''); setPendingUser(content)
    const pollRun = async () => { if (!polling || !id) return; try { setActiveRun(await assistantApi.getAgentRun(id, clientRequestId)) } catch { /* The run may not exist until the backend finishes validation. */ } if (polling) pollTimer = window.setTimeout(pollRun, 700) }
    try { if (!id) { const created = await assistantApi.createConversation(); id = created.conversation.conversationId; navigate(`/app/assistant/${id}`, { replace: true }) } pollTimer = window.setTimeout(pollRun, 300); const response = await assistantApi.sendMessage(id, content, clientRequestId, attachmentIds); if (response.agentRun) setActiveRun(response.agentRun); setPendingAttachmentIds([]); await Promise.all([loadDetail(id), refreshConversations()]) }
    catch (reason) { setError(formatAssistantError(reason, '发送助手消息', clientRequestId)); setMessage(content) }
    finally { polling = false; if (pollTimer) window.clearTimeout(pollTimer); setPendingUser(''); setSending(false) }
  }
  async function bindProject(value: string) { if (!activeId) return; try { await assistantApi.updateBinding(activeId, value ? Number(value) : null, null); await loadDetail(activeId) } catch (reason) { setError(reason instanceof Error ? reason.message : '方案绑定失败') } }
  async function bindRoom(value: string) { if (!activeId) return; try { await assistantApi.updateBinding(activeId, detail.conversation.projectId, value ? Number(value) : null); await loadDetail(activeId) } catch (reason) { setError(reason instanceof Error ? reason.message : '房间绑定失败') } }
  async function uploadAttachments(files: FileList | null) {
    if (!files?.length || uploadingAttachments) return
    const remaining = 6 - pendingAttachmentIds.length
    if (remaining <= 0) { setError('每条消息最多上传 6 张图片。'); return }
    setUploadingAttachments(true); setError('')
    let id = activeId
    try {
      if (!id) {
        const created = await assistantApi.createConversation()
        id = created.conversation.conversationId
        navigate(`/app/assistant/${id}`, { replace: true })
      }
      const selected = Array.from(files).slice(0, remaining)
      const uploaded = []
      for (const file of selected) uploaded.push(await assistantApi.uploadAttachment(id, file, detail.conversation.roomId))
      setPendingAttachmentIds((current) => [...current, ...uploaded.map((item) => item.attachmentId)])
      await loadDetail(id)
    } catch (reason) { setError(formatAssistantError(reason, '上传设计素材')) }
    finally { setUploadingAttachments(false); if (attachmentInput.current) attachmentInput.current.value = '' }
  }
  async function removeAttachment(attachmentId: number) {
    if (!activeId || sending) return
    try {
      await assistantApi.deleteAttachment(activeId, attachmentId)
      setPendingAttachmentIds((current) => current.filter((id) => id !== attachmentId))
      setDetail((current) => ({ ...current, attachments: current.attachments.filter((item) => item.attachmentId !== attachmentId) }))
    } catch (reason) { setError(reason instanceof Error ? reason.message : '删除附件失败') }
  }
  async function execute(action: AssistantAction, prompt: string, negativePrompt: string, autoAdd: boolean) {
    if (!activeId) return; setExecuting(true); setError(''); setMediaError(''); setJob(null); setResults([])
    try { const response = await assistantApi.executeGeneration(activeId, action.actionId, { prompt, negativePrompt, autoAddToProject: autoAdd }); setResults([]); setJob(await aiApi.getJob(response.jobId)); await loadDetail(activeId) }
    catch (reason) { setError(formatAssistantError(reason, `提交生图动作（对话 ${activeId} / 动作 ${action.actionId}）`)) } finally { setExecuting(false) }
  }
  async function evaluateResult() {
    if (!activeId || !currentAction || !job?.jobId || evaluatingResult) return
    const clientRequestId = `result-eval-${currentAction.actionId}-${job.jobId}`.slice(0, 64)
    let polling = true; let pollTimer: number | undefined
    setEvaluatingResult(true); setError(''); setActiveRun(null)
    const pollRun = async () => { if (!polling) return; try { setActiveRun(await assistantApi.getAgentRun(activeId, clientRequestId)) } catch { /* Run is created after validation. */ } if (polling) pollTimer = window.setTimeout(pollRun, 700) }
    try {
      pollTimer = window.setTimeout(pollRun, 300)
      const response = await assistantApi.evaluateGeneration(activeId, currentAction.actionId, clientRequestId)
      setActiveRun(response.run)
      await loadDetail(activeId)
    } catch (reason) { setError(formatAssistantError(reason, '评估生成结果', clientRequestId)) }
    finally { polling = false; if (pollTimer) window.clearTimeout(pollTimer); setEvaluatingResult(false) }
  }
  async function openResult(index: number) { const item = results[index]; if (!item || !job?.jobId) return; if (!item.original) { try { const original = await aiApi.fetchJobResultMedia(job.jobId, item.aiImageID, 'original'); setMediaError(''); setResults((current) => current.map((value, i) => i === index ? { ...value, original } : value)) } catch (reason) { const message = reason instanceof Error ? reason.message : '原图加载失败'; setMediaError(message); setError(message); return } } setLightboxIndex(index) }
  async function removeConversation(id: number) { if (!window.confirm('删除这条设计对话？已生成的任务和方案图片不会被删除。')) return; try { await assistantApi.deleteConversation(id); await refreshConversations(); if (id === activeId) navigate('/app/assistant') } catch (reason) { setError(reason instanceof Error ? reason.message : '对话删除失败') } }

  return <div className="h-full min-h-0 bg-[var(--bg-base)] p-3 md:p-5">
    <div className="relative mx-auto grid h-full max-w-[1760px] min-h-0 gap-4 xl:grid-cols-[minmax(0,1fr)_430px]">
      <main className={cn(panel, 'flex min-h-[680px] min-w-0 flex-col overflow-hidden xl:min-h-0')}>
        <header className="flex shrink-0 items-center justify-between gap-3 border-b border-[var(--border-subtle)] px-4 py-3.5 md:px-5"><div className="flex min-w-0 items-center gap-3"><span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--accent)]/14 text-[var(--accent)]"><Bot size={20} /></span><div className="min-w-0"><h1 className="truncate text-[15px] font-semibold text-[var(--text-primary)]">{detail.conversation.title || 'AI 设计助手'}</h1><p className="mt-0.5 text-xs text-[var(--text-tertiary)]">一次追问 · 主动构思 · 生成效果图</p></div></div><div className="flex gap-2"><button onClick={() => setHistoryOpen(true)} className={secondary}><History size={15} /><span className="hidden sm:inline">历史对话</span></button><button onClick={() => void createConversation()} className={secondary}><MessageSquarePlus size={15} /><span className="hidden sm:inline">新对话</span></button></div></header>
        <div className="flex-1 space-y-5 overflow-y-auto px-4 py-6 md:px-7 md:py-8">
          {loading ? <div className="flex h-full items-center justify-center"><Loader2 className="animate-spin text-[var(--accent)]" /></div> : detail.messages.length || pendingUser ? <>
            {detail.messages.map((item) => <div key={item.messageId} className={cn('flex gap-3', item.role === 'user' ? 'justify-end' : 'justify-start')}>{item.role !== 'user' && <span className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-[var(--accent)]/12 text-[var(--accent)]"><Bot size={16} /></span>}<div className={cn('max-w-[min(86%,760px)] rounded-2xl px-4 py-3.5 text-[15px] leading-7 md:px-5', item.role === 'user' ? 'rounded-br-md bg-[var(--accent)] text-white shadow-[0_8px_24px_var(--accent-glow)]' : 'rounded-bl-md border border-[var(--border-default)] bg-[var(--bg-input)] text-[var(--text-primary)] shadow-sm')}>{item.role === 'user' ? <p className="whitespace-pre-wrap">{item.content}</p> : <FormattedMessage content={item.content} />}</div></div>)}
            {pendingUser && <div className="flex justify-end"><div className="max-w-[86%] rounded-2xl rounded-br-md bg-[var(--accent)] px-5 py-3.5 text-[15px] leading-7 text-white opacity-75">{pendingUser}</div></div>}
            {sending && <div className="flex items-start gap-3"><span className="flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--accent)]/12 text-[var(--accent)]"><Bot size={16} /></span><div className="w-full max-w-xl rounded-2xl rounded-bl-md border border-[var(--border-default)] bg-[var(--bg-input)] px-4 py-3"><div className="flex items-center gap-2 text-sm font-medium text-[var(--text-primary)]"><Loader2 size={15} className="animate-spin text-[var(--accent)]" />{latestAgentEvent?.title || thinkingText(thinkingSeconds)}</div>{latestAgentEvent && <p className="mt-1 text-xs leading-5 text-[var(--text-tertiary)]">{agentName(latestAgentEvent.agentId)}{latestAgentEvent.detail ? ` · ${latestAgentEvent.detail}` : ''}</p>}{visibleRun && <div className="mt-3 flex flex-wrap gap-1.5">{visibleRun.events.slice(-4).map((event) => <span key={event.eventId || event.sequence} className="rounded-full border border-[var(--border-subtle)] px-2 py-1 text-[10px] text-[var(--text-tertiary)]">{agentName(event.agentId)} · {event.title}</span>)}</div>}</div></div>}
            {(executing || (job && !terminal(job.status))) && <div className="flex items-center gap-3"><span className="flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--accent)]/12 text-[var(--accent)]"><WandSparkles size={16} /></span><div className="w-full max-w-md rounded-2xl rounded-bl-md border border-[var(--accent)]/20 bg-[var(--accent)]/8 px-4 py-3"><div className="flex items-center justify-between gap-3 text-sm"><span className="font-medium text-[var(--text-primary)]">{executing && !job ? '正在创建生图任务…' : `效果图${statusText(job?.status ?? 'created')}`}</span><span className="text-xs text-[var(--accent)]">{job?.progressValue ?? 1}%</span></div><div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[var(--bg-input)]"><div className={cn('h-full rounded-full bg-[var(--accent)] transition-[width] duration-300', executing && !job && 'animate-pulse')} style={{ width: `${Math.max(3, job?.progressValue ?? 8)}%` }} /></div><p className="mt-2 text-xs text-[var(--text-tertiary)]">进度会同步显示在右侧设计工作台</p></div></div>}
            {!sending && job?.status.toLowerCase() === 'succeeded' && results.length > 0 && <div className="flex items-center gap-3"><span className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-400/12 text-emerald-300"><Check size={16} /></span><button type="button" onClick={() => void openResult(0)} className="rounded-2xl rounded-bl-md border border-emerald-400/20 bg-emerald-400/8 px-4 py-3 text-left text-sm text-[var(--text-primary)]">效果图已生成，共 {results.length} 张。点击查看第一张，全部结果可在右侧工作台查看。</button></div>}
            <div ref={messagesEnd} />
          </> : <div className="flex h-full flex-col items-center justify-center px-4 text-center"><span className="flex h-16 w-16 items-center justify-center rounded-[22px] bg-[var(--accent)]/12 text-[var(--accent)] shadow-[0_10px_35px_var(--accent-glow)]"><Sparkles size={28} /></span><h2 className="mt-6 text-2xl font-semibold tracking-tight text-[var(--text-primary)]">没有完整想法也没关系</h2><p className="mt-3 max-w-lg text-[15px] leading-7 text-[var(--text-secondary)]">告诉我空间和大致偏好。我只会补问一次关键问题，然后给出明确方案并准备效果图。</p><div className="mt-6 flex max-w-xl flex-wrap justify-center gap-2">{['帮我构思一个温暖的客厅', '小户型卧室怎么显得更大', '给我一个有质感的日式方案'].map((text) => <button key={text} onClick={() => setMessage(text)} className={secondary}>{text}</button>)}</div></div>}
        </div>
        {error && <div className="mx-4 mb-2 whitespace-pre-wrap rounded-xl border border-red-400/25 bg-red-400/10 px-3.5 py-2.5 text-xs leading-5 text-red-300 md:mx-5">{error}</div>}
        <form onSubmit={submit} className="shrink-0 border-t border-[var(--border-subtle)] p-3.5 md:p-4">
          {pendingAttachmentIds.length > 0 && <div className="mb-2.5 flex gap-2 overflow-x-auto pb-1">{detail.attachments.filter((item) => pendingAttachmentIds.includes(item.attachmentId)).map((item) => <div key={item.attachmentId} className="relative h-20 w-24 shrink-0 overflow-hidden rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)]">{attachmentPreviews[item.attachmentId] ? <img src={attachmentPreviews[item.attachmentId]} alt={item.fileName} className="h-full w-full object-cover" /> : <span className="flex h-full items-center justify-center"><Loader2 size={15} className="animate-spin text-[var(--text-tertiary)]" /></span>}<button type="button" onClick={() => void removeAttachment(item.attachmentId)} className="absolute right-1 top-1 rounded-full bg-black/65 p-1 text-white" aria-label="移除图片"><X size={12} /></button><span className="absolute inset-x-1 bottom-1 truncate rounded bg-black/60 px-1 py-0.5 text-center text-[9px] text-white">{attachmentKindText(item.kind)}</span></div>)}</div>}
          <div className="flex items-end gap-2 rounded-2xl border border-[var(--border-default)] bg-[var(--bg-input)] p-2 focus-within:border-[var(--accent-border)] focus-within:ring-2 focus-within:ring-[var(--accent)]/10">
            <input ref={attachmentInput} type="file" accept="image/jpeg,image/png,image/webp" multiple className="hidden" onChange={(event) => void uploadAttachments(event.target.files)} />
            <button type="button" onClick={() => attachmentInput.current?.click()} disabled={sending || uploadingAttachments || pendingAttachmentIds.length >= 6} className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-[var(--text-secondary)] transition-colors hover:bg-[var(--bg-card)] hover:text-[var(--accent)] disabled:opacity-40" aria-label="上传房间照片、户型图或参考图">{uploadingAttachments ? <Loader2 size={18} className="animate-spin" /> : <Paperclip size={18} />}</button>
            <textarea value={message} onChange={(event) => setMessage(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); event.currentTarget.form?.requestSubmit() } }} rows={2} maxLength={4000} className="min-h-[58px] flex-1 resize-none bg-transparent px-2.5 py-2 text-[15px] leading-6 text-[var(--text-primary)] outline-none placeholder:text-[var(--text-placeholder)]" placeholder="描述空间，也可以上传房间、毛坯、户型或风格参考图…" />
            <button disabled={sending || uploadingAttachments || (!message.trim() && pendingAttachmentIds.length === 0)} aria-label="发送消息" className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--accent)] text-white disabled:opacity-40">{sending ? <Loader2 size={18} className="animate-spin" /> : <Send size={18} />}</button>
          </div>
          <p className="mt-2 px-1 text-[11px] text-[var(--text-tertiary)]">最多 6 张 JPEG、PNG 或 WebP，单张不超过 15 MB。视觉 Agent 会自动判断图片用途。</p>
        </form>
      </main>

      <aside className={cn(panel, 'min-h-[680px] overflow-y-auto xl:min-h-0')}>
        <div className="sticky top-0 z-10 border-b border-[var(--border-subtle)] bg-[var(--bg-card)]/95 px-5 py-4 backdrop-blur"><div className="flex items-center justify-between"><div><p className="flex items-center gap-2 text-[15px] font-semibold text-[var(--text-primary)]"><WandSparkles size={17} className="text-[var(--accent)]" />设计工作台</p><p className="mt-1 text-xs text-[var(--text-tertiary)]">从方案草案到生成结果都在这里完成</p></div>{bound && <span className="flex items-center gap-1 text-[10px] font-medium text-emerald-300"><Check size={12} />已绑定</span>}</div></div>
        <section className="px-5 py-5"><p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]"><FolderOpen size={15} className="text-[var(--accent)]" />保存位置</p><div className="mt-3 grid grid-cols-2 gap-2"><label className="text-[11px] text-[var(--text-tertiary)]">方案<select value={detail.conversation.projectId ?? ''} disabled={!activeId} onChange={(event) => void bindProject(event.target.value)} className={cn(input, 'mt-1.5')}><option value="">选择方案</option>{projects.map((item) => <option key={item.projectID} value={item.projectID}>{item.name}</option>)}</select></label><label className="text-[11px] text-[var(--text-tertiary)]">房间<select value={detail.conversation.roomId ?? ''} disabled={!detail.conversation.projectId} onChange={(event) => void bindRoom(event.target.value)} className={cn(input, 'mt-1.5')}><option value="">选择房间</option>{rooms.map((item) => <option key={item.roomID} value={item.roomID}>{item.name}</option>)}</select></label></div></section>
        {detail.rooms.length > 0 && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center justify-between"><p className="text-sm font-semibold text-[var(--text-primary)]">房间推进</p><span className="text-[10px] text-[var(--text-tertiary)]">一个对话可推进完整方案</span></div><div className="mt-3 flex gap-2 overflow-x-auto pb-1">{detail.rooms.map((room) => <button key={room.roomId} type="button" onClick={() => void bindRoom(String(room.roomId))} className={cn('min-w-[112px] rounded-xl border px-3 py-2.5 text-left transition-colors', room.selected ? 'border-[var(--accent-border)] bg-[var(--accent)]/10' : 'border-[var(--border-subtle)] bg-[var(--bg-input)] hover:border-[var(--border-strong)]')}><span className="block truncate text-xs font-semibold text-[var(--text-primary)]">{room.name}</span><span className={cn('mt-1 block text-[10px]', ['completed', 'design_ready', 'generation_ready'].includes(room.status) ? 'text-emerald-300' : room.status === 'generating' || room.status === 'analyzing' ? 'text-[var(--accent)]' : 'text-[var(--text-tertiary)]')}>{roomStatusText(room.status)}</span></button>)}</div></section>}
        {detail.attachments.length > 0 && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center justify-between"><p className="text-sm font-semibold text-[var(--text-primary)]">设计素材</p><span className="text-[10px] text-[var(--text-tertiary)]">{detail.attachments.length} 张</span></div><div className="mt-3 grid grid-cols-3 gap-2">{detail.attachments.slice(-6).map((item) => <div key={item.attachmentId} className="overflow-hidden rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)]"><div className="aspect-square">{attachmentPreviews[item.attachmentId] ? <img src={attachmentPreviews[item.attachmentId]} alt={item.fileName} className="h-full w-full object-cover" /> : <span className="flex h-full items-center justify-center"><Loader2 size={14} className="animate-spin text-[var(--text-tertiary)]" /></span>}</div><div className="truncate px-2 py-1.5 text-[9px] text-[var(--text-tertiary)]">{item.visionStatus === 'completed' ? attachmentKindText(item.kind) : item.messageId ? '等待视觉分析' : '待发送'}</div></div>)}</div></section>}
        <section className="border-t border-[var(--border-subtle)] px-5 py-5"><p className="text-sm font-semibold text-[var(--text-primary)]">当前设计摘要</p><div className="mt-4 space-y-3"><BriefItem label="空间" value={detail.brief.roomType} /><BriefItem label="面积" value={detail.brief.area} /><BriefItem label="风格" value={detail.brief.style} /><BriefItem label="色彩" value={detail.brief.colors} /><BriefItem label="材质" value={detail.brief.materials} /><BriefItem label="功能" value={detail.brief.requirements} /><BriefItem label="照明" value={detail.brief.lighting} /><BriefItem label="限制" value={detail.brief.constraints} />{!detail.brief.roomType && !detail.brief.style && <p className="rounded-xl bg-[var(--bg-input)] p-3 text-xs leading-5 text-[var(--text-tertiary)]">开始对话后，助手会把可执行的设计要点整理到这里。</p>}</div></section>
        {(sending || evaluatingResult) && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--accent)]/12 text-[var(--accent)]"><Loader2 size={18} className="animate-spin" /></span><div><p className="text-sm font-semibold text-[var(--text-primary)]">{latestAgentEvent?.title || (evaluatingResult ? '正在评估实际生成结果' : 'AI 正在准备工作台')}</p><p className="mt-1 text-xs text-[var(--text-tertiary)]">{latestAgentEvent ? `${agentName(latestAgentEvent.agentId)}正在处理` : evaluatingResult ? '先观察图片，再核对设计方案' : thinkingText(thinkingSeconds)}</p></div></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-[var(--bg-input)]"><div className="h-full animate-pulse rounded-full bg-[var(--accent)]" style={{ width: `${Math.min(90, 18 + (visibleRun?.events.length ?? 0) * 12)}%` }} /></div>{visibleRun && <div className="mt-4 space-y-2">{visibleRun.events.slice(-5).map((event) => <div key={event.eventId || event.sequence} className="flex gap-2 text-xs"><span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-[var(--accent)]" /><div><p className="font-medium text-[var(--text-secondary)]">{event.title}</p>{event.detail && <p className="mt-0.5 line-clamp-2 text-[11px] leading-4 text-[var(--text-tertiary)]">{event.detail}</p>}</div></div>)}</div>}</section>}
        {!sending && detail.agentArtifacts.length > 0 && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><p className="text-sm font-semibold text-[var(--text-primary)]">Agent 专业成果</p><div className="mt-3 space-y-2">{detail.agentArtifacts.slice(0, 4).map((artifact) => <details key={artifact.artifactId} className="rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)]"><summary className="cursor-pointer px-3 py-2.5 text-xs font-medium text-[var(--text-secondary)]">{artifact.title || artifact.artifactType} · v{artifact.version}</summary><pre className="max-h-56 overflow-auto whitespace-pre-wrap border-t border-[var(--border-subtle)] p-3 text-[10px] leading-4 text-[var(--text-tertiary)]">{(() => { try { return JSON.stringify(JSON.parse(artifact.contentJson), null, 2) } catch { return artifact.contentJson } })()}</pre></details>)}</div></section>}
        {!sending && proposed && <GenerationDraft action={proposed} bound={bound} executing={executing} onExecute={(prompt, negative, auto) => void execute(proposed, prompt, negative, auto)} />}
        {executing && !job && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center justify-between"><p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]"><Clock3 size={15} className="text-[var(--accent)]" />正在创建生图任务</p><Loader2 size={16} className="animate-spin text-[var(--accent)]" /></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-[var(--bg-input)]"><div className="h-full w-[8%] animate-pulse rounded-full bg-[var(--accent)]" /></div><p className="mt-3 text-xs leading-5 text-[var(--text-tertiary)]">正在校验额度、工作流和方案绑定，并提交到 AI 服务。</p></section>}
        {mediaError && <section className="border-t border-red-400/20 bg-red-400/8 px-5 py-4"><p className="text-sm font-semibold text-red-300">结果图片加载失败</p><p className="mt-1 break-all text-xs leading-5 text-red-200/80">{mediaError}</p><p className="mt-2 text-[11px] text-[var(--text-tertiary)]">请重新登录后重试；若仍为401，请在服务器日志中查找“JWT认证挑战”对应路径。</p><button type="button" onClick={() => { setMediaError(''); setError(''); setMediaRetryKey((value) => value + 1) }} className={cn(secondary, 'mt-3')}>重新读取图片</button></section>}
        {job && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center justify-between"><p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]"><Clock3 size={15} className="text-[var(--accent)]" />生图进度</p><span className={cn('rounded-full px-2 py-1 text-[10px] font-medium', job.status.toLowerCase() === 'succeeded' ? 'bg-emerald-400/10 text-emerald-300' : 'bg-[var(--accent)]/10 text-[var(--accent)]')}>{statusText(job.status)} · {progressMode === 'push' ? '实时推送' : progressMode === 'polling' ? '轮询兜底' : '连接中'}</span></div><div className="mt-4 h-2 overflow-hidden rounded-full bg-[var(--bg-input)]"><div className="h-full rounded-full bg-[var(--accent)] transition-[width] duration-300" style={{ width: `${Math.max(2, job.progressValue)}%` }} /></div><div className="mt-2 flex justify-between text-[11px] text-[var(--text-tertiary)]"><span>{statusText(job.status)}</span><span>{job.progressValue}%</span></div>{job.errorMessage && <p className="mt-3 rounded-xl bg-red-400/10 p-3 text-xs leading-5 text-red-300">{job.errorMessage}</p>}{results.length > 0 && <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-1 2xl:grid-cols-2">{results.map((item, index) => <button key={item.aiImageID} onClick={() => void openResult(index)} className="group overflow-hidden rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)]"><div className="relative aspect-[4/3] overflow-hidden"><img src={item.preview} alt="AI 生成效果图" onError={() => setMediaError('浏览器无法解码结果图片，请尝试打开原图。')} className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-[1.02]" /><span className="absolute inset-x-2 bottom-2 flex items-center justify-center gap-1.5 rounded-lg bg-black/55 px-2 py-1.5 text-[11px] text-white backdrop-blur-sm"><ImageIcon size={13} />查看大图</span></div></button>)}</div>}{!results.length && job.status.toLowerCase() === 'succeeded' && <p className="mt-3 rounded-xl bg-[var(--bg-input)] p-3 text-xs text-[var(--text-tertiary)]">任务已完成，正在读取结果图片…</p>}<button onClick={() => navigate(`/app/generate/text/jobs/${job.jobId}`)} className={cn(secondary, 'mt-4 w-full')}>进入完整任务页 <ChevronRight size={13} /></button></section>}
        {job?.status.toLowerCase() === 'succeeded' && results.length > 0 && !evaluation && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><p className="text-sm font-semibold text-[var(--text-primary)]">结果复核</p><p className="mt-1 text-xs leading-5 text-[var(--text-tertiary)]">视觉 Agent 会查看实际图片，再由评估 Agent 对照设计方案给出偏差和修改建议。只消耗助手 Token，不会自动再次生图。</p><button type="button" disabled={evaluatingResult} onClick={() => void evaluateResult()} className={cn(secondary, 'mt-3 w-full')}>{evaluatingResult ? <Loader2 size={14} className="animate-spin" /> : <Sparkles size={14} />}{evaluatingResult ? '正在评估…' : '让 AI 评估这组效果图'}</button></section>}
        {evaluation && <section className="border-t border-[var(--border-subtle)] px-5 py-5"><div className="flex items-center justify-between gap-3"><p className="text-sm font-semibold text-[var(--text-primary)]">效果图评估</p><span className="rounded-full bg-[var(--accent)]/10 px-2.5 py-1 text-xs font-semibold text-[var(--accent)]">{Number(evaluation.score ?? 0)} 分</span></div><p className="mt-3 text-xs leading-6 text-[var(--text-secondary)]">{String(evaluation.assistantText ?? '')}</p>{Array.isArray(evaluation.strengths) && evaluation.strengths.length > 0 && <div className="mt-4"><p className="text-[11px] font-semibold text-emerald-300">符合预期</p><ul className="mt-2 space-y-1.5">{evaluation.strengths.map((item, index) => <li key={index} className="flex gap-2 text-xs leading-5 text-[var(--text-secondary)]"><Check size={13} className="mt-1 shrink-0 text-emerald-300" />{String(item)}</li>)}</ul></div>}{Array.isArray(evaluation.issues) && evaluation.issues.length > 0 && <div className="mt-4"><p className="text-[11px] font-semibold text-amber-300">建议调整</p><div className="mt-2 space-y-2">{evaluation.issues.slice(0, 4).map((raw, index) => { const issue = raw && typeof raw === 'object' ? raw as Record<string, unknown> : {}; return <div key={index} className="rounded-xl bg-[var(--bg-input)] p-3"><p className="text-xs leading-5 text-[var(--text-secondary)]">{String(issue.description ?? '')}</p><p className="mt-1 text-[11px] leading-5 text-[var(--text-tertiary)]">{String(issue.suggestion ?? '')}</p></div> })}</div></div>}{evaluation.suggestedInstruction && <button type="button" onClick={() => setMessage(String(evaluation.suggestedInstruction))} className={cn(secondary, 'mt-4 w-full')}>把修改建议带入下一轮对话</button>}</section>}
        {!proposed && !job && <section className="border-t border-[var(--border-subtle)] px-5 py-8 text-center"><ImageIcon size={23} className="mx-auto text-[var(--text-placeholder)]" /><p className="mt-3 text-sm font-medium text-[var(--text-secondary)]">效果图工作区</p><p className="mt-1 text-xs leading-5 text-[var(--text-tertiary)]">助手提出方案后，提示词、生成进度和图片会显示在这里。</p></section>}
      </aside>

      {historyOpen && <div className="absolute inset-0 z-30 flex items-start rounded-[22px] bg-black/35 p-3 backdrop-blur-[2px] md:p-5" onMouseDown={(event) => { if (event.target === event.currentTarget) setHistoryOpen(false) }}><section className={cn(panel, 'flex max-h-[calc(100%-1rem)] w-full max-w-sm flex-col overflow-hidden')}><header className="flex items-center justify-between border-b border-[var(--border-subtle)] px-4 py-3.5"><div><p className="text-sm font-semibold text-[var(--text-primary)]">设计对话</p><p className="mt-0.5 text-[11px] text-[var(--text-tertiary)]">共 {conversations.length} 条</p></div><button onClick={() => setHistoryOpen(false)} className="rounded-lg p-2 text-[var(--text-secondary)] hover:bg-[var(--bg-input)]"><X size={16} /></button></header><div className="flex-1 space-y-1 overflow-y-auto p-2">{conversations.length ? conversations.map((item) => <div key={item.conversationId} className={cn('group flex items-center rounded-xl', item.conversationId === activeId && 'bg-[var(--accent)]/10')}><button onClick={() => navigate(`/app/assistant/${item.conversationId}`)} className="min-w-0 flex-1 p-3 text-left"><p className="truncate text-sm font-medium text-[var(--text-primary)]">{item.title}</p><p className="mt-1 truncate text-[11px] text-[var(--text-tertiary)]">{item.projectName ? `${item.projectName}${item.roomName ? ` · ${item.roomName}` : ''}` : '尚未绑定方案'}</p></button><button onClick={() => void removeConversation(item.conversationId)} className="mr-2 rounded-lg p-2 text-[var(--text-tertiary)] opacity-0 hover:bg-red-400/10 hover:text-red-300 group-hover:opacity-100"><Trash2 size={14} /></button></div>) : <p className="p-6 text-center text-xs text-[var(--text-tertiary)]">还没有设计对话</p>}</div><div className="border-t border-[var(--border-subtle)] p-3"><button onClick={() => void createConversation()} className="flex w-full items-center justify-center gap-2 rounded-xl bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white"><MessageSquarePlus size={15} />新建设计对话</button></div></section></div>}
    </div>
    <ImageLightbox images={lightbox} currentIndex={lightboxIndex} onClose={() => setLightboxIndex(-1)} onIndexChange={setLightboxIndex} />
  </div>
}
