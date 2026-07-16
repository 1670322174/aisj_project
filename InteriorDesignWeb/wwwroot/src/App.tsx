import { useEffect, useState, type ReactNode } from 'react'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { Loader2, ShieldAlert } from 'lucide-react'
import LandingPage from './pages/LandingPage'
import AppLayout from './layouts/AppLayout'
import GeneratePage from './pages/app/GeneratePage'
import ProjectsPage from './pages/ProjectsPage'
import NewProjectPage from './pages/NewProjectPage'
import GalleryPage from './pages/GalleryPage'
import AIAssistantPage from './pages/app/AIAssistantPageV2'
import RouteErrorPage from './components/RouteErrorPage'
import AdminPage from './pages/app/AdminPage'
import { useAppStore } from './store/useAppStore'

function AdminGuard({ children }: { children: ReactNode }) {
  const authUser = useAppStore((state) => state.authUser)
  const initAuthAction = useAppStore((state) => state.initAuthAction)
  const openLoginModal = useAppStore((state) => state.openLoginModal)
  const [checked, setChecked] = useState(Boolean(authUser))

  useEffect(() => {
    if (authUser) {
      setChecked(true)
      return
    }
    let active = true
    void initAuthAction().finally(() => active && setChecked(true))
    return () => { active = false }
  }, [authUser, initAuthAction])

  if (!checked) {
    return <div className="flex h-full min-h-[420px] items-center justify-center gap-3 text-sm text-[var(--text-tertiary)]"><Loader2 size={19} className="animate-spin text-[var(--accent)]" />正在验证管理员身份…</div>
  }

  if (authUser?.role?.toLowerCase() !== 'administrator') {
    return (
      <div className="flex h-full min-h-[520px] items-center justify-center p-6">
        <div className="max-w-md rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] p-8 text-center shadow-[var(--shadow-card)]">
          <ShieldAlert size={30} className="mx-auto text-amber-400" />
          <h1 className="mt-4 text-lg font-semibold text-[var(--text-primary)]">需要管理员权限</h1>
          <p className="mt-2 text-sm leading-6 text-[var(--text-secondary)]">管理中心包含用户、系统与存储操作，仅 Administrator 账号可以访问。</p>
          {!authUser && <button type="button" onClick={openLoginModal} className="mt-5 rounded-xl bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white">管理员登录</button>}
        </div>
      </div>
    )
  }

  return children
}

const router = createBrowserRouter([
  {
    path: '/',
    element: <LandingPage />,
    errorElement: <RouteErrorPage />,
  },
  {
    path: '/app',
    element: <AppLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <Navigate to="generate/text" replace /> },
      { path: 'generate', element: <Navigate to="/app/generate/text" replace /> },
      { path: 'generate/:mode', element: <GeneratePage /> },
      { path: 'generate/:mode/jobs/:jobId', element: <GeneratePage /> },
      { path: 'gallery', element: <GalleryPage /> },
      { path: 'new', element: <NewProjectPage /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: 'projects/:projectId', element: <ProjectsPage /> },
      { path: 'assistant', element: <AIAssistantPage /> },
      { path: 'assistant/:conversationId', element: <AIAssistantPage /> },
      { path: 'admin', element: <AdminGuard><AdminPage /></AdminGuard> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
