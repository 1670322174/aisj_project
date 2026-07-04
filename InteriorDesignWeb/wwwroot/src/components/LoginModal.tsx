// src/components/LoginModal.tsx
import React, { useState } from 'react'
import { User, Lock, Sparkles, AlertCircle, Loader2 } from 'lucide-react'
import { Modal } from './ui/Modal'
import { Input } from './ui/Input'
import { Button } from './ui/Button'
import { useAppStore } from '@/store/useAppStore'

/* ─────────────────────────────────────────
   用户名校验
───────────────────────────────────────── */
const USERNAME_REGEX = /^[\u4e00-\u9fa5a-zA-Z0-9_-]+$/

function validateUsername(value: string): string {
  if (!value.trim())              return '用户名不能为空'
  if (!USERNAME_REGEX.test(value)) return '用户名不能包含特殊字符'
  if (value.length < 2)           return '用户名至少 2 个字符'
  if (value.length > 20)          return '用户名最多 20 个字符'
  return ''
}

/* ─────────────────────────────────────────
   微信图标
───────────────────────────────────────── */
function WechatIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor">
      <path d="M8.691 2.188C3.891 2.188 0 5.476 0 9.53c0 2.212 1.17 4.203 3.002 5.55a.59.59 0 0 1 .213.665l-.39 1.48c-.019.07-.048.141-.048.213 0 .163.13.295.29.295a.326.326 0 0 0 .167-.054l1.903-1.114a.864.864 0 0 1 .717-.098 10.16 10.16 0 0 0 2.837.403c.276 0 .543-.027.811-.05-.857-2.578.157-4.972 1.932-6.446 1.703-1.415 3.882-1.98 5.853-1.838-.576-3.583-4.06-6.348-8.596-6.348zM5.785 5.991c.642 0 1.162.529 1.162 1.18a1.17 1.17 0 0 1-1.162 1.178A1.17 1.17 0 0 1 4.623 7.17c0-.651.52-1.18 1.162-1.18zm5.813 0c.642 0 1.162.529 1.162 1.18a1.17 1.17 0 0 1-1.162 1.178 1.17 1.17 0 0 1-1.162-1.178c0-.651.52-1.18 1.162-1.18zm5.34 2.867c-1.797-.052-3.746.512-5.161 1.67-1.42 1.162-2.204 2.957-1.632 4.912.942 3.093 4.994 4.22 8.018 3.17a.5.5 0 0 1 .528.094l1.508.882a.26.26 0 0 0 .14.047c.134 0 .24-.107.24-.24 0-.06-.023-.12-.038-.176l-.327-1.233a.47.47 0 0 1 .174-.535C21.787 16.006 23 14.587 23 12.948c0-2.818-2.885-4.99-6.062-4.09zm-3.332 2.322c.533 0 .965.44.965.983a.974.974 0 0 1-.965.983.974.974 0 0 1-.966-.983c0-.543.432-.983.966-.983zm4.5 0c.533 0 .965.44.965.983a.974.974 0 0 1-.965.983.974.974 0 0 1-.966-.983c0-.543.432-.983.966-.983z" />
    </svg>
  )
}

/* ─────────────────────────────────────────
   主组件
───────────────────────────────────────── */
export function LoginModal() {
  const {
    isLoginModalOpen,
    closeLoginModal,
    loginAction,
    isAuthLoading,
    authError,
    clearAuthError,
  } = useAppStore()

  const [username, setUsername]           = useState<string>('')
  const [password, setPassword]           = useState<string>('')
  const [usernameError, setUsernameError] = useState<string>('')

  /* ── 关闭弹窗：清空表单和错误 ── */
  const handleClose = () => {
    setUsername('')
    setPassword('')
    setUsernameError('')
    clearAuthError()
    closeLoginModal()
  }

  /* ── 用户名实时校验 ── */
  const handleUsernameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value
    setUsername(val)
    // 已有错误时实时修正反馈
    if (usernameError) setUsernameError(validateUsername(val))
  }

  /* ── 表单提交 ── */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()

    // 前端校验
    const err = validateUsername(username)
    if (err) {
      setUsernameError(err)
      return
    }

    await loginAction({ Username: username, Password: password })
    // 成功时 loginAction 内部会调用 setLoginModalOpen(false)
    // 失败时 authError 会被设置，弹窗保持打开
  }

  /* ── 微信登录（占位） ── */
  const handleWechatLogin = async () => {
    // TODO: 调用微信 OAuth 接口
    console.log('微信登录暂未实现')
  }

  return (
    <Modal isOpen={isLoginModalOpen} onClose={handleClose}>

      {/* ── Logo + 标题 ── */}
      <div className="flex flex-col items-center gap-2 mb-6">
        <div
          className="w-10 h-10 rounded-xl bg-[var(--accent)] flex items-center justify-center
                     shadow-[0_0_20px_var(--accent-glow)]"
        >
          <Sparkles size={18} className="text-white" />
        </div>
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          欢迎回来
        </h2>
        <p className="text-sm text-[var(--text-secondary)]">
          登录以使用 AI 设计助手
        </p>
      </div>

      {/* ── 表单 ── */}
      <form onSubmit={handleSubmit} className="flex flex-col gap-3" noValidate>

        {/* 用户名 */}
        <div className="flex flex-col gap-1">
          <Input
            label="用户名"
            type="text"
            placeholder="请输入用户名"
            icon={<User size={14} />}
            value={username}
            onChange={handleUsernameChange}
            onBlur={() => setUsernameError(validateUsername(username))}
            required
            className={usernameError ? 'border-red-500/70 focus:border-red-500' : ''}
          />
          {usernameError ? (
            <div className="flex items-center gap-1.5 mt-0.5">
              <AlertCircle size={12} className="text-red-400 shrink-0" />
              <span className="text-xs text-red-400">{usernameError}</span>
            </div>
          ) : (
            <p className="text-[11px] text-[var(--text-tertiary)] mt-0.5">
              支持中文、英文、数字、下划线、连字符
            </p>
          )}
        </div>

        {/* 密码 */}
        <Input
          label="密码"
          type="password"
          placeholder="••••••••"
          icon={<Lock size={14} />}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        {/* 忘记密码 */}
        <div className="flex justify-end -mt-1">
          <button
            type="button"
            className="text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] transition-colors"
          >
            忘记密码？
          </button>
        </div>

        {/* ── 接口错误提示（在按钮上方） ── */}
        {authError && (
          <div
            className="flex items-start gap-2 px-3 py-2.5 rounded-lg
                       bg-red-500/10 border border-red-500/20"
          >
            <AlertCircle size={14} className="text-red-400 shrink-0 mt-0.5" />
            <p className="text-xs text-red-400 leading-relaxed">{authError}</p>
          </div>
        )}

        {/* 登录按钮 */}
        <Button
          type="submit"
          variant="primary"
          size="lg"
          className="w-full mt-1"
          disabled={isAuthLoading}
        >
          {isAuthLoading ? (
            <span className="flex items-center gap-2">
              <Loader2 size={15} className="animate-spin" />
              登录中...
            </span>
          ) : (
            '登录'
          )}
        </Button>
      </form>

      {/* ── 分割线 ── */}
      <div className="flex items-center gap-3 my-4">
        <div className="flex-1 h-px bg-[var(--border-subtle)]" />
        <span className="text-xs text-[var(--text-tertiary)]">或</span>
        <div className="flex-1 h-px bg-[var(--border-subtle)]" />
      </div>

      {/* ── 微信登录 ── */}
      <Button
        type="button"
        variant="outline"
        size="md"
        className="w-full text-[#07C160] border-[#07C160]/30 hover:bg-[#07C160]/10 hover:border-[#07C160]/60"
        onClick={handleWechatLogin}
        disabled={isAuthLoading}
      >
        <WechatIcon size={16} />
        使用微信继续
      </Button>

      {/* ── 底部条款 ── */}
      <p className="text-center text-xs text-[var(--text-tertiary)] mt-4">
        登录即代表你同意我们的{' '}
        <button
          type="button"
          className="text-[var(--text-secondary)] hover:text-[var(--text-primary)]
                     underline underline-offset-2 transition-colors"
        >
          服务条款
        </button>
        {' '}和{' '}
        <button
          type="button"
          className="text-[var(--text-secondary)] hover:text-[var(--text-primary)]
                     underline underline-offset-2 transition-colors"
        >
          隐私政策
        </button>
      </p>
    </Modal>
  )
}