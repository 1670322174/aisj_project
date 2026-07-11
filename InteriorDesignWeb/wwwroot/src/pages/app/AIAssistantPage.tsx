import { Bot, MessageSquareText, Sparkles } from 'lucide-react'

export default function AIAssistantPage() {
  return (
    <div className="min-h-full bg-[var(--bg-base)] px-6 py-8">
      <div className="max-w-5xl mx-auto">
        <div className="rounded-3xl border border-[var(--border-subtle)] bg-[var(--bg-card)] p-8 md:p-12 overflow-hidden relative">
          <div className="absolute -right-20 -top-24 w-72 h-72 rounded-full bg-[var(--accent)]/10 blur-3xl pointer-events-none" />
          <div className="relative max-w-2xl">
            <div className="w-12 h-12 rounded-2xl bg-[var(--accent)]/15 border border-[var(--accent-border)] flex items-center justify-center text-[var(--accent)] mb-6">
              <Bot size={23} />
            </div>
            <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full border border-[var(--border-default)] text-[11px] text-[var(--text-secondary)] mb-4">
              <Sparkles size={11} />即将上线
            </div>
            <h1 className="text-3xl font-semibold tracking-tight text-[var(--text-primary)]">AI 设计助理</h1>
            <p className="mt-4 text-sm leading-7 text-[var(--text-secondary)]">
              围绕当前方案理解空间需求、整理设计方向、优化提示词，并把对话结论转成可执行的生成任务。
            </p>
            <div className="mt-8 grid sm:grid-cols-3 gap-3">
              {['方案需求梳理', '生成提示词优化', '设计结果解读'].map((item) => (
                <div key={item} className="px-4 py-3 rounded-xl bg-[var(--bg-input)] border border-[var(--border-subtle)] text-xs text-[var(--text-secondary)] flex items-center gap-2">
                  <MessageSquareText size={13} className="text-[var(--accent)]" />{item}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
