import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import LandingPage from './pages/LandingPage'
import AppLayout from './layouts/AppLayout'
import GeneratePage from './pages/app/GeneratePage'
import ProjectsPage from './pages/ProjectsPage'
import NewProjectPage from './pages/NewProjectPage'
import GalleryPage from './pages/GalleryPage'
import AIAssistantPage from './pages/app/AIAssistantPage'

const router = createBrowserRouter([
  {
    path: '/',
    element: <LandingPage />,
  },
  {
    path: '/app',
    element: <AppLayout />,
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
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
