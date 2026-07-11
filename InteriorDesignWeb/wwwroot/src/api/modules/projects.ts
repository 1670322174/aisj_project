// src/api/modules/projects.ts
import { requestWithAuth } from '../client'
import { fetchAuthenticatedMedia } from '../media'

/* ─────────────────────────────────────────
   类型定义
───────────────────────────────────────── */
export interface Project {
  projectID:   string
  name:        string
  description: string
  createdAt:   string
}

type RawProject = {
  projectID?: string | number
  ProjectID?: string | number
  name?: string
  Name?: string
  description?: string | null
  Description?: string | null
  createdAt?: string
  CreatedAt?: string
}

export interface ProjectImage {
  relationID:   string
  imageID:      string | number | null
  aiImageID:    string | number | null
  room:         string | null
  thumbnailUrl: string
  fullImageUrl: string
  fileName:     string
  sourceType?:  string
}

export interface NormalizedProjectImage {
  relationID:   string
  imageId:      string   // imageID ?? aiImageID，取非空的那个
  isAi:         boolean  // aiImageID 不为 null 则为 true
  imageID:      string | null
  aiImageID:    string | null
  sourceType:   'ai' | 'gallery'
  room:         string   // 空/null/'null' 统一为 'unclassified'
  thumbnailUrl: string
  fullImageUrl: string
  fileName:     string
}

/* ─────────────────────────────────────────
   后端响应结构（兼容大小写混用）
───────────────────────────────────────── */
type ApiResponse<T> = {
  success?: boolean
  Success?: boolean
  data?:    T
  Data?:    T
  message?: string
  Message?: string
}

/* ─────────────────────────────────────────
   工具：安全取响应体中的 data 字段
───────────────────────────────────────── */
function extractData<T>(resp: unknown): T {
  const r = resp as ApiResponse<T>
  const data = r.data ?? r.Data
  if (data !== undefined && data !== null) return data as T
  // 如果后端直接返回数组或对象（无包装层），直接使用
  return resp as T
}

function normalizeProject(raw: RawProject): Project {
  return {
    projectID: String(raw.projectID ?? raw.ProjectID ?? ''),
    name: raw.name ?? raw.Name ?? '',
    description: raw.description ?? raw.Description ?? '',
    createdAt: raw.createdAt ?? raw.CreatedAt ?? '',
  }
}

/* ─────────────────────────────────────────
   私有工具：标准化单张图片
───────────────────────────────────────── */
function normalizeProjectImage(raw: ProjectImage): NormalizedProjectImage {
  const imageID = raw.imageID === null || raw.imageID === undefined ? null : String(raw.imageID)
  const aiImageID = raw.aiImageID === null || raw.aiImageID === undefined ? null : String(raw.aiImageID)
  const imageId = imageID ?? aiImageID ?? ''

  // isAi：aiImageID 存在且非空
  const isAi = aiImageID !== null

  // room：空字符串 / null / 字面量 'null' 统一归为 unclassified
  const roomRaw = (raw.room ?? '').trim().toLowerCase()
  const room    = roomRaw === '' || roomRaw === 'null' ? 'unclassified' : roomRaw

  return {
    relationID:   String(raw.relationID ?? ''),
    imageId,
    isAi,
    imageID,
    aiImageID,
    sourceType: isAi ? 'ai' : 'gallery',
    room,
    thumbnailUrl: raw.thumbnailUrl ?? '',
    fullImageUrl: raw.fullImageUrl ?? '',
    fileName:     raw.fileName     ?? '',
  }
}

/* ─────────────────────────────────────────
   getUserProjects
   GET /projects
───────────────────────────────────────── */
async function getUserProjects(): Promise<Project[]> {
  const resp = await requestWithAuth('/projects', { method: 'GET' })
  const projects = extractData<RawProject[]>(resp) ?? []
  return projects.map(normalizeProject)
}

/* ─────────────────────────────────────────
   getProjectImages
   GET /projects/:projectId/images
───────────────────────────────────────── */
async function getProjectImages(
  projectId: string,
): Promise<NormalizedProjectImage[]> {
  const resp = await requestWithAuth(
    `/projects/${projectId}/images`,
    { method: 'GET' },
  )
  const raw = extractData<ProjectImage[]>(resp) ?? []
  return raw.map(normalizeProjectImage)
}

function fetchProjectImageMedia(
  projectId: string,
  relationId: string,
  type: 'thumbnail' | 'original',
): Promise<string> {
  const endpoint = `/projects/${encodeURIComponent(projectId)}/images/${encodeURIComponent(relationId)}/file?type=${type}`
  return fetchAuthenticatedMedia(endpoint, `project-image:${projectId}:${relationId}:${type}`)
}

/* ─────────────────────────────────────────
   deleteProject
   DELETE /projects/:projectId
───────────────────────────────────────── */
async function deleteProject(projectId: string): Promise<void> {
  await requestWithAuth(`/projects/${projectId}`, { method: 'DELETE' })
}

/* ─────────────────────────────────────────
   removeImageFromProject
   DELETE /projects/:projectId/images/:relationId
───────────────────────────────────────── */
async function removeImageFromProject(
  projectId:  string,
  relationId: string,
): Promise<void> {
  await requestWithAuth(
    `/projects/${projectId}/images/${relationId}`,
    { method: 'DELETE' },
  )
}

/* ─────────────────────────────────────────
   createProject
   POST /projects
   请求体字段名首字母大写（与登录接口保持一致）
───────────────────────────────────────── */
async function createProject(data: {
  name:        string
  description: string
}): Promise<Project> {
  const resp = await requestWithAuth('/projects', {
    method: 'POST',
    body:   JSON.stringify({
      Name:        data.name,
      Description: data.description,
    }),
  })
  return normalizeProject(extractData<RawProject>(resp))
}

/* ─────────────────────────────────────────
   groupImagesByRoom（导出工具函数）
   按 room 字段分组，返回 Map
   key 为 room 字符串，未分类的 key 为 'unclassified'
───────────────────────────────────────── */
export function groupImagesByRoom(
  images: NormalizedProjectImage[],
): Map<string, NormalizedProjectImage[]> {
  const map = new Map<string, NormalizedProjectImage[]>()

  for (const img of images) {
    const key = img.room  // normalizeProjectImage 已保证空值统一为 'unclassified'
    const group = map.get(key)
    if (group) {
      group.push(img)
    } else {
      map.set(key, [img])
    }
  }

  return map
}

/* ─────────────────────────────────────────
   导出
───────────────────────────────────────── */
export const projectsApi = {
  getUserProjects,
  getProjectImages,
  fetchProjectImageMedia,
  deleteProject,
  removeImageFromProject,
  createProject,
  groupImagesByRoom,
  addRoom,
  addRoomsToProject,
  getProjectRooms,
  addImageToProject,
  addAiImageToProject,
}

// ─── 创建方案功能-新增内容追加到 src/api/modules/projects.ts 末尾 ───

/* ─────────────────────────────────────────
   新增类型
───────────────────────────────────────── */
export interface Room {
  roomID:       string
  projectID:    string
  parentRoomID: string | null
  name:         string
  type:         string
  orderIndex:   number
}

export interface RoomConfig {
  bedroom:    number
  livingRoom: number
  bathroom:   number
  balcony:    number
}

export interface RoomInput {
  Name: string
  Type: string
}

export interface RoomResult {
  room:     Room | null
  success:  boolean
  error?:   string
}

/* ─────────────────────────────────────────
   私有工具：根据户型配置生成房间输入列表
───────────────────────────────────────── */
function generateRoomInputs(config: RoomConfig): RoomInput[] {
  const rooms: RoomInput[] = []

  /* ── 卧室 ── */
  if (config.bedroom === 1) {
    rooms.push({ Name: '主卧', Type: 'bedroom' })
  } else if (config.bedroom === 2) {
    rooms.push(
      { Name: '主卧', Type: 'bedroom' },
      { Name: '次卧', Type: 'bedroom' },
    )
  } else if (config.bedroom === 3) {
    rooms.push(
      { Name: '主卧',  Type: 'bedroom' },
      { Name: '次卧1', Type: 'bedroom' },
      { Name: '次卧2', Type: 'bedroom' },
    )
  } else if (config.bedroom >= 4) {
    rooms.push(
      { Name: '主卧',  Type: 'bedroom' },
      { Name: '次卧1', Type: 'bedroom' },
      { Name: '次卧2', Type: 'bedroom' },
      { Name: '次卧3', Type: 'bedroom' },
    )
  }

  /* ── 客厅 / 餐厅 ── */
  if (config.livingRoom === 1) {
    rooms.push({ Name: '客厅', Type: 'living_room' })
  } else if (config.livingRoom >= 2) {
    rooms.push(
      { Name: '客厅', Type: 'living_room' },
      { Name: '餐厅', Type: 'living_room' },
    )
  }

  /* ── 卫生间 ── */
  if (config.bathroom === 1) {
    rooms.push({ Name: '卫生间', Type: 'bathroom' })
  } else if (config.bathroom >= 2) {
    rooms.push(
      { Name: '主卫', Type: 'bathroom' },
      { Name: '次卫', Type: 'bathroom' },
    )
  }

  /* ── 阳台 ── */
  if (config.balcony === 1) {
    rooms.push({ Name: '阳台', Type: 'balcony' })
  } else if (config.balcony >= 2) {
    rooms.push(
      { Name: '主阳台', Type: 'balcony' },
      { Name: '次阳台', Type: 'balcony' },
    )
  }

  return rooms
}

/* ─────────────────────────────────────────
   addRoom
   POST /projects/:projectId/rooms
───────────────────────────────────────── */
async function addRoom(projectId: string, input: RoomInput): Promise<Room> {
  const resp = await requestWithAuth(`/projects/${projectId}/rooms`, {
    method: 'POST',
    body:   JSON.stringify({ Name: input.Name, Type: input.Type }),
  })
  return extractData<Room>(resp)
}

/* ─────────────────────────────────────────
   addRoomsToProject
   并发添加多个房间，每个独立处理错误
───────────────────────────────────────── */
async function addRoomsToProject(
  projectId: string,
  config:    RoomConfig,
): Promise<RoomResult[]> {
  const inputs = generateRoomInputs(config)

  const results = await Promise.all(
    inputs.map(async (input): Promise<RoomResult> => {
      try {
        const room = await addRoom(projectId, input)
        return { room, success: true }
      } catch (err) {
        const error = err instanceof Error ? err.message : '添加房间失败'
        return { room: null, success: false, error }
      }
    }),
  )

  return results
}


// ─── 将图片添加到方案中-新增内容追加到 src/api/modules/projects.ts ───

/* ─────────────────────────────────────────
   新增类型
───────────────────────────────────────── */
export interface AddImagePayload {
  RoomID:     number
  ImageID:    number
  aiImageID:  number
  CustomTags: string[]
}

export interface AddImageResult {
  imageId:  string
  success:  boolean
  error?:   string
}

/* ─────────────────────────────────────────
   中文房间名 → 英文 type 映射表
───────────────────────────────────────── */
const ROOM_TYPE_MAP: Record<string, string> = {
  '客厅':  'living_room',
  '主卧':  'bedroom',
  '卧室':  'bedroom',
  '次卧':  'bedroom',
  '卫生间': 'bathroom',
  '主卫':  'bathroom',
  '次卫':  'bathroom',
  '阳台':  'balcony',
  '主阳台': 'balcony',
  '次阳台': 'balcony',
  '书房':  'study',
  '餐厅':  'dining_room',
  '厨房':  'kitchen',
  '玄关':  'entrance',
}

/* ─────────────────────────────────────────
   私有工具：中文 room → 英文 type
───────────────────────────────────────── */
function getRoomType(chineseRoom: string): string {
  const trimmed = chineseRoom.trim()
  return ROOM_TYPE_MAP[trimmed] ?? trimmed.toLowerCase()
}

/* ─────────────────────────────────────────
   私有工具：Room[] → Map<type, roomID>
───────────────────────────────────────── */
function buildRoomTypeMap(rooms: Room[]): Map<string, number> {
  const map = new Map<string, number>()
  for (const room of rooms) {
    // 接口可能返回字符串形式的 ID，统一转为 number
    const id = parseInt(String(room.roomID), 10)
    if (!isNaN(id)) {
      map.set(room.type, id)
    }
  }
  return map
}

/* ─────────────────────────────────────────
   私有工具：中文 room → roomID（找不到返回 0）
───────────────────────────────────────── */
function matchRoomId(
  chineseRoom: string,
  roomTypeMap: Map<string, number>,
): number {
  const type = getRoomType(chineseRoom)
  return roomTypeMap.get(type) ?? 0
}

/* ─────────────────────────────────────────
   getProjectRooms
   GET /projects/:projectId/rooms
   失败时静默返回空数组（降级为未分类）
───────────────────────────────────────── */
async function getProjectRooms(projectId: string): Promise<Room[]> {
  try {
    const resp = await requestWithAuth(
      `/projects/${projectId}/rooms`,
      { method: 'GET' },
    )
    return extractData<Room[]>(resp) ?? []
  } catch (err) {
    console.warn('[projectsApi] getProjectRooms 失败，降级为空房间列表:', err)
    return []
  }
}

/* ─────────────────────────────────────────
   addImageToProject
   自动匹配房间后 POST /projects/:projectId/images
───────────────────────────────────────── */
async function addImageToProject(
  projectId: string,
  imageId:   string,
  room:      string,
): Promise<void> {
  // 1. 获取方案下所有房间
  const rooms = await getProjectRooms(projectId)

  // 2. 建立 type → roomID 映射
  const roomTypeMap = buildRoomTypeMap(rooms)

  // 3. 匹配 roomID（匹配不到返回 0 → 未分类）
  const roomId = matchRoomId(room, roomTypeMap)

  // 4. 构造请求体
  const payload: AddImagePayload = {
    RoomID:     roomId,
    ImageID:    parseInt(imageId, 10) || 0,
    aiImageID:  0,
    CustomTags: [],
  }

  // 5. 发送请求
  await requestWithAuth(`/projects/${projectId}/images`, {
    method: 'POST',
    body:   JSON.stringify(payload),
  })
}



/* ─────────────────────────────────────────
   addAiImageToProject
   将 AI 生成结果加入当前项目。RoomID 会一并发送；
   是否写入房间取决于当前后端 ProjectImagesController 的实现。
───────────────────────────────────────── */
async function addAiImageToProject(
  projectId: string,
  aiImageId: number,
  roomId: number | null = null,
): Promise<void> {
  await requestWithAuth(`/projects/${projectId}/images`, {
    method: 'POST',
    body: JSON.stringify({
      RoomID: roomId ?? 0,
      ImageID: 0,
      AiImageID: aiImageId,
      CustomTags: [],
    }),
  })
}
