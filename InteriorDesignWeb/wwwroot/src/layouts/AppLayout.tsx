import { Outlet, useLocation } from 'react-router-dom';
import { Sidebar } from '@/components/Sidebar';
import { LoginModal } from '@/components/LoginModal';

export default function AppLayout() {
  const location = useLocation();
  const pageKey = location.pathname.split('/').slice(0, 3).join('/');
  return (
    <div className="flex h-screen overflow-hidden bg-[var(--bg-base)]">
      <Sidebar />
      <main className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <div className="flex-1 overflow-y-auto">
          <div key={pageKey} className="app-page-enter h-full min-h-full">
            <Outlet />
          </div>
        </div>
      </main>
      <LoginModal />
    </div>
  );
}
