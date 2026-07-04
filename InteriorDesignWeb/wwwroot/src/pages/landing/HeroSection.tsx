import { useNavigate } from 'react-router-dom';
import { ArrowRight, Play, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useAppStore } from '@/store/useAppStore';

export function HeroSection() {
  const navigate = useNavigate();
  const { openLoginModal, user } = useAppStore();

  return (
    <section
      id="hero"
      className="snap-section relative flex flex-col items-center justify-center overflow-hidden px-6 text-center"
    >
      {/* Background glow layers */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[500px] rounded-full glow-accent opacity-60" />
        <div className="absolute top-[30%] left-[20%] w-[300px] h-[300px] rounded-full bg-[var(--accent-glow)] blur-[80px] opacity-30" />
        <div className="absolute top-[60%] right-[15%] w-[200px] h-[200px] rounded-full bg-[var(--accent-glow)] blur-[60px] opacity-20" />
      </div>

      {/* Grid lines decoration */}
      <div
        className="absolute inset-0 opacity-[0.03]"
        style={{
          backgroundImage: `linear-gradient(var(--text-primary) 1px, transparent 1px), linear-gradient(90deg, var(--text-primary) 1px, transparent 1px)`,
          backgroundSize: '60px 60px',
        }}
      />

      {/* Badge */}
      <div className="relative mb-6 flex items-center gap-2 px-3 py-1.5 rounded-full glass border border-[var(--border-default)] text-xs text-[var(--text-secondary)]">
        <Sparkles size={12} className="text-[var(--accent)]" style={{ color: 'var(--accent)' }} />
        <span>全新 AI 生成引擎 v2.0 上线</span>
        <span className="text-[var(--accent)] font-medium" style={{ color: 'var(--accent)' }}>了解更多 →</span>
      </div>

      {/* Main title */}
      <h1 className="relative max-w-4xl text-5xl sm:text-6xl md:text-7xl font-bold leading-[1.08] tracking-tight mb-6">
        <span className="text-[var(--text-primary)]">让 AI 重新</span>
        <br />
        <span
          className="relative inline-block"
          style={{
            background: 'linear-gradient(135deg, var(--text-primary) 0%, rgba(255,255,255,0.6) 100%)',
            WebkitBackgroundClip: 'text',
            WebkitTextFillColor: 'transparent',
            backgroundClip: 'text',
          }}
        >
          定义你的空间
        </span>
      </h1>

      {/* Subtitle */}
      <p className="relative max-w-xl text-base sm:text-lg text-[var(--text-secondary)] leading-relaxed mb-8">
        描述你的理想居所，上传参考图片，AI 在秒级内为你生成专业级室内设计方案。
        <br className="hidden sm:block" />
        从想象到现实，从未如此简单。
      </p>

      {/* CTA Buttons */}
      <div className="relative flex flex-col sm:flex-row items-center gap-3">
        <Button
          variant="primary"
          size="lg"
          className="w-full sm:w-auto px-8 text-base"
          onClick={() => navigate('/app/generate')}
        >
          免费开始设计
          <ArrowRight size={16} />
        </Button>
        <Button
          variant="glass"
          size="lg"
          className="w-full sm:w-auto px-8 text-base"
          onClick={user ? () => navigate('/app/gallery') : openLoginModal}
        >
          <Play size={14} />
          观看演示
        </Button>
      </div>

      {/* Stats row */}
      <div className="relative mt-16 flex items-center gap-8 sm:gap-12 text-center">
        {[
          { value: '50K+', label: '设计方案生成' },
          { value: '3s', label: '平均生成时间' },
          { value: '12+', label: '设计风格' },
          { value: '98%', label: '用户满意度' },
        ].map((stat) => (
          <div key={stat.label} className="flex flex-col gap-0.5">
            <span className="text-xl sm:text-2xl font-bold text-[var(--text-primary)]">{stat.value}</span>
            <span className="text-xs text-[var(--text-tertiary)] whitespace-nowrap">{stat.label}</span>
          </div>
        ))}
      </div>

      {/* Scroll hint */}
      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 flex flex-col items-center gap-2 text-[var(--text-tertiary)] animate-bounce">
        <span className="text-xs">向下滚动</span>
        <div className="w-4 h-7 rounded-full border border-[var(--border-default)] flex items-start justify-center pt-1.5">
          <div className="w-0.5 h-2 rounded-full bg-[var(--text-tertiary)]" />
        </div>
      </div>
    </section>
  );
}
