import { AlertTriangle, Home, RefreshCw, Undo2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom'
import { Button } from './ui/Button'

type ErrorDetails = {
  title: string
  message: string
  status?: number
}

function describeError(error: unknown): ErrorDetails {
  if (isRouteErrorResponse(error)) {
    if (error.status === 404) {
      return {
        status: 404,
        title: '页面不存在',
        message: '这个地址可能已经失效，或者页面已被移动。',
      }
    }

    return {
      status: error.status,
      title: '页面暂时无法打开',
      message: typeof error.data === 'string' ? error.data : '请稍后重试。',
    }
  }

  return {
    title: '页面出现异常',
    message: '当前操作没有完成。你可以重新加载，或返回工作台继续使用。',
  }
}

function createErrorId(): string {
  try {
    return globalThis.crypto?.randomUUID?.().slice(0, 8).toUpperCase()
      ?? Date.now().toString(36).toUpperCase()
  } catch {
    return Date.now().toString(36).toUpperCase()
  }
}

export default function RouteErrorPage() {
  const navigate = useNavigate()
  const error = useRouteError()
  const details = describeError(error)
  const [errorId] = useState(createErrorId)

  useEffect(() => {
    if (import.meta.env.DEV) {
      console.error(`[RouteError:${errorId}]`, error)
    }
  }, [error, errorId])

  return (
    <main className="min-h-screen bg-[var(--bg-primary)] text-[var(--text-primary)] flex items-center justify-center p-6">
      <section className="w-full max-w-lg rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] p-6 sm:p-8 shadow-[var(--shadow-elevated)]">
        <div className="mb-5 flex h-11 w-11 items-center justify-center rounded-xl bg-red-500/10 text-red-400">
          <AlertTriangle size={22} aria-hidden="true" />
        </div>

        <p className="text-xs font-medium tracking-wide text-[var(--text-tertiary)]">
          {details.status ? `HTTP ${details.status}` : 'APPLICATION ERROR'}
        </p>
        <h1 className="mt-2 text-xl font-semibold">{details.title}</h1>
        <p className="mt-2 text-sm leading-6 text-[var(--text-secondary)]">{details.message}</p>
        <p className="mt-4 text-xs text-[var(--text-tertiary)]">问题编号：{errorId}</p>

        <div className="mt-6 flex flex-wrap gap-2">
          <Button variant="primary" onClick={() => window.location.reload()}>
            <RefreshCw size={14} />重新加载
          </Button>
          <Button variant="outline" onClick={() => navigate(-1)}>
            <Undo2 size={14} />返回上一页
          </Button>
          <Button variant="ghost" onClick={() => navigate('/app', { replace: true })}>
            <Home size={14} />返回工作台
          </Button>
        </div>
      </section>
    </main>
  )
}
