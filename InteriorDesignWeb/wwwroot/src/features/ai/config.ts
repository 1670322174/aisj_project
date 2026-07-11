// 作用：集中维护 AI 生成页的展示配置和默认值。
// 工作流是否可用仍以后端 /api/ai/generations/options 返回结果为准。
export type GenerationMode = 'text' | 'image' | 'video'

/**
 * 默认负面提示词占位。
 * 请在正式上线前替换为项目实际使用的负面提示词内容。
 */
export const DEFAULT_NEGATIVE_PROMPT = 'low quality,blurry,noise,grainy,overexposed,underexposed,bad lighting,bad composition,distorted,deformed,ugly,messy,unrealistic,cartoon,anime,'

export const MODE_LABELS: Record<GenerationMode, string> = {
  text: '文生图',
  image: '图生图',
  video: '图生视频',
}

export const WORKFLOW_MODE: Record<string, GenerationMode> = {
  api_grok_image_edit: 'text',
  api_banana_image: 'image',
  api_bria_image_edit: 'image',
  api_luma_image_edit: 'image',
  api_seedream_image_edit: 'image',
  api_seedance2: 'video',
  api_veo3: 'video',
}

export const ROOM_TYPES = [
  { value: '', label: '不指定' },
  { value: 'living_room', label: '客厅' },
  { value: 'bedroom', label: '卧室' },
  { value: 'dining_room', label: '餐厅' },
  { value: 'kitchen', label: '厨房' },
  { value: 'bathroom', label: '卫生间' },
  { value: 'study', label: '书房' },
  { value: 'balcony', label: '阳台' },
  { value: 'entrance', label: '玄关' },
  { value: 'other', label: '其他' },
]

export const ASPECT_RATIOS = [
  { value: 'auto', label: '自动' },
  { value: '1:1', label: '1:1' },
  { value: '4:3', label: '4:3' },
  { value: '3:4', label: '3:4' },
  { value: '16:9', label: '16:9' },
  { value: '9:16', label: '9:16' },
]

export const IMAGE_RESOLUTIONS = [
  { value: '1K', label: '1K' },
  { value: '2K', label: '2K' },
  { value: '1024x1024', label: '1024 × 1024' },
  { value: '1536x1024', label: '1536 × 1024' },
  { value: '1024x1536', label: '1024 × 1536' },
]

export const VIDEO_RESOLUTIONS = [
  { value: '720p', label: '720p' },
  { value: '1080p', label: '1080p' },
]

export const STYLE_TAGS = ['北欧极简', '日式侘寂', '法式轻奢', '工业复古', '现代简约', '美式乡村']

export function modeFromRoute(value?: string): GenerationMode {
  return value === 'image' || value === 'video' ? value : 'text'
}

export function roomLabel(value: string): string {
  return ROOM_TYPES.find((item) => item.value === value)?.label ?? value
}
