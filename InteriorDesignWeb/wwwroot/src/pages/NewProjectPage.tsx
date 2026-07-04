// src/pages/NewProjectPage.tsx
import { useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Loader2,
  CheckCircle2,
  AlertCircle,
  Plus,
  Minus,
  BedDouble,
  Sofa,
  Bath,
  Sun,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react'
import { projectsApi } from '../api/modules/projects'
import type { RoomConfig, RoomResult } from '../api/modules/projects'

/* ─────────────────────────────────────────
   户型预设
───────────────────────────────────────── */
interface LayoutPreset {
  bedroom:    number
  livingRoom: number
  bathroom:   number
  balcony:    number
}

const LAYOUT_PRESETS: Record<string, LayoutPreset | null> = {
  '一室一厅': { bedroom: 1, livingRoom: 1, bathroom: 1, balcony: 1 },
  '两室一厅': { bedroom: 2, livingRoom: 1, bathroom: 1, balcony: 1 },
  '三室两厅': { bedroom: 3, livingRoom: 2, bathroom: 2, balcony: 1 },
  '四室两厅': { bedroom: 4, livingRoom: 2, bathroom: 2, balcony: 2 },
  '自定义':   null,
}

const PRESET_NAMES = Object.keys(LAYOUT_PRESETS)

/* ─────────────────────────────────────────
   房间数量限制
───────────────────────────────────────── */
const ROOM_LIMITS: Record<keyof RoomConfig, { min: number; max: number }> = {
  bedroom:    { min: 0, max: 6 },
  livingRoom: { min: 0, max: 3 },
  bathroom:   { min: 0, max: 4 },
  balcony:    { min: 0, max: 4 },
}

/* ─────────────────────────────────────────
   Step 3 预览用：本地版 generateRoomInputsPreview
   逻辑与 projects.ts 中的私有函数完全一致
───────────────────────────────────────── */
function generateRoomInputsPreview(
  config: RoomConfig,
): Array<{ name: string; type: string }> {
  const rooms: Array<{ name: string; type: string }> = []

  /* 卧室 */
  if (config.bedroom === 1) {
    rooms.push({ name: '主卧', type: 'bedroom' })
  } else if (config.bedroom === 2) {
    rooms.push({ name: '主卧', type: 'bedroom' }, { name: '次卧', type: 'bedroom' })
  } else if (config.bedroom === 3) {
    rooms.push(
      { name: '主卧',  type: 'bedroom' },
      { name: '次卧1', type: 'bedroom' },
      { name: '次卧2', type: 'bedroom' },
    )
  } else if (config.bedroom >= 4) {
    rooms.push(
      { name: '主卧',  type: 'bedroom' },
      { name: '次卧1', type: 'bedroom' },
      { name: '次卧2', type: 'bedroom' },
      { name: '次卧3', type: 'bedroom' },
    )
  }

  /* 客厅 / 餐厅 */
  if (config.livingRoom === 1) {
    rooms.push({ name: '客厅', type: 'living_room' })
  } else if (config.livingRoom >= 2) {
    rooms.push(
      { name: '客厅', type: 'living_room' },
      { name: '餐厅', type: 'living_room' },
    )
  }

  /* 卫生间 */
  if (config.bathroom === 1) {
    rooms.push({ name: '卫生间', type: 'bathroom' })
  } else if (config.bathroom >= 2) {
    rooms.push(
      { name: '主卫', type: 'bathroom' },
      { name: '次卫', type: 'bathroom' },
    )
  }

  /* 阳台 */
  if (config.balcony === 1) {
    rooms.push({ name: '阳台', type: 'balcony' })
  } else if (config.balcony >= 2) {
    rooms.push(
      { name: '主阳台', type: 'balcony' },
      { name: '次阳台', type: 'balcony' },
    )
  }

  return rooms
}

/* ─────────────────────────────────────────
   步骤进度条
───────────────────────────────────────── */
const STEPS = [
  { id: 1 as const, label: '基础信息' },
  { id: 2 as const, label: '房间配置' },
  { id: 3 as const, label: '确认创建' },
]

type StepId = 1 | 2 | 3

interface StepBarProps {
  current: StepId
}

function StepBar({ current }: StepBarProps) {
  return (
    <div className="flex items-center justify-center mb-10 select-none">
      {STEPS.map((step, idx) => {
        const done    = step.id < current
        const active  = step.id === current
        const future  = step.id > current

        return (
          <div key={step.id} className="flex items-center">
            {/* 圆形 + 标签 */}
            <div className="flex flex-col items-center gap-2">
              <div
                className={`w-9 h-9 rounded-full flex items-center justify-center
                            border-2 transition-all duration-300
                            ${done
                              ? 'bg-[var(--accent)] border-[var(--accent)]'
                              : active
                                ? 'bg-[var(--accent)]/15 border-[var(--accent)]'
                                : 'bg-[var(--bg-input)] border-[var(--border-default)]'
                            }`}
              >
                {done
                  ? <CheckCircle2 size={18} className="text-white" strokeWidth={2.5} />
                  : (
                    <span
                      className={`text-sm font-semibold
                                  ${active  ? 'text-[var(--accent)]'  : ''}
                                  ${future  ? 'text-[var(--text-tertiary)]' : ''}`}
                    >
                      {step.id}
                    </span>
                  )
                }
              </div>
              <span
                className={`text-xs transition-colors duration-200
                            ${done   ? 'text-[var(--text-tertiary)]'  : ''}
                            ${active ? 'text-[var(--text-primary)] font-semibold' : ''}
                            ${future ? 'text-[var(--text-tertiary)]'  : ''}`}
              >
                {step.label}
              </span>
            </div>

            {/* 连接线 */}
            {idx < STEPS.length - 1 && (
              <div
                className={`w-24 h-0.5 mx-3 mb-5 rounded-full transition-colors duration-300
                            ${step.id < current
                              ? 'bg-[var(--accent)]'
                              : 'bg-[var(--border-default)]'
                            }`}
              />
            )}
          </div>
        )
      })}
    </div>
  )
}

/* ─────────────────────────────────────────
   房间配置行
───────────────────────────────────────── */
interface RoomRowProps {
  icon:    React.ReactNode
  label:   string
  value:   number
  min:     number
  max:     number
  onDec:   () => void
  onInc:   () => void
}

function RoomRow({ icon, label, value, min, max, onDec, onInc }: RoomRowProps) {
  const decDisabled = value <= min
  const incDisabled = value >= max

  const btnBase =
    `w-8 h-8 rounded-lg flex items-center justify-center border
     transition-all duration-150`
  const btnEnabled =
    `border-[var(--border-default)] bg-[var(--bg-input)]
     text-[var(--text-primary)]
     hover:border-[var(--border-strong)] hover:bg-[var(--bg-card)]
     cursor-pointer`
  const btnDisabledCls =
    `border-[var(--border-subtle)] bg-[var(--bg-input)]
     text-[var(--text-tertiary)] opacity-35 cursor-not-allowed`

  return (
    <div
      className="flex items-center justify-between py-3
                 border-b border-[var(--border-subtle)] last:border-0"
    >
      {/* 左侧：图标 + 名称 */}
      <div className="flex items-center gap-3 text-[var(--text-secondary)]">
        <span className="text-[var(--text-tertiary)]">{icon}</span>
        <span className="text-sm font-medium text-[var(--text-primary)]">{label}</span>
      </div>

      {/* 右侧：计数控制 */}
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onDec}
          disabled={decDisabled}
          className={`${btnBase} ${decDisabled ? btnDisabledCls : btnEnabled}`}
        >
          <Minus size={14} strokeWidth={2.5} />
        </button>

        <span
          className={`w-6 text-center text-base font-bold tabular-nums
                      transition-opacity duration-150
                      ${value === 0 ? 'opacity-30' : 'opacity-100'}
                      text-[var(--text-primary)]`}
        >
          {value}
        </span>

        <button
          type="button"
          onClick={onInc}
          disabled={incDisabled}
          className={`${btnBase} ${incDisabled ? btnDisabledCls : btnEnabled}`}
        >
          <Plus size={14} strokeWidth={2.5} />
        </button>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────
   主页面
───────────────────────────────────────── */
export default function NewProjectPage() {
  const navigate = useNavigate()

  /* ── 步骤 ── */
  const [currentStep, setCurrentStep] = useState<StepId>(1)

  /* ── Step 1 ── */
  const [name, setName]             = useState<string>('')
  const [description, setDescription] = useState<string>('')
  const [nameError, setNameError]   = useState<string>('')

  /* ── Step 2 ── */
  const [selectedPreset, setSelectedPreset] = useState<string>('两室一厅')
  const [roomConfig, setRoomConfig] = useState<RoomConfig>({
    bedroom:    2,
    livingRoom: 1,
    bathroom:   1,
    balcony:    1,
  })

  /* ── 提交状态 ── */
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false)
  const [submitError, setSubmitError]   = useState<string>('')
  const [roomResults, setRoomResults]   = useState<RoomResult[]>([])

  /* ─────────────────────────────────────
     Step 1 校验
  ───────────────────────────────────── */
  const validateStep1 = useCallback((): boolean => {
    const trimmed = name.trim()
    if (!trimmed) {
      setNameError('方案名称不能为空')
      return false
    }
    if (trimmed.length > 50) {
      setNameError('方案名称不能超过 50 个字符')
      return false
    }
    setNameError('')
    return true
  }, [name])

  /* ─────────────────────────────────────
     户型预设选择
  ───────────────────────────────────── */
  const handlePresetSelect = useCallback((presetName: string) => {
    setSelectedPreset(presetName)
    const preset = LAYOUT_PRESETS[presetName]
    if (preset) setRoomConfig(preset)
  }, [])

  /* ─────────────────────────────────────
     房间数量调整
  ───────────────────────────────────── */
  const handleRoomCountChange = useCallback(
    (key: keyof RoomConfig, delta: 1 | -1) => {
      setRoomConfig((prev) => {
        const { min, max } = ROOM_LIMITS[key]
        const next = Math.min(max, Math.max(min, prev[key] + delta))
        return { ...prev, [key]: next }
      })
      setSelectedPreset('自定义')
    },
    [],
  )

  /* ─────────────────────────────────────
     总房间数
  ───────────────────────────────────── */
  const totalRooms =
    roomConfig.bedroom +
    roomConfig.livingRoom +
    roomConfig.bathroom +
    roomConfig.balcony

  /* ─────────────────────────────────────
     步骤导航
  ───────────────────────────────────── */
  const handleNext = useCallback(() => {
    if (currentStep === 1) {
      if (!validateStep1()) return
      setCurrentStep(2)
    } else if (currentStep === 2) {
      setCurrentStep(3)
    }
  }, [currentStep, validateStep1])

  const handlePrev = useCallback(() => {
    if (currentStep > 1) setCurrentStep((s) => (s - 1) as StepId)
  }, [currentStep])

  /* ─────────────────────────────────────
     提交
  ───────────────────────────────────── */
  const handleSubmit = useCallback(async () => {
    if (isSubmitting) return

    setIsSubmitting(true)
    setSubmitError('')
    setRoomResults([])

    try {
      /* Step 1：创建方案 */
      const project = await projectsApi.createProject({
        name:        name.trim(),
        description: description.trim(),
      })

      /* Step 2：批量添加房间 */
      const results = await projectsApi.addRoomsToProject(
        project.projectID,
        roomConfig,
      )
      setRoomResults(results)

      /* Step 3：判断结果 */
      const failedCount = results.filter((r) => !r.success).length

      if (failedCount === 0) {
        // 全部成功
        setTimeout(() => navigate('/app/projects'), 1500)
      } else {
        // 部分失败
        setSubmitError(`方案已创建，但有 ${failedCount} 个房间添加失败`)
        setTimeout(() => navigate('/app/projects'), 3000)
      }
    } catch (err) {
      // 方案创建本身失败
      const msg = err instanceof Error ? err.message : '创建失败，请稍后重试'
      setSubmitError(msg)
      // 保持 isSubmitting = true 以禁用按钮（通过重试按钮手动重置）
    }
  }, [isSubmitting, name, description, roomConfig, navigate])

  /* ─────────────────────────────────────
     Step 3 预览
  ───────────────────────────────────── */
  const previewRooms = generateRoomInputsPreview(roomConfig)

  /* ─────────────────────────────────────
     提交状态派生
  ───────────────────────────────────── */
  const isCreating      = isSubmitting && !submitError && roomResults.length === 0
  const isPartialFail   = isSubmitting && !!submitError && roomResults.length > 0
  const isFullSuccess   = isSubmitting && !submitError && roomResults.length > 0
  const isCreateFailed  = isSubmitting && !!submitError && roomResults.length === 0

  const failedRooms = roomResults.filter((r) => !r.success)

  /* ─────────────────────────────────────
     渲染
  ───────────────────────────────────── */
  return (
    <div className="min-h-full bg-[var(--bg-base)] px-6 pt-8 pb-16">
      <div className="max-w-[640px] mx-auto">

        {/* 页面标题 */}
        <div className="mb-8">
          <h1 className="text-2xl font-bold text-[var(--text-primary)] tracking-tight">
            新建方案
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            创建一个新的室内设计方案
          </p>
        </div>

        {/* 步骤进度条 */}
        <StepBar current={currentStep} />

        {/* ════════ Step 1：基础信息 ════════ */}
        {currentStep === 1 && (
          <div
            className="rounded-2xl border border-[var(--border-default)]
                       bg-[var(--bg-card)] p-8"
          >
            <h2 className="text-base font-semibold text-[var(--text-primary)] mb-6">
              填写方案基础信息
            </h2>

            {/* 方案名称 */}
            <div className="mb-6">
              <label className="block text-sm font-medium text-[var(--text-secondary)] mb-1.5">
                方案名称
                <span className="text-red-400 ml-0.5">*</span>
              </label>
              <input
                type="text"
                maxLength={50}
                value={name}
                onChange={(e) => {
                  setName(e.target.value)
                  if (nameError) setNameError('')
                }}
                placeholder="请输入方案名称"
                className={`w-full px-4 py-2.5 rounded-xl text-sm outline-none
                            border transition-all duration-150
                            bg-[var(--bg-input)] text-[var(--text-primary)]
                            placeholder:text-[var(--text-tertiary)]
                            ${nameError
                              ? 'border-red-500/60 focus:border-red-500/80 bg-red-500/5'
                              : 'border-[var(--border-default)] focus:border-[var(--accent-border)] focus:ring-1 focus:ring-[var(--accent-glow)]'
                            }`}
              />
              <div className="flex items-center justify-between mt-1.5">
                <div>
                  {nameError && (
                    <div className="flex items-center gap-1.5">
                      <AlertCircle size={12} className="text-red-400 shrink-0" />
                      <span className="text-xs text-red-400">{nameError}</span>
                    </div>
                  )}
                </div>
                <span
                  className={`text-xs tabular-nums
                              ${name.length > 45
                                ? 'text-amber-400'
                                : 'text-[var(--text-tertiary)]'
                              }`}
                >
                  {name.length} / 50
                </span>
              </div>
            </div>

            {/* 方案描述 */}
            <div className="mb-8">
              <label className="block text-sm font-medium text-[var(--text-secondary)] mb-1.5">
                方案描述
                <span className="ml-1.5 text-xs text-[var(--text-tertiary)] font-normal">
                  选填
                </span>
              </label>
              <textarea
                rows={4}
                maxLength={200}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="简单描述这个方案的设计目标、风格偏好等..."
                className="w-full px-4 py-2.5 rounded-xl text-sm resize-none outline-none
                           border border-[var(--border-default)]
                           bg-[var(--bg-input)] text-[var(--text-primary)]
                           placeholder:text-[var(--text-tertiary)]
                           focus:border-[var(--accent-border)]
                           focus:ring-1 focus:ring-[var(--accent-glow)]
                           transition-all duration-150"
              />
              <div className="flex justify-end mt-1">
                <span
                  className={`text-xs tabular-nums
                              ${description.length > 180
                                ? 'text-amber-400'
                                : 'text-[var(--text-tertiary)]'
                              }`}
                >
                  {description.length} / 200
                </span>
              </div>
            </div>

            {/* 按钮 */}
            <div className="flex justify-between items-center">
              <div className="opacity-0 pointer-events-none">
                <button className="px-5 py-2.5">占位</button>
              </div>
              <button
                type="button"
                onClick={handleNext}
                className="inline-flex items-center gap-2 px-6 py-2.5
                           rounded-xl text-sm font-semibold
                           bg-[var(--accent)] text-white
                           hover:opacity-90 active:opacity-80
                           transition-opacity duration-150"
              >
                下一步
                <ChevronRight size={16} />
              </button>
            </div>
          </div>
        )}

        {/* ════════ Step 2：房间配置 ════════ */}
        {currentStep === 2 && (
          <div className="space-y-4">

            {/* 户型快速选择 */}
            <div
              className="rounded-2xl border border-[var(--border-default)]
                         bg-[var(--bg-card)] p-6"
            >
              <h3 className="text-sm font-semibold text-[var(--text-primary)] mb-3">
                选择户型
              </h3>
              <div
                className="flex gap-2 overflow-x-auto pb-1
                           [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
              >
                {PRESET_NAMES.map((name) => {
                  const isActive = selectedPreset === name
                  return (
                    <button
                      key={name}
                      type="button"
                      onClick={() => handlePresetSelect(name)}
                      className={`px-4 py-2 rounded-xl text-xs font-medium
                                  border whitespace-nowrap flex-shrink-0
                                  transition-all duration-150
                                  ${isActive
                                    ? 'bg-[var(--accent)] border-[var(--accent)] text-white'
                                    : `bg-transparent border-[var(--border-default)]
                                       text-[var(--text-secondary)]
                                       hover:border-[var(--border-strong)]
                                       hover:bg-[var(--bg-input)]`
                                  }`}
                    >
                      {name}
                    </button>
                  )
                })}
              </div>
            </div>

            {/* 房间数量配置 */}
            <div
              className="rounded-2xl border border-[var(--border-default)]
                         bg-[var(--bg-card)] p-6"
            >
              <div className="mb-4">
                <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                  房间配置
                </h3>
                <p className="text-xs text-[var(--text-tertiary)] mt-0.5">
                  你也可以手动调整各房间数量
                </p>
              </div>

              <div className="space-y-0">
                <RoomRow
                  icon={<BedDouble size={18} />}
                  label="卧室"
                  value={roomConfig.bedroom}
                  min={ROOM_LIMITS.bedroom.min}
                  max={ROOM_LIMITS.bedroom.max}
                  onDec={() => handleRoomCountChange('bedroom', -1)}
                  onInc={() => handleRoomCountChange('bedroom', 1)}
                />
                <RoomRow
                  icon={<Sofa size={18} />}
                  label="客厅"
                  value={roomConfig.livingRoom}
                  min={ROOM_LIMITS.livingRoom.min}
                  max={ROOM_LIMITS.livingRoom.max}
                  onDec={() => handleRoomCountChange('livingRoom', -1)}
                  onInc={() => handleRoomCountChange('livingRoom', 1)}
                />
                <RoomRow
                  icon={<Bath size={18} />}
                  label="卫生间"
                  value={roomConfig.bathroom}
                  min={ROOM_LIMITS.bathroom.min}
                  max={ROOM_LIMITS.bathroom.max}
                  onDec={() => handleRoomCountChange('bathroom', -1)}
                  onInc={() => handleRoomCountChange('bathroom', 1)}
                />
                <RoomRow
                  icon={<Sun size={18} />}
                  label="阳台"
                  value={roomConfig.balcony}
                  min={ROOM_LIMITS.balcony.min}
                  max={ROOM_LIMITS.balcony.max}
                  onDec={() => handleRoomCountChange('balcony', -1)}
                  onInc={() => handleRoomCountChange('balcony', 1)}
                />
              </div>

              {/* 总房间数提示 */}
              <div className="mt-4 pt-4 border-t border-[var(--border-subtle)]">
                {totalRooms === 0 ? (
                  <div className="flex items-center gap-2">
                    <AlertCircle size={14} className="text-amber-400 shrink-0" />
                    <span className="text-xs text-amber-400">
                      至少添加一个房间
                    </span>
                  </div>
                ) : (
                  <span className="text-xs text-[var(--text-tertiary)]">
                    共{' '}
                    <span className="text-[var(--text-primary)] font-semibold">
                      {totalRooms}
                    </span>{' '}
                    个房间
                  </span>
                )}
              </div>
            </div>

            {/* 按钮 */}
            <div className="flex items-center justify-between">
              <button
                type="button"
                onClick={handlePrev}
                className="inline-flex items-center gap-2 px-5 py-2.5
                           rounded-xl text-sm font-medium
                           border border-[var(--border-default)]
                           bg-[var(--bg-card)] text-[var(--text-primary)]
                           hover:bg-[var(--bg-input)]
                           hover:border-[var(--border-strong)]
                           transition-all duration-150"
              >
                <ChevronLeft size={16} />
                上一步
              </button>

              <button
                type="button"
                onClick={handleNext}
                className={`inline-flex items-center gap-2 px-6 py-2.5
                            rounded-xl text-sm font-semibold
                            transition-all duration-150
                            ${totalRooms === 0
                              ? `border border-[var(--border-default)]
                                 bg-[var(--bg-card)] text-[var(--text-secondary)]
                                 hover:bg-[var(--bg-input)]`
                              : `bg-[var(--accent)] text-white
                                 hover:opacity-90 active:opacity-80`
                            }`}
              >
                {totalRooms === 0 ? '跳过房间配置，继续' : '下一步'}
                <ChevronRight size={16} />
              </button>
            </div>
          </div>
        )}

        {/* ════════ Step 3：确认创建 ════════ */}
        {currentStep === 3 && (
          <div className="space-y-4">

            {/* 信息汇总卡片 */}
            <div
              className="rounded-2xl border border-[var(--border-default)]
                         bg-[var(--bg-card)] p-6 space-y-5"
            >
              {/* 方案信息 */}
              <div>
                <h3
                  className="text-xs font-semibold uppercase tracking-wider
                             text-[var(--text-tertiary)] mb-3"
                >
                  方案信息
                </h3>
                <div className="space-y-2.5">
                  <div>
                    <span className="text-xs text-[var(--text-tertiary)]">名称</span>
                    <p className="text-sm font-semibold text-[var(--text-primary)] mt-0.5">
                      {name}
                    </p>
                  </div>
                  <div>
                    <span className="text-xs text-[var(--text-tertiary)]">描述</span>
                    <p
                      className={`text-sm mt-0.5
                                  ${description.trim()
                                    ? 'text-[var(--text-secondary)]'
                                    : 'text-[var(--text-tertiary)] italic'
                                  }`}
                    >
                      {description.trim() || '暂无描述'}
                    </p>
                  </div>
                </div>
              </div>

              {/* 分割线 */}
              <div className="border-t border-[var(--border-subtle)]" />

              {/* 房间配置 */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3
                    className="text-xs font-semibold uppercase tracking-wider
                               text-[var(--text-tertiary)]"
                  >
                    房间配置
                  </h3>
                  <span className="text-xs text-[var(--text-tertiary)]">
                    共 {previewRooms.length} 个
                  </span>
                </div>

                {previewRooms.length === 0 ? (
                  <div className="flex items-center gap-2 py-2">
                    <AlertCircle size={14} className="text-amber-400 shrink-0" />
                    <span className="text-xs text-amber-400">
                      未配置任何房间，仍可继续创建
                    </span>
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-2">
                    {previewRooms.map((room, idx) => (
                      <div
                        key={idx}
                        className="flex items-center gap-2 px-3 py-2 rounded-xl
                                   border border-[var(--border-subtle)]
                                   bg-[var(--bg-input)]"
                      >
                        <span className="text-sm text-[var(--text-primary)] font-medium">
                          {room.name}
                        </span>
                        <span
                          className="text-[10px] text-[var(--text-tertiary)] px-1.5 py-0.5
                                     rounded-md bg-[var(--bg-card)]
                                     border border-[var(--border-subtle)]"
                        >
                          {room.type}
                        </span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* 提交状态区 */}
            {isSubmitting && (
              <div
                className={`rounded-2xl border p-5
                            ${isPartialFail || isCreateFailed
                              ? 'border-amber-500/30 bg-amber-500/5'
                              : isFullSuccess
                                ? 'border-emerald-500/30 bg-emerald-500/5'
                                : 'border-[var(--border-default)] bg-[var(--bg-card)]'
                            }`}
              >
                {/* 创建中 */}
                {isCreating && (
                  <div className="flex items-center gap-3">
                    <Loader2
                      size={20}
                      className="text-[var(--accent)] animate-spin shrink-0"
                    />
                    <div>
                      <p className="text-sm font-medium text-[var(--text-primary)]">
                        正在创建方案...
                      </p>
                      <p className="text-xs text-[var(--text-tertiary)] mt-0.5">
                        正在添加房间，请稍候
                      </p>
                    </div>
                  </div>
                )}

                {/* 全部成功 */}
                {isFullSuccess && (
                  <div className="flex items-center gap-3">
                    <CheckCircle2
                      size={20}
                      className="text-emerald-400 shrink-0"
                      strokeWidth={2}
                    />
                    <div>
                      <p className="text-sm font-semibold text-emerald-400">
                        创建成功！
                      </p>
                      <p className="text-xs text-[var(--text-tertiary)] mt-0.5">
                        正在跳转...
                      </p>
                    </div>
                  </div>
                )}

                {/* 部分房间失败 */}
                {isPartialFail && (
                  <div className="space-y-3">
                    <div className="flex items-center gap-3">
                      <AlertCircle
                        size={20}
                        className="text-amber-400 shrink-0"
                      />
                      <p className="text-sm font-medium text-amber-400">
                        {submitError}
                      </p>
                    </div>
                    <div className="space-y-1.5 pl-8">
                      {failedRooms.map((r, i) => (
                        <div
                          key={i}
                          className="flex items-center gap-2 text-xs
                                     text-[var(--text-secondary)]"
                        >
                          <span className="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0" />
                          <span>{r.error ?? '未知错误'}</span>
                        </div>
                      ))}
                    </div>
                    <p className="text-xs text-[var(--text-tertiary)] pl-8">
                      即将跳转到方案列表...
                    </p>
                  </div>
                )}

                {/* 方案创建本身失败 */}
                {isCreateFailed && (
                  <div className="space-y-3">
                    <div className="flex items-start gap-3">
                      <AlertCircle
                        size={20}
                        className="text-red-400 shrink-0 mt-0.5"
                      />
                      <div>
                        <p className="text-sm font-medium text-red-400">
                          {submitError}
                        </p>
                        <p className="text-xs text-[var(--text-tertiary)] mt-1">
                          方案创建失败，请检查网络后重试
                        </p>
                      </div>
                    </div>
                    <div className="pl-8">
                      <button
                        type="button"
                        onClick={() => {
                          setIsSubmitting(false)
                          setSubmitError('')
                        }}
                        className="px-4 py-1.5 rounded-lg text-xs font-medium
                                   border border-red-500/30 text-red-400
                                   hover:bg-red-500/10 transition-colors"
                      >
                        重试
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* 按钮区 */}
            <div className="flex items-center justify-between">
              <button
                type="button"
                onClick={handlePrev}
                disabled={isSubmitting}
                className={`inline-flex items-center gap-2 px-5 py-2.5
                            rounded-xl text-sm font-medium
                            border border-[var(--border-default)]
                            bg-[var(--bg-card)] text-[var(--text-primary)]
                            hover:bg-[var(--bg-input)]
                            hover:border-[var(--border-strong)]
                            disabled:opacity-40 disabled:cursor-not-allowed
                            transition-all duration-150`}
              >
                <ChevronLeft size={16} />
                上一步
              </button>

              <button
                type="button"
                onClick={handleSubmit}
                disabled={isSubmitting}
                className="inline-flex items-center gap-2 px-6 py-2.5
                           rounded-xl text-sm font-semibold min-w-[120px] justify-center
                           bg-[var(--accent)] text-white
                           hover:opacity-90 active:opacity-80
                           disabled:opacity-60 disabled:cursor-not-allowed
                           transition-all duration-150"
              >
                {isCreating ? (
                  <>
                    <Loader2 size={15} className="animate-spin" />
                    创建中...
                  </>
                ) : (
                  '确认创建'
                )}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}