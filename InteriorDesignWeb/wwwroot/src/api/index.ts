// src/api/index.ts
// 统一导出入口，外部模块只需从此处引入

export { request, requestWithAuth } from './client'

export {
  login,
  logout,
  initAuth,
  getCurrentUser,
  getToken,
  isTokenValid,
  type AuthUser,
} from './auth'

export { projectsApi } from './modules/projects'
export { imagesApi }   from './modules/images'
export { roomsApi }    from './modules/rooms'
export { adminApi }    from './modules/admin'
