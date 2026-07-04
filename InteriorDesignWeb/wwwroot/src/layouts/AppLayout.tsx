import { Outlet } from 'react-router-dom';
import { Sidebar } from '@/components/Sidebar';
import { LoginModal } from '@/components/LoginModal';

export default function AppLayout() {
  return (
    <div className="flex h-screen overflow-hidden bg-[var(--bg-base)]">
      <Sidebar />
      <main className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <div className="flex-1 overflow-y-auto">
          <Outlet />
        </div>
      </main>
      <LoginModal />
    </div>
  );
}
