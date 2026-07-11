// 作用：定义 AI 生成页内部使用的图片来源和表单类型。
export type AssetSource = 'local' | 'gallery' | 'project' | 'history'

export interface GenerationAsset {
  source: AssetSource
  id: string
  label: string
  previewUrl: string
  file?: File
  fullUrl?: string
  imageId?: string
  aiImageId?: number
  isAi?: boolean
}

export interface GenerationFormState {
  prompt: string
  negativePrompt: string
  roomType: string
  aspectRatio: string
  resolution: string
  batchSize: number
  seed: string
  duration: number
}
