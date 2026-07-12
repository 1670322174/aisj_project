import {
  Activity,
  AlertCircle,
  BarChart3,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleOff,
  Cloud,
  Database,
  FileImage,
  Gauge,
  KeyRound,
  ListFilter,
  Loader2,
  LockKeyhole,
  MonitorCog,
  Plus,
  RefreshCw,
  Search,
  Server,
  ShieldCheck,
  UploadCloud,
  Users,
  X,
} from 'lucide-react'
import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  adminApi,
  type AdminApiEndpoint,
  type AdminAuditLog,
  type AdminOverview,
  type AdminRole,
  type AdminSession,
  type AdminSystemInfo,
  type AdminTrendPoint,
  type AdminUserDetail,
  type AdminUserSummary,
} from '@/api/modules/admin'
import { cn } from '@/utils/cn'

type AdminTab = 'overview' | 'users' | 'gallery' | 'system' | 'audit'

const panelClass = 'rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] shadow-[var(--shadow-card)]'
const inputClass = 'w-full rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3.5 py-2.5 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)]'
const primaryButtonClass = 'inline-flex items-center justify-center gap-2 rounded-xl bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_6px_18px_var(--accent-glow)] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50'
const secondaryButtonClass = 'inline-flex items-center justify-center gap-2 rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3.5 py-2 text-sm font-medium text-[var(--text-secondary)] hover:border-[var(--border-strong)] hover:text-[var(--text-primary)] disabled:cursor-not-allowed disabled:opacity-50'

const EMPTY_OVERVIEW: AdminOverview = {
  users: { total: 0, enabled: 0, disabled: 0, newToday: 0, active24h: 0 },
  projects: { total: 0, newToday: 0 },
  gallery: { total: 0, newToday: 0 },
  aiJobs: { total: 0, today: 0, running: 0, succeeded: 0, failed: 0, successRate: 0, statusDistribution: [] },
  storage: { objects: 0, bytes: 0, displaySize: '' },
  trends: [],
  recentFailedJobs: [],
}

const TAB_ITEMS: Array<{ key: AdminTab; label: string; icon: typeof Gauge }> = [
  { key: 'overview', label: '数据概览', icon: Gauge },
  { key: 'users', label: '用户管理', icon: Users },
  { key: 'gallery', label: '图库管理', icon: FileImage },
  { key: 'system', label: '系统与接口', icon: Server },
  { key: 'audit', label: '审计日志', icon: Activity },
]

function formatNumber(value: number): string {
  return new Intl.NumberFormat('zh-CN').format(value || 0)
}

function formatBytes(value: number): string {
  if (!value) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const index = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1)
  return `${(value / 1024 ** index).toFixed(index > 1 ? 1 : 0)} ${units[index]}`
}

function formatDate(value: string, fallback = '暂无记录'): string {
  if (!value) return fallback
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function statusTone(value: string): string {
  const status = value.toLowerCase()
  if (['healthy', 'success', 'succeeded', 'completed', 'active', 'enabled'].some((item) => status.includes(item))) {
    return 'border-emerald-400/25 bg-emerald-400/10 text-emerald-300'
  }
  if (['failed', 'error', 'unhealthy', 'disabled', 'revoked'].some((item) => status.includes(item))) {
    return 'border-red-400/25 bg-red-400/10 text-red-300'
  }
  if (['running', 'pending', 'queued', 'degraded'].some((item) => status.includes(item))) {
    return 'border-amber-400/25 bg-amber-400/10 text-amber-300'
  }
  return 'border-[var(--border-default)] bg-[var(--bg-input)] text-[var(--text-secondary)]'
}

function StatusBadge({ value }: { value: string }) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-semibold', statusTone(value))}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {value || 'Unknown'}
    </span>
  )
}

function EmptyState({ icon, title, description }: { icon: ReactNode; title: string; description: string }) {
  return (
    <div className="flex min-h-44 flex-col items-center justify-center px-6 text-center">
      <div className="mb-3 rounded-2xl border border-[var(--border-default)] bg-[var(--bg-input)] p-3 text-[var(--text-tertiary)]">{icon}</div>
      <p className="font-semibold text-[var(--text-primary)]">{title}</p>
      <p className="mt-1 max-w-md text-xs leading-5 text-[var(--text-tertiary)]">{description}</p>
    </div>
  )
}

function SectionHeader({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h2 className="text-base font-semibold tracking-tight text-[var(--text-primary)]">{title}</h2>
        {description && <p className="mt-1 text-xs text-[var(--text-tertiary)]">{description}</p>}
      </div>
      {action}
    </div>
  )
}

function KpiCard({ icon, label, value, detail, accent }: { icon: ReactNode; label: string; value: string; detail: string; accent: string }) {
  return (
    <div className={cn(panelClass, 'relative overflow-hidden p-5')}>
      <div className={cn('absolute -right-6 -top-6 h-24 w-24 rounded-full opacity-10 blur-2xl', accent)} />
      <div className="relative flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-medium text-[var(--text-tertiary)]">{label}</p>
          <p className="mt-2 text-2xl font-semibold tracking-tight text-[var(--text-primary)]">{value}</p>
          <p className="mt-2 text-[11px] text-[var(--text-secondary)]">{detail}</p>
        </div>
        <div className={cn('rounded-xl p-2.5 text-white', accent)}>{icon}</div>
      </div>
    </div>
  )
}

function TrendChart({ points }: { points: AdminTrendPoint[] }) {
  const values = points.map((point) => point.jobs)
  const max = Math.max(1, ...values)
  const chartPoints = points.map((point, index) => {
    const x = points.length <= 1 ? 320 : 36 + index * (568 / (points.length - 1))
    const y = 160 - (point.jobs / max) * 126
    return { x, y, point }
  })
  const polyline = chartPoints.map(({ x, y }) => `${x},${y}`).join(' ')
  const area = chartPoints.length
    ? `36,160 ${polyline} ${chartPoints[chartPoints.length - 1].x},160`
    : ''

  if (!points.length) {
    return <EmptyState icon={<BarChart3 size={20} />} title="暂无趋势数据" description="开始产生用户和 AI 任务后，这里会显示近两周趋势。" />
  }

  return (
    <div className="w-full overflow-hidden">
      <svg viewBox="0 0 640 190" className="h-[210px] w-full" role="img" aria-label="近两周 AI 任务趋势">
        <defs>
          <linearGradient id="adminTrendGradient" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.32" />
            <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
          </linearGradient>
        </defs>
        {[34, 76, 118, 160].map((y) => (
          <line key={y} x1="36" x2="604" y1={y} y2={y} stroke="var(--border-subtle)" strokeWidth="1" />
        ))}
        <polygon points={area} fill="url(#adminTrendGradient)" />
        <polyline points={polyline} fill="none" stroke="var(--accent)" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
        {chartPoints.map(({ x, y, point }, index) => (
          <g key={`${point.date}-${index}`}>
            <circle cx={x} cy={y} r="4" fill="var(--bg-card)" stroke="var(--accent)" strokeWidth="2.5" />
            {(index === 0 || index === chartPoints.length - 1 || index % 3 === 0) && (
              <text x={x} y="181" textAnchor="middle" fontSize="9" fill="var(--text-tertiary)">
                {point.date ? point.date.slice(5) : ''}
              </text>
            )}
          </g>
        ))}
      </svg>
      <div className="flex flex-wrap items-center justify-center gap-5 text-[11px] text-[var(--text-secondary)]">
        <span className="flex items-center gap-2"><span className="h-0.5 w-5 rounded-full bg-[var(--accent)]" />任务总量</span>
        <span>峰值 {formatNumber(max)} 次/日</span>
      </div>
    </div>
  )
}

function StatusDistribution({ overview }: { overview: AdminOverview }) {
  const source = overview.aiJobs.statusDistribution.length
    ? overview.aiJobs.statusDistribution
    : [
        { status: 'Succeeded', count: overview.aiJobs.succeeded },
        { status: 'Failed', count: overview.aiJobs.failed },
        { status: 'Running', count: overview.aiJobs.running },
      ].filter((item) => item.count > 0)
  const total = Math.max(1, source.reduce((sum, item) => sum + item.count, 0))
  const colors = ['#34d399', '#f87171', '#fbbf24', '#60a5fa', '#a78bfa', '#94a3b8']
  let cursor = 0
  const gradient = source.map((item, index) => {
    const start = cursor
    cursor += item.count / total * 100
    return `${colors[index % colors.length]} ${start}% ${cursor}%`
  }).join(', ')

  return (
    <div className="flex min-h-[230px] flex-col items-center justify-center gap-6 sm:flex-row lg:flex-col xl:flex-row">
      <div
        className="relative h-36 w-36 shrink-0 rounded-full"
        style={{ background: source.length ? `conic-gradient(${gradient})` : 'var(--bg-input)' }}
      >
        <div className="absolute inset-[18px] flex flex-col items-center justify-center rounded-full bg-[var(--bg-card)]">
          <span className="text-2xl font-semibold text-[var(--text-primary)]">{overview.aiJobs.successRate.toFixed(0)}%</span>
          <span className="text-[10px] text-[var(--text-tertiary)]">成功率</span>
        </div>
      </div>
      <div className="w-full space-y-2.5">
        {source.length ? source.map((item, index) => (
          <div key={item.status} className="flex items-center gap-2.5 text-xs">
            <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: colors[index % colors.length] }} />
            <span className="flex-1 truncate text-[var(--text-secondary)]">{item.status}</span>
            <span className="font-semibold text-[var(--text-primary)]">{formatNumber(item.count)}</span>
          </div>
        )) : <p className="text-center text-xs text-[var(--text-tertiary)]">暂无任务数据</p>}
      </div>
    </div>
  )
}

function OverviewPanel({ refreshKey }: { refreshKey: number }) {
  const [overview, setOverview] = useState<AdminOverview>(EMPTY_OVERVIEW)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    adminApi.getOverview(14)
      .then((value) => active && setOverview(value))
      .catch((reason) => active && setError(reason instanceof Error ? reason.message : '概览加载失败'))
      .finally(() => active && setLoading(false))
    return () => { active = false }
  }, [refreshKey])

  if (loading) return <PageLoading label="正在汇总网站数据…" />
  if (error) return <ErrorPanel message={error} />

  const storage = overview.storage.displaySize || formatBytes(overview.storage.bytes)
  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard icon={<Users size={19} />} label="用户总数" value={formatNumber(overview.users.total)} detail={`今日新增 ${overview.users.newToday} · 24h 活跃 ${overview.users.active24h}`} accent="bg-blue-500" />
        <KpiCard icon={<MonitorCog size={19} />} label="方案总数" value={formatNumber(overview.projects.total)} detail={`今日新建 ${overview.projects.newToday} 个方案`} accent="bg-violet-500" />
        <KpiCard icon={<BarChart3 size={19} />} label="AI 任务" value={formatNumber(overview.aiJobs.total)} detail={`今日 ${overview.aiJobs.today} · 运行中 ${overview.aiJobs.running}`} accent="bg-emerald-500" />
        <KpiCard icon={<Cloud size={19} />} label="图库存储" value={storage} detail={`${formatNumber(overview.storage.objects || overview.gallery.total)} 个对象`} accent="bg-cyan-500" />
      </div>

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1.7fr)_minmax(300px,0.8fr)]">
        <section className={cn(panelClass, 'p-5')}>
          <SectionHeader title="近 14 天任务趋势" description="按任务创建日期聚合，便于观察使用峰值和增长变化。" />
          <TrendChart points={overview.trends} />
        </section>
        <section className={cn(panelClass, 'p-5')}>
          <SectionHeader title="任务状态分布" description={`累计成功 ${formatNumber(overview.aiJobs.succeeded)}，失败 ${formatNumber(overview.aiJobs.failed)}`} />
          <StatusDistribution overview={overview} />
        </section>
      </div>

      <div className="grid gap-5 xl:grid-cols-3">
        <section className={cn(panelClass, 'p-5 xl:col-span-2')}>
          <SectionHeader title="最近失败任务" description="快速定位 Provider、输入和工作流异常。" />
          {overview.recentFailedJobs.length ? (
            <div className="divide-y divide-[var(--border-subtle)]">
              {overview.recentFailedJobs.slice(0, 6).map((job) => (
                <div key={job.jobId} className="grid gap-2 py-3 first:pt-0 sm:grid-cols-[minmax(0,1fr)_120px_160px] sm:items-center">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-[var(--text-primary)]">{job.prompt || job.workflowCode || job.jobId}</p>
                    <p className="mt-1 truncate text-[11px] text-red-300/80">{job.errorMessage || '未记录错误详情'}</p>
                  </div>
                  <p className="text-xs text-[var(--text-secondary)]">{job.username || '未知用户'}</p>
                  <p className="text-xs text-[var(--text-tertiary)] sm:text-right">{formatDate(job.createdAt)}</p>
                </div>
              ))}
            </div>
          ) : <EmptyState icon={<CheckCircle2 size={20} />} title="近期没有失败任务" description="AI 任务运行稳定。" />}
        </section>
        <section className={cn(panelClass, 'p-5')}>
          <SectionHeader title="运行提示" />
          <div className="space-y-3 text-xs leading-5 text-[var(--text-secondary)]">
            <div className="rounded-xl border border-emerald-400/20 bg-emerald-400/8 p-3">
              <p className="font-semibold text-emerald-300">账号状态</p>
              <p className="mt-1">{overview.users.enabled} 个启用，{overview.users.disabled} 个禁用。</p>
            </div>
            <div className="rounded-xl border border-blue-400/20 bg-blue-400/8 p-3">
              <p className="font-semibold text-blue-300">普通图库</p>
              <p className="mt-1">当前记录 {formatNumber(overview.gallery.total)} 张，今日新增 {overview.gallery.newToday} 张。</p>
            </div>
            <div className="rounded-xl border border-amber-400/20 bg-amber-400/8 p-3">
              <p className="font-semibold text-amber-300">失败率</p>
              <p className="mt-1">建议当失败率持续高于 10% 时检查 AI Provider 与后台任务日志。</p>
            </div>
          </div>
        </section>
      </div>
    </div>
  )
}

function PageLoading({ label }: { label: string }) {
  return (
    <div className="flex min-h-[440px] flex-col items-center justify-center gap-3 text-[var(--text-tertiary)]">
      <Loader2 size={24} className="animate-spin text-[var(--accent)]" />
      <p className="text-sm">{label}</p>
    </div>
  )
}

function ErrorPanel({ message }: { message: string }) {
  return (
    <div className={cn(panelClass, 'flex min-h-52 flex-col items-center justify-center p-8 text-center')}>
      <AlertCircle size={24} className="text-red-400" />
      <p className="mt-3 font-semibold text-[var(--text-primary)]">数据加载失败</p>
      <p className="mt-1 max-w-lg text-xs leading-5 text-[var(--text-tertiary)]">{message}</p>
    </div>
  )
}

function ModalShell({ title, description, onClose, children }: { title: string; description?: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" onMouseDown={onClose}>
      <div className={cn(panelClass, 'max-h-[90vh] w-full max-w-lg overflow-y-auto bg-[var(--bg-card)] p-5 animate-soft-pop')} onMouseDown={(event) => event.stopPropagation()}>
        <div className="mb-5 flex items-start justify-between gap-4">
          <div>
            <h3 className="text-lg font-semibold text-[var(--text-primary)]">{title}</h3>
            {description && <p className="mt-1 text-xs text-[var(--text-tertiary)]">{description}</p>}
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-[var(--text-tertiary)] hover:bg-[var(--bg-input)] hover:text-[var(--text-primary)]" aria-label="关闭">
            <X size={17} />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

function CreateUserModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [username, setUsername] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<AdminRole>('FreeUser')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (password.length < 10) {
      setError('密码至少需要 10 位。')
      return
    }
    setSubmitting(true)
    setError('')
    try {
      await adminApi.createUser({ username: username.trim(), phoneNumber: phoneNumber.trim(), password, role })
      onCreated()
      onClose()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : '新用户创建失败')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <ModalShell title="添加新用户" description="账号由管理员创建，用户无需也无法公开注册。" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        <label className="block text-xs font-medium text-[var(--text-secondary)]">用户名
          <input required value={username} onChange={(event) => setUsername(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="请输入唯一用户名" autoFocus />
        </label>
        <label className="block text-xs font-medium text-[var(--text-secondary)]">手机号（可选）
          <input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="用于联系和账号识别" />
        </label>
        <label className="block text-xs font-medium text-[var(--text-secondary)]">初始密码
          <input required type="password" value={password} onChange={(event) => setPassword(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="至少 10 位" />
        </label>
        <label className="block text-xs font-medium text-[var(--text-secondary)]">账号权限
          <select value={role} onChange={(event) => setRole(event.target.value as AdminRole)} className={cn(inputClass, 'mt-1.5')}>
            <option value="FreeUser">免费用户</option>
            <option value="Member">会员用户</option>
            <option value="PremiumMember">高级会员</option>
            <option value="Administrator">管理员</option>
          </select>
        </label>
        {error && <p className="rounded-xl border border-red-400/20 bg-red-400/8 px-3 py-2 text-xs text-red-300">{error}</p>}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" onClick={onClose} className={secondaryButtonClass}>取消</button>
          <button type="submit" disabled={submitting} className={primaryButtonClass}>{submitting && <Loader2 size={15} className="animate-spin" />}创建账号</button>
        </div>
      </form>
    </ModalShell>
  )
}

function UserDetailDrawer({ userId, onClose, onChanged }: { userId: string; onClose: () => void; onChanged: () => void }) {
  const [detail, setDetail] = useState<AdminUserDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [password, setPassword] = useState('')
  const [savingPassword, setSavingPassword] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setDetail(await adminApi.getUserDetail(userId))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : '用户详情加载失败')
    } finally {
      setLoading(false)
    }
  }, [userId])

  useEffect(() => { void load() }, [load])

  async function revoke(session: AdminSession) {
    if (!window.confirm(`确定撤销该设备会话（${session.ipAddress || '未知 IP'}）吗？`)) return
    await adminApi.revokeSession(userId, session.sessionId)
    await load()
  }

  async function revokeAll() {
    if (!window.confirm('确定让该用户的所有设备立即退出登录吗？')) return
    await adminApi.revokeAllSessions(userId)
    await load()
  }

  async function resetPassword() {
    if (password.length < 10) {
      setError('新密码至少需要 10 位。')
      return
    }
    setSavingPassword(true)
    try {
      await adminApi.resetUserPassword(userId, password)
      setPassword('')
      await load()
      onChanged()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : '密码重置失败')
    } finally {
      setSavingPassword(false)
    }
  }

  return (
    <div className="fixed inset-0 z-[85] bg-black/55 backdrop-blur-sm" onMouseDown={onClose}>
      <aside className="absolute inset-y-0 right-0 w-full max-w-2xl overflow-y-auto border-l border-[var(--border-default)] bg-[var(--bg-base)] shadow-2xl animate-slide-up sm:animate-soft-pop" onMouseDown={(event) => event.stopPropagation()}>
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-[var(--border-default)] bg-[var(--bg-base)]/95 px-5 py-4 backdrop-blur-xl">
          <div>
            <p className="text-sm font-semibold text-[var(--text-primary)]">用户管理详情</p>
            <p className="mt-0.5 text-[11px] text-[var(--text-tertiary)]">账号、使用行为和登录设备</p>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-[var(--text-tertiary)] hover:bg-[var(--bg-card)] hover:text-[var(--text-primary)]"><X size={18} /></button>
        </div>
        {loading ? <PageLoading label="正在读取用户资料…" /> : error && !detail ? <ErrorPanel message={error} /> : detail ? (
          <div className="space-y-5 p-5">
            <section className={cn(panelClass, 'p-5')}>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="flex items-center gap-3">
                  <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--accent)] text-base font-bold text-white">{detail.summary.username.slice(0, 1).toUpperCase()}</div>
                  <div>
                    <p className="font-semibold text-[var(--text-primary)]">{detail.summary.username}</p>
                    <p className="text-xs text-[var(--text-tertiary)]">{detail.summary.phone || '未填写手机号'}</p>
                  </div>
                </div>
                <div className="flex gap-2"><StatusBadge value={detail.summary.role} /><StatusBadge value={detail.summary.isEnabled ? 'Enabled' : 'Disabled'} /></div>
              </div>
              <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
                {[
                  ['方案', detail.counts.projects], ['任务', detail.counts.jobs], ['图片', detail.counts.images], ['设备', detail.counts.sessions],
                ].map(([label, value]) => <div key={String(label)} className="rounded-xl bg-[var(--bg-input)] p-3 text-center"><p className="text-lg font-semibold text-[var(--text-primary)]">{value}</p><p className="text-[10px] text-[var(--text-tertiary)]">{label}</p></div>)}
              </div>
              <div className="mt-4 rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)] p-3">
                <div className="flex items-center justify-between text-xs"><span className="text-[var(--text-secondary)]">AI 额度</span><span className="font-medium text-[var(--text-primary)]">{detail.quota.available} / {detail.quota.total}</span></div>
                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[var(--border-default)]"><div className="h-full rounded-full bg-[var(--accent)]" style={{ width: `${detail.quota.total ? Math.min(100, detail.quota.available / detail.quota.total * 100) : 0}%` }} /></div>
              </div>
            </section>

            <section className={cn(panelClass, 'p-5')}>
              <SectionHeader title="重置密码" description="保存后会撤销该用户的所有登录会话。" />
              <div className="flex flex-col gap-2 sm:flex-row">
                <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} className={inputClass} placeholder="输入至少 10 位的新密码" />
                <button type="button" onClick={() => void resetPassword()} disabled={savingPassword} className={primaryButtonClass}>{savingPassword ? <Loader2 size={15} className="animate-spin" /> : <KeyRound size={15} />}重置</button>
              </div>
              {error && <p className="mt-2 text-xs text-red-300">{error}</p>}
            </section>

            <section className={cn(panelClass, 'p-5')}>
              <SectionHeader title="登录设备" description="可撤销单台设备或强制所有设备退出。" action={<button type="button" onClick={() => void revokeAll()} className={secondaryButtonClass}><LockKeyhole size={14} />全部下线</button>} />
              {detail.sessions.length ? <div className="space-y-2">
                {detail.sessions.map((session) => (
                  <div key={session.sessionId} className="rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)] p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0"><p className="truncate text-xs font-medium text-[var(--text-primary)]">{session.userAgent || '未知浏览器'}</p><p className="mt-1 text-[11px] text-[var(--text-tertiary)]">IP {session.ipAddress || '未知'} · 最近 {formatDate(session.lastUsedAt)}</p></div>
                      <button type="button" disabled={session.isRevoked} onClick={() => void revoke(session)} className="shrink-0 rounded-lg px-2.5 py-1.5 text-[11px] text-red-300 hover:bg-red-400/10 disabled:opacity-40">撤销</button>
                    </div>
                  </div>
                ))}
              </div> : <EmptyState icon={<MonitorCog size={18} />} title="暂无有效设备" description="该用户当前没有可撤销的登录会话。" />}
            </section>

            <section className={cn(panelClass, 'p-5')}>
              <SectionHeader title="最近行为" description="登录、方案和 AI 任务等关键行为。" />
              {detail.recentActivities.length ? <div className="relative space-y-4 before:absolute before:bottom-2 before:left-[5px] before:top-2 before:w-px before:bg-[var(--border-default)]">
                {detail.recentActivities.map((activity) => <div key={activity.id || `${activity.action}-${activity.createdAt}`} className="relative flex gap-3"><span className="relative z-10 mt-1 h-2.5 w-2.5 shrink-0 rounded-full border-2 border-[var(--bg-card)] bg-[var(--accent)]" /><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><p className="text-xs font-medium text-[var(--text-primary)]">{activity.action}</p><StatusBadge value={activity.result} /></div><p className="mt-1 text-xs text-[var(--text-secondary)]">{activity.description}</p><p className="mt-1 text-[10px] text-[var(--text-tertiary)]">{formatDate(activity.createdAt)} · {activity.ipAddress || '未知 IP'}</p></div></div>)}
              </div> : <EmptyState icon={<Activity size={18} />} title="暂无行为记录" description="关键账号行为将显示在这里。" />}
            </section>
          </div>
        ) : null}
      </aside>
    </div>
  )
}

function UsersPanel({ refreshKey, notify }: { refreshKey: number; notify: (message: string, type?: 'success' | 'error') => void }) {
  const [users, setUsers] = useState<AdminUserSummary[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [detailUserId, setDetailUserId] = useState('')
  const [actionUserId, setActionUserId] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => { setPage(1) }, [search, role, status])
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setLoading(true)
      setError('')
      adminApi.getUsers({ search: search.trim(), page, pageSize: 20, role: role || undefined, isEnabled: status ? status === 'enabled' : undefined })
        .then((result) => { setUsers(result.items); setTotalPages(result.totalPages); setTotalCount(result.totalCount) })
        .catch((reason) => setError(reason instanceof Error ? reason.message : '用户列表加载失败'))
        .finally(() => setLoading(false))
    }, search ? 300 : 0)
    return () => window.clearTimeout(timer)
  }, [search, page, role, status, refreshKey, reloadKey])

  async function changeRole(user: AdminUserSummary, nextRole: AdminRole) {
    if (user.role === nextRole) return
    const roleLabels: Record<AdminRole, string> = { FreeUser: '免费用户', Member: '会员用户', PremiumMember: '高级会员', Administrator: '管理员' }
    if (!window.confirm(`确定将 ${user.username} 的权限修改为 ${roleLabels[nextRole]}吗？`)) return
    setActionUserId(user.userId)
    try {
      await adminApi.setUserRole(user.userId, nextRole)
      notify('账号权限已更新')
      setReloadKey((value) => value + 1)
    } catch (reason) {
      notify(reason instanceof Error ? reason.message : '权限更新失败', 'error')
    } finally { setActionUserId('') }
  }

  async function toggleStatus(user: AdminUserSummary) {
    const next = !user.isEnabled
    if (!window.confirm(`确定${next ? '启用' : '禁用'}账号 ${user.username} 吗？${next ? '' : '该用户的登录会话将被撤销。'}`)) return
    setActionUserId(user.userId)
    try {
      await adminApi.setUserStatus(user.userId, next)
      notify(next ? '账号已启用' : '账号已禁用并下线')
      setReloadKey((value) => value + 1)
    } catch (reason) {
      notify(reason instanceof Error ? reason.message : '账号状态更新失败', 'error')
    } finally { setActionUserId('') }
  }

  return (
    <div className="space-y-4">
      <section className={cn(panelClass, 'p-4 sm:p-5')}>
        <SectionHeader title="用户与权限" description={`共 ${formatNumber(totalCount)} 个账号，可管理权限、状态、密码和登录设备。`} action={<button className={primaryButtonClass} onClick={() => setCreateOpen(true)}><Plus size={16} />添加用户</button>} />
        <div className="grid gap-2 md:grid-cols-[minmax(220px,1fr)_180px_180px]">
          <label className="relative"><Search size={15} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--text-tertiary)]" /><input value={search} onChange={(event) => setSearch(event.target.value)} className={cn(inputClass, 'pl-10')} placeholder="搜索用户名、手机号或用户 ID" /></label>
          <select value={role} onChange={(event) => setRole(event.target.value)} className={inputClass}><option value="">全部权限</option><option value="FreeUser">免费用户</option><option value="Member">会员用户</option><option value="PremiumMember">高级会员</option><option value="Administrator">管理员</option></select>
          <select value={status} onChange={(event) => setStatus(event.target.value)} className={inputClass}><option value="">全部状态</option><option value="enabled">已启用</option><option value="disabled">已禁用</option></select>
        </div>
      </section>

      <section className={cn(panelClass, 'overflow-hidden')}>
        {loading ? <PageLoading label="正在加载用户…" /> : error ? <ErrorPanel message={error} /> : users.length ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-left">
                <thead className="border-b border-[var(--border-default)] bg-[var(--bg-input)]/65 text-[11px] uppercase tracking-wider text-[var(--text-tertiary)]"><tr><th className="px-5 py-3 font-semibold">用户</th><th className="px-4 py-3 font-semibold">权限</th><th className="px-4 py-3 font-semibold">状态</th><th className="px-4 py-3 font-semibold">业务数据</th><th className="px-4 py-3 font-semibold">最后登录</th><th className="px-5 py-3 text-right font-semibold">操作</th></tr></thead>
                <tbody className="divide-y divide-[var(--border-subtle)]">
                  {users.map((user) => (
                    <tr key={user.userId} className="hover:bg-[var(--bg-input)]/55">
                      <td className="px-5 py-3.5"><button className="flex items-center gap-3 text-left" onClick={() => setDetailUserId(user.userId)}><span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--accent)]/15 text-xs font-bold text-[var(--accent)]">{user.username.slice(0, 1).toUpperCase()}</span><span><span className="block text-sm font-medium text-[var(--text-primary)]">{user.username}</span><span className="block text-[11px] text-[var(--text-tertiary)]">{user.phone || user.userId}</span></span></button></td>
                      <td className="px-4 py-3.5"><select value={user.role} disabled={actionUserId === user.userId} onChange={(event) => void changeRole(user, event.target.value as AdminRole)} className="w-36 rounded-lg border border-[var(--border-default)] bg-[var(--bg-input)] px-2.5 py-1.5 text-xs text-[var(--text-primary)]"><option value="FreeUser">免费用户</option><option value="Member">会员用户</option><option value="PremiumMember">高级会员</option><option value="Administrator">管理员</option></select></td>
                      <td className="px-4 py-3.5"><StatusBadge value={user.isEnabled ? 'Enabled' : 'Disabled'} /></td>
                      <td className="px-4 py-3.5 text-xs text-[var(--text-secondary)]">{user.projectCount} 方案 · {user.jobCount} 任务 · {user.sessionCount} 设备</td>
                      <td className="px-4 py-3.5 text-xs text-[var(--text-secondary)]">{formatDate(user.lastLoginAt)}</td>
                      <td className="px-5 py-3.5"><div className="flex items-center justify-end gap-1.5"><button type="button" onClick={() => setDetailUserId(user.userId)} className="rounded-lg px-2.5 py-1.5 text-xs text-[var(--text-secondary)] hover:bg-[var(--bg-card-hover)] hover:text-[var(--text-primary)]">详情</button><button type="button" disabled={actionUserId === user.userId} onClick={() => void toggleStatus(user)} className={cn('rounded-lg px-2.5 py-1.5 text-xs', user.isEnabled ? 'text-red-300 hover:bg-red-400/10' : 'text-emerald-300 hover:bg-emerald-400/10')}>{actionUserId === user.userId ? <Loader2 size={13} className="animate-spin" /> : user.isEnabled ? '禁用' : '启用'}</button></div></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex flex-col gap-3 border-t border-[var(--border-default)] px-5 py-3 sm:flex-row sm:items-center sm:justify-between"><p className="text-xs text-[var(--text-tertiary)]">第 {page} / {totalPages} 页，共 {formatNumber(totalCount)} 条</p><div className="flex gap-2"><button disabled={page <= 1} onClick={() => setPage((value) => value - 1)} className={secondaryButtonClass}><ChevronLeft size={14} />上一页</button><button disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)} className={secondaryButtonClass}>下一页<ChevronRight size={14} /></button></div></div>
          </>
        ) : <EmptyState icon={<Users size={21} />} title="没有匹配的用户" description="调整搜索或筛选条件，或创建一个新账号。" />}
      </section>
      {createOpen && <CreateUserModal onClose={() => setCreateOpen(false)} onCreated={() => { notify('新用户已创建'); setReloadKey((value) => value + 1) }} />}
      {detailUserId && <UserDetailDrawer userId={detailUserId} onClose={() => setDetailUserId('')} onChanged={() => setReloadKey((value) => value + 1)} />}
    </div>
  )
}

function GalleryPanel({ notify }: { notify: (message: string, type?: 'success' | 'error') => void }) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState('')
  const [roomType, setRoomType] = useState('客厅')
  const [houseType, setHouseType] = useState('')
  const [style, setStyle] = useState('')
  const [material, setMaterial] = useState('')
  const [elements, setElements] = useState('')
  const [other, setOther] = useState('')
  const [uploading, setUploading] = useState(false)

  useEffect(() => {
    if (!file) { setPreview(''); return }
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!file) { notify('请先选择图片文件', 'error'); return }
    setUploading(true)
    try {
      await adminApi.uploadGalleryImage({ file, roomType, houseType, style, material, elements, other })
      notify('图片已上传到普通图库并写入数据库')
      setFile(null); setStyle(''); setMaterial(''); setElements(''); setOther('')
      if (fileRef.current) fileRef.current.value = ''
    } catch (reason) {
      notify(reason instanceof Error ? reason.message : '图片上传失败', 'error')
    } finally { setUploading(false) }
  }

  return (
    <div className="grid gap-5 xl:grid-cols-[minmax(0,1.25fr)_minmax(320px,0.75fr)]">
      <section className={cn(panelClass, 'p-5')}>
        <SectionHeader title="上传到 COS 普通图库" description="后端会校验管理员权限、文件类型与大小，并同步创建缩略图和数据库记录。" />
        <form onSubmit={submit} className="space-y-4">
          <button type="button" onClick={() => fileRef.current?.click()} className="group flex min-h-60 w-full flex-col items-center justify-center overflow-hidden rounded-2xl border border-dashed border-[var(--border-strong)] bg-[var(--bg-input)] hover:border-[var(--accent-border)]">
            {preview ? <img src={preview} alt="上传预览" className="h-60 w-full object-contain" /> : <><span className="rounded-2xl bg-[var(--accent)]/12 p-4 text-[var(--accent)] group-hover:scale-105"><UploadCloud size={26} /></span><p className="mt-4 text-sm font-semibold text-[var(--text-primary)]">点击选择图片</p><p className="mt-1 text-xs text-[var(--text-tertiary)]">JPG、PNG、WebP；实际大小限制以后端配置为准</p></>}
          </button>
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
          {file && <div className="flex items-center justify-between rounded-xl border border-[var(--border-subtle)] bg-[var(--bg-input)] px-3 py-2 text-xs"><span className="truncate text-[var(--text-secondary)]">{file.name} · {formatBytes(file.size)}</span><button type="button" onClick={() => setFile(null)} className="ml-3 text-red-300">移除</button></div>}
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-xs text-[var(--text-secondary)]">房间类型 *<input required value={roomType} onChange={(event) => setRoomType(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="例如：客厅" /></label>
            <label className="text-xs text-[var(--text-secondary)]">户型<input value={houseType} onChange={(event) => setHouseType(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="例如：三室两厅" /></label>
            <label className="text-xs text-[var(--text-secondary)]">风格<input value={style} onChange={(event) => setStyle(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="现代、原木、极简…" /></label>
            <label className="text-xs text-[var(--text-secondary)]">材质<input value={material} onChange={(event) => setMaterial(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="木材、石材、金属…" /></label>
            <label className="text-xs text-[var(--text-secondary)] sm:col-span-2">设计元素<input value={elements} onChange={(event) => setElements(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="落地窗、岛台、吊灯等，逗号分隔" /></label>
            <label className="text-xs text-[var(--text-secondary)] sm:col-span-2">其他标签<input value={other} onChange={(event) => setOther(event.target.value)} className={cn(inputClass, 'mt-1.5')} placeholder="补充可被图库搜索匹配的关键词" /></label>
          </div>
          <button type="submit" disabled={!file || uploading} className={cn(primaryButtonClass, 'w-full sm:w-auto')}>{uploading ? <Loader2 size={16} className="animate-spin" /> : <UploadCloud size={16} />}{uploading ? '正在上传并生成缩略图…' : '上传到普通图库'}</button>
        </form>
      </section>
      <aside className="space-y-5">
        <section className={cn(panelClass, 'p-5')}><SectionHeader title="上传检查清单" /><div className="space-y-3 text-xs leading-5 text-[var(--text-secondary)]">{['确认图片拥有合法使用权', '房间和风格信息尽量准确', '避免上传包含个人隐私的原图', '上传成功后到普通图库搜索验证', '若云端返回 Forbidden，检查普通图库 Bucket 权限与区域配置'].map((item) => <p key={item} className="flex gap-2"><CheckCircle2 size={15} className="mt-0.5 shrink-0 text-emerald-400" />{item}</p>)}</div></section>
        <section className="rounded-2xl border border-amber-400/20 bg-amber-400/8 p-5"><p className="flex items-center gap-2 text-sm font-semibold text-amber-300"><AlertCircle size={17} />上传链路</p><p className="mt-2 text-xs leading-5 text-amber-100/65">浏览器只把文件交给本站后端；COS 密钥始终留在服务器。后端上传原图、缩略图并写入数据库，前端不会接触 SecretKey。</p></section>
      </aside>
    </div>
  )
}

function HealthCard({ icon, title, status, detail, latency }: { icon: ReactNode; title: string; status: string; detail: string; latency: number }) {
  return <div className={cn(panelClass, 'p-4')}><div className="flex items-start justify-between gap-3"><div className="rounded-xl bg-[var(--bg-input)] p-2.5 text-[var(--accent)]">{icon}</div><StatusBadge value={status} /></div><p className="mt-4 text-sm font-semibold text-[var(--text-primary)]">{title}</p><p className="mt-1 truncate text-xs text-[var(--text-tertiary)]" title={detail}>{detail || '未提供详情'}</p><p className="mt-2 text-[11px] text-[var(--text-secondary)]">响应 {latency || 0} ms</p></div>
}

function SystemPanel({ refreshKey }: { refreshKey: number }) {
  const [system, setSystem] = useState<AdminSystemInfo | null>(null)
  const [apis, setApis] = useState<AdminApiEndpoint[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    Promise.all([adminApi.getSystem(), adminApi.getApis()])
      .then(([systemResult, apiResult]) => { if (active) { setSystem(systemResult); setApis(apiResult) } })
      .catch((reason) => active && setError(reason instanceof Error ? reason.message : '系统信息加载失败'))
      .finally(() => active && setLoading(false))
    return () => { active = false }
  }, [refreshKey])

  const filteredApis = useMemo(() => {
    const keyword = search.trim().toLowerCase()
    return keyword ? apis.filter((item) => `${item.method} ${item.path} ${item.controller} ${item.summary}`.toLowerCase().includes(keyword)) : apis
  }, [apis, search])

  if (loading) return <PageLoading label="正在检查服务与接口…" />
  if (error || !system) return <ErrorPanel message={error || '系统信息不可用'} />
  return <div className="space-y-5">
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"><HealthCard icon={<Server size={18} />} title="应用服务" status="Healthy" detail={`${system.application.name} ${system.application.version}`} latency={0} /><HealthCard icon={<Database size={18} />} title="数据库" status={system.database.status} detail={system.database.message} latency={system.database.latencyMs} /><HealthCard icon={<Cloud size={18} />} title="腾讯 COS" status={system.cos.status} detail={`${system.cos.bucket} · ${system.cos.region}`} latency={system.cos.latencyMs} /><HealthCard icon={<Activity size={18} />} title={system.aiProvider.provider || 'AI Provider'} status={system.aiProvider.status} detail={system.aiProvider.message} latency={system.aiProvider.latencyMs} /></div>
    <div className="grid gap-5 lg:grid-cols-2"><section className={cn(panelClass, 'p-5')}><SectionHeader title="应用与运行环境" /><dl className="grid grid-cols-[130px_minmax(0,1fr)] gap-x-4 gap-y-3 text-xs"><dt className="text-[var(--text-tertiary)]">运行环境</dt><dd className="text-[var(--text-primary)]">{system.application.environment}</dd><dt className="text-[var(--text-tertiary)]">服务器时间</dt><dd className="text-[var(--text-primary)]">{formatDate(system.application.serverTime)}</dd><dt className="text-[var(--text-tertiary)]">运行时长</dt><dd className="text-[var(--text-primary)]">{system.application.uptime || '未知'}</dd><dt className="text-[var(--text-tertiary)]">.NET 运行时</dt><dd className="text-[var(--text-primary)]">{system.runtime.framework || '未知'}</dd><dt className="text-[var(--text-tertiary)]">操作系统</dt><dd className="break-all text-[var(--text-primary)]">{system.runtime.os || '未知'}</dd><dt className="text-[var(--text-tertiary)]">进程内存</dt><dd className="text-[var(--text-primary)]">{formatBytes(system.runtime.processMemoryBytes)}</dd></dl></section><section className={cn(panelClass, 'p-5')}><SectionHeader title="存储配置（脱敏）" /><dl className="grid grid-cols-[110px_minmax(0,1fr)] gap-x-4 gap-y-3 text-xs"><dt className="text-[var(--text-tertiary)]">Bucket</dt><dd className="break-all text-[var(--text-primary)]">{system.cos.bucket || '未配置'}</dd><dt className="text-[var(--text-tertiary)]">Region</dt><dd className="text-[var(--text-primary)]">{system.cos.region || '未配置'}</dd><dt className="text-[var(--text-tertiary)]">Base URL</dt><dd className="break-all text-[var(--text-primary)]">{system.cos.baseUrl || '未配置'}</dd><dt className="text-[var(--text-tertiary)]">CPU 核心</dt><dd className="text-[var(--text-primary)]">{system.runtime.processorCount || '未知'}</dd></dl><p className="mt-4 rounded-xl border border-emerald-400/20 bg-emerald-400/8 p-3 text-[11px] leading-5 text-emerald-100/70">本页面不会返回或显示数据库密码、JWT 密钥、COS SecretKey、AI ApiKey 和刷新 Token 摘要。</p></section></div>
    <section className={cn(panelClass, 'overflow-hidden')}><div className="border-b border-[var(--border-default)] p-5"><SectionHeader title="网站 API 清单" description={`当前发现 ${apis.length} 个接口，可按路径、控制器或说明检索。`} /><label className="relative block max-w-lg"><Search size={15} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--text-tertiary)]" /><input value={search} onChange={(event) => setSearch(event.target.value)} className={cn(inputClass, 'pl-10')} placeholder="搜索 /api/admin、Images、登录…" /></label></div>{filteredApis.length ? <div className="max-h-[520px] overflow-auto"><table className="w-full min-w-[760px] text-left text-xs"><thead className="sticky top-0 bg-[var(--bg-input)] text-[var(--text-tertiary)]"><tr><th className="px-5 py-3">方法</th><th className="px-4 py-3">路径</th><th className="px-4 py-3">控制器</th><th className="px-4 py-3">权限</th><th className="px-5 py-3">说明</th></tr></thead><tbody className="divide-y divide-[var(--border-subtle)]">{filteredApis.map((api, index) => <tr key={`${api.method}-${api.path}-${index}`} className="hover:bg-[var(--bg-input)]/60"><td className="px-5 py-3"><span className={cn('rounded-md px-2 py-1 text-[10px] font-bold', api.method === 'GET' ? 'bg-emerald-400/10 text-emerald-300' : api.method === 'DELETE' ? 'bg-red-400/10 text-red-300' : api.method === 'POST' ? 'bg-blue-400/10 text-blue-300' : 'bg-amber-400/10 text-amber-300')}>{api.method}</span></td><td className="px-4 py-3 font-mono text-[11px] text-[var(--text-primary)]">{api.path}</td><td className="px-4 py-3 text-[var(--text-secondary)]">{api.controller}</td><td className="px-4 py-3"><StatusBadge value={api.authorization || 'Public'} /></td><td className="px-5 py-3 text-[var(--text-secondary)]">{api.summary}</td></tr>)}</tbody></table></div> : <EmptyState icon={<ListFilter size={20} />} title="没有匹配接口" description="清除搜索关键词后查看全部接口。" />}</section>
  </div>
}

function AuditPanel({ refreshKey }: { refreshKey: number }) {
  const [items, setItems] = useState<AdminAuditLog[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => { setPage(1) }, [search])
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setLoading(true); setError('')
      adminApi.getAuditLogs({ page, pageSize: 30, search: search.trim() }).then((result) => { setItems(result.items); setTotalPages(result.totalPages); setTotalCount(result.totalCount) }).catch((reason) => setError(reason instanceof Error ? reason.message : '审计日志加载失败')).finally(() => setLoading(false))
    }, search ? 300 : 0)
    return () => window.clearTimeout(timer)
  }, [page, search, refreshKey])
  return <div className="space-y-4"><section className={cn(panelClass, 'p-5')}><SectionHeader title="管理操作审计" description="记录管理员对用户、会话、图库及其他敏感资源的修改。" /><label className="relative block max-w-lg"><Search size={15} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--text-tertiary)]" /><input value={search} onChange={(event) => setSearch(event.target.value)} className={cn(inputClass, 'pl-10')} placeholder="搜索操作人、动作、目标或描述" /></label></section><section className={cn(panelClass, 'overflow-hidden')}>{loading ? <PageLoading label="正在读取审计记录…" /> : error ? <ErrorPanel message={error} /> : items.length ? <><div className="overflow-x-auto"><table className="w-full min-w-[980px] text-left text-xs"><thead className="border-b border-[var(--border-default)] bg-[var(--bg-input)] text-[var(--text-tertiary)]"><tr><th className="px-5 py-3">时间</th><th className="px-4 py-3">管理员</th><th className="px-4 py-3">操作</th><th className="px-4 py-3">目标</th><th className="px-4 py-3">结果</th><th className="px-4 py-3">IP</th><th className="px-5 py-3">说明</th></tr></thead><tbody className="divide-y divide-[var(--border-subtle)]">{items.map((item) => <tr key={item.id} className="hover:bg-[var(--bg-input)]/55"><td className="whitespace-nowrap px-5 py-3 text-[var(--text-secondary)]">{formatDate(item.createdAt)}</td><td className="px-4 py-3 font-medium text-[var(--text-primary)]">{item.operatorName || item.operatorUserId}</td><td className="px-4 py-3"><span className="rounded-lg bg-[var(--accent)]/10 px-2 py-1 text-[11px] font-medium text-[var(--accent)]">{item.action}</span></td><td className="px-4 py-3 text-[var(--text-secondary)]">{item.targetType} {item.targetId}</td><td className="px-4 py-3"><StatusBadge value={item.result} /></td><td className="px-4 py-3 text-[var(--text-tertiary)]">{item.ipAddress || '—'}</td><td className="max-w-sm truncate px-5 py-3 text-[var(--text-secondary)]" title={item.description}>{item.description}</td></tr>)}</tbody></table></div><div className="flex items-center justify-between border-t border-[var(--border-default)] px-5 py-3"><p className="text-xs text-[var(--text-tertiary)]">第 {page}/{totalPages} 页，共 {totalCount} 条</p><div className="flex gap-2"><button disabled={page <= 1} onClick={() => setPage((value) => value - 1)} className={secondaryButtonClass}><ChevronLeft size={14} /></button><button disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)} className={secondaryButtonClass}><ChevronRight size={14} /></button></div></div></> : <EmptyState icon={<Activity size={20} />} title="暂无审计日志" description="敏感管理操作执行后会显示在这里。" />}</section></div>
}

export default function AdminPage() {
  const [tab, setTab] = useState<AdminTab>('overview')
  const [refreshKey, setRefreshKey] = useState(0)
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null)

  const notify = useCallback((message: string, type: 'success' | 'error' = 'success') => {
    setToast({ message, type })
    window.setTimeout(() => setToast(null), 3200)
  }, [])

  return (
    <div className="min-h-full bg-[var(--bg-base)]">
      <header className="sticky top-0 z-30 border-b border-[var(--border-subtle)] bg-[var(--bg-base)]/92 backdrop-blur-xl">
        <div className="mx-auto flex max-w-[1600px] flex-col gap-4 px-4 py-4 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
          <div className="flex items-center gap-3"><div className="rounded-2xl bg-[var(--accent)] p-2.5 text-white shadow-[0_0_20px_var(--accent-glow)]"><ShieldCheck size={21} /></div><div><h1 className="text-lg font-semibold tracking-tight text-[var(--text-primary)]">网站管理中心</h1><p className="text-[11px] text-[var(--text-tertiary)]">用户、业务数据、COS、接口与安全审计</p></div></div>
          <div className="flex items-center gap-2"><span className="hidden items-center gap-2 rounded-full border border-emerald-400/20 bg-emerald-400/8 px-3 py-1.5 text-[11px] text-emerald-300 sm:inline-flex"><span className="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400" />管理员会话已验证</span><button onClick={() => setRefreshKey((value) => value + 1)} className={secondaryButtonClass}><RefreshCw size={14} />刷新当前数据</button></div>
        </div>
        <div className="mx-auto max-w-[1600px] overflow-x-auto px-4 sm:px-6 lg:px-8"><nav className="flex min-w-max gap-1">{TAB_ITEMS.map((item) => { const Icon = item.icon; return <button key={item.key} onClick={() => setTab(item.key)} className={cn('flex items-center gap-2 border-b-2 px-3 py-3 text-xs font-medium', tab === item.key ? 'border-[var(--accent)] text-[var(--text-primary)]' : 'border-transparent text-[var(--text-tertiary)] hover:text-[var(--text-primary)]')}><Icon size={14} />{item.label}</button> })}</nav></div>
      </header>
      <main className="mx-auto max-w-[1600px] p-4 sm:p-6 lg:p-8">{tab === 'overview' && <OverviewPanel refreshKey={refreshKey} />}{tab === 'users' && <UsersPanel refreshKey={refreshKey} notify={notify} />}{tab === 'gallery' && <GalleryPanel notify={notify} />}{tab === 'system' && <SystemPanel refreshKey={refreshKey} />}{tab === 'audit' && <AuditPanel refreshKey={refreshKey} />}</main>
      {toast && <div className={cn('fixed bottom-5 right-5 z-[120] flex max-w-sm items-center gap-2 rounded-xl border px-4 py-3 text-sm shadow-2xl animate-soft-pop', toast.type === 'success' ? 'border-emerald-400/25 bg-[#10251f] text-emerald-200' : 'border-red-400/25 bg-[#2a1518] text-red-200')}>{toast.type === 'success' ? <CheckCircle2 size={17} /> : <CircleOff size={17} />}<span>{toast.message}</span></div>}
    </div>
  )
}
