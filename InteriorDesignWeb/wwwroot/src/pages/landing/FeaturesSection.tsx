import { Wand2, ImagePlus, Layers, Zap, Palette, SlidersHorizontal } from 'lucide-react';

const FEATURES = [
  {
    icon: Wand2,
    title: '文字生成设计',
    desc: '用自然语言描述你的想法，AI 理解语义，秒级生成符合审美的室内设计方案。',
    gradient: 'from-violet-500/10 to-blue-500/5',
  },
  {
    icon: ImagePlus,
    title: '图片风格迁移',
    desc: '上传参考图片，AI 分析风格元素，将其融入你的空间，精准还原设计意图。',
    gradient: 'from-blue-500/10 to-cyan-500/5',
  },
  {
    icon: Layers,
    title: '多方案对比',
    desc: '一次输入，同时生成多个方案，横向对比不同设计路线，找到最心仪的那一个。',
    gradient: 'from-cyan-500/10 to-teal-500/5',
  },
  {
    icon: Zap,
    title: '极速渲染引擎',
    desc: '自研推理加速技术，3 秒出图，8K 分辨率输出，告别漫长等待。',
    gradient: 'from-amber-500/10 to-orange-500/5',
  },
  {
    icon: Palette,
    title: '专业配色系统',
    desc: '内置 500+ 专业配色方案，AI 自动匹配空间氛围，色彩搭配不再是难题。',
    gradient: 'from-rose-500/10 to-pink-500/5',
  },
  {
    icon: SlidersHorizontal,
    title: '细节精调控制',
    desc: '风格、色调、家具密度、光线氛围，每一个维度都可以精确控制和调整。',
    gradient: 'from-green-500/10 to-emerald-500/5',
  },
];

export function FeaturesSection() {
  return (
    <section
      id="features"
      className="snap-section relative flex flex-col items-center justify-center px-6 py-24"
    >
      {/* Subtle glow */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-[200px] glow-accent opacity-20 pointer-events-none" />

      <div className="max-w-6xl w-full mx-auto">
        {/* Section header */}
        <div className="text-center mb-14">
          <div className="inline-flex items-center gap-2 text-xs text-[var(--text-tertiary)] mb-4 px-3 py-1 rounded-full border border-[var(--border-subtle)] bg-[var(--bg-card)]">
            <span className="w-1.5 h-1.5 rounded-full bg-[var(--accent)]" style={{ backgroundColor: 'var(--accent)' }} />
            核心功能
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-[var(--text-primary)] mb-4 tracking-tight">
            专业级设计，触手可及
          </h2>
          <p className="max-w-lg mx-auto text-[var(--text-secondary)] text-base leading-relaxed">
            从草稿到精稿，DesignAI 覆盖室内设计创作的每一个环节，让专业不再是门槛。
          </p>
        </div>

        {/* Cards grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {FEATURES.map((feature) => {
            const Icon = feature.icon;
            return (
              <div
                key={feature.title}
                className="group relative rounded-2xl p-6 border border-[var(--border-subtle)] bg-[var(--bg-card)] card-hover overflow-hidden cursor-default"
                style={{ boxShadow: 'var(--shadow-card)' }}
              >
                {/* Gradient bg */}
                <div className={`absolute inset-0 bg-gradient-to-br ${feature.gradient} opacity-0 group-hover:opacity-100 transition-opacity duration-300`} />

                {/* Accent border on hover */}
                <div className="absolute inset-0 rounded-2xl border border-[var(--accent-border)] opacity-0 group-hover:opacity-30 transition-opacity duration-300" />

                <div className="relative">
                  {/* Icon */}
                  <div className="w-10 h-10 rounded-xl bg-[var(--bg-input)] border border-[var(--border-subtle)] flex items-center justify-center mb-4 group-hover:border-[var(--accent-border)] group-hover:shadow-[0_0_12px_var(--accent-glow)] transition-all duration-300">
                    <Icon size={18} className="text-[var(--text-secondary)] group-hover:text-[var(--text-primary)] transition-colors" />
                  </div>

                  <h3 className="text-sm font-semibold text-[var(--text-primary)] mb-2">
                    {feature.title}
                  </h3>
                  <p className="text-sm text-[var(--text-secondary)] leading-relaxed">
                    {feature.desc}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
