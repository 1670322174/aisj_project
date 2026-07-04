import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import LandingPage from './pages/LandingPage'
import AppLayout from './layouts/AppLayout'
import GeneratePage from './pages/app/GeneratePage'
import ProjectsPage from './pages/ProjectsPage'
import NewProjectPage from './pages/NewProjectPage'
import GalleryPage from './pages/GalleryPage'

const router = createBrowserRouter([
  {
    path: '/',
    element: <LandingPage />,
  },
  {
    path: '/app',
    element: <AppLayout />,
    children: [
      { index: true, element: <GeneratePage /> },
      { path: 'generate', element: <GeneratePage /> },
      { path: 'gallery', element: <GalleryPage /> },
      { path: 'new', element: <NewProjectPage /> },
      { path: 'projects', element: <ProjectsPage /> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
