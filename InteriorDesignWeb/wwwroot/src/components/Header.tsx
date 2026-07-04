import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Sun, Moon, Sparkles, LogOut, User, ChevronDown } from 'lucide-react';
import { useAppStore } from '@/store/useAppStore';
import { Button } from './ui/Button';
import { cn } from '@/utils/cn';

const NAV_LINKS = [
  { label: '功能', href: '#features' },
  { label: '案例', href: '#gallery' },
  { label: '定价', href: '#pricing' },
  { label: '关于', href: '#about' },
];

export function Header() {
  const { resolvedTheme, toggleTheme, user, openLoginModal, logout } = useAppStore();
  const [scrolled, setScrolled] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const container = document.querySelector('.snap-container');
    const handler = () => {
      const scrollTop = container ? container.scrollTop : window.scrollY;
      setScrolled(scrollTop > 20);
    };
    const scrollTarget = document.querySelector('.snap-container') || window;
    scrollTarget.addEventListener('scroll', handler, { passive: true });
    return () => scrollTarget.removeEventListener('scroll', handler);
  }, []);

  return (
    <header
      className={cn(
        'fixed top-0 left-0 right-0 z-50 transition-all duration-300',
        scrolled
          ? 'glass border-b border-[var(--glass-border)] shadow-[var(--shadow-card)]'
          : 'bg-transparent'
      )}
    >
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between gap-6">
        {/* Logo */}
        <Link to="/" className="flex items-center gap-2 shrink-0 group">
          <div className="w-7 h-7 rounded-lg bg-[var(--accent)] flex items-center justify-center shadow-[0_0_12px_var(--accent-glow)] group-hover:shadow-[0_0_20px_var(--accent-glow)] transition-all">
            <Sparkles size={14} className="text-white" />
          </div>
          <span className="text-sm font-semibold text-[var(--text-primary)] tracking-tight">
            Design<span className="text-[var(--accent)] [data-theme='light']_&:text-[#4CABF6]">AI</span>
          </span>
        </Link>

        {/* Nav Pill */}
        <nav className="hidden md:flex items-center nav-pill rounded-full px-1 py-1 gap-0.5">
          {NAV_LINKS.map((link) => (
            <a
              key={link.label}
              href={link.href}
              className="px-4 py-1.5 text-sm text-[var(--text-secondary)] hover:text-[var(--text-primary)] rounded-full transition-all duration-200 hover:bg-[var(--bg-card)]"
              onClick={(e) => {
                e.preventDefault();
                const el = document.querySelector(link.href);
                el?.scrollIntoView({ behavior: 'smooth' });
              }}
            >
              {link.label}
            </a>
          ))}
        </nav>

        {/* Right actions */}
        <div className="flex items-center gap-2 shrink-0">
          {/* Theme toggle */}
          <button
            onClick={toggleTheme}
            className="w-9 h-9 rounded-xl flex items-center justify-center text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)] border border-transparent hover:border-[var(--border-subtle)] transition-all duration-200"
            title={resolvedTheme === 'dark' ? '切换到亮色模式' : '切换到暗色模式'}
          >
            {resolvedTheme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
          </button>

          {user ? (
            <div className="relative">
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-2 h-9 px-3 rounded-xl text-sm text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)] border border-[var(--border-subtle)] transition-all duration-200"
              >
                <div className="w-5 h-5 rounded-full bg-[var(--accent)] flex items-center justify-center">
                  <User size={11} className="text-white" />
                </div>
                <span className="hidden sm:block max-w-[100px] truncate">{user.name}</span>
                <ChevronDown size={12} className={cn('transition-transform', userMenuOpen && 'rotate-180')} />
              </button>

              {userMenuOpen && (
                <div className="absolute right-0 top-full mt-2 w-48 rounded-xl bg-[var(--bg-card)] border border-[var(--border-default)] shadow-[var(--shadow-elevated)] py-1 animate-slide-up">
                  <div className="px-3 py-2 border-b border-[var(--border-subtle)]">
                    <p className="text-xs font-medium text-[var(--text-primary)] truncate">{user.name}</p>
                    <p className="text-xs text-[var(--text-tertiary)] truncate">{user.email}</p>
                  </div>
                  <button
                    onClick={() => { navigate('/app/projects'); setUserMenuOpen(false); }}
                    className="w-full text-left px-3 py-2 text-sm text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card-hover)] transition-colors flex items-center gap-2"
                  >
                    <User size={13} /> 我的方案
                  </button>
                  <button
                    onClick={() => { logout(); setUserMenuOpen(false); }}
                    className="w-full text-left px-3 py-2 text-sm text-red-400 hover:text-red-300 hover:bg-[var(--bg-card-hover)] transition-colors flex items-center gap-2"
                  >
                    <LogOut size={13} /> 退出登录
                  </button>
                </div>
              )}
            </div>
          ) : (
            <>
              <Button variant="ghost" size="sm" onClick={openLoginModal}>
                登录
              </Button>
              <Button
                variant="primary"
                size="sm"
                onClick={() => navigate('/app/generate')}
              >
                开始使用
              </Button>
            </>
          )}
        </div>
      </div>
    </header>
  );
}
