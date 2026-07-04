import { Download, Heart } from 'lucide-react';

const GALLERY_ITEMS = [
  { id: 1, label: '北欧极简客厅', style: '北欧', ratio: 'aspect-[4/3]', hue: '210 20% 15%' },
  { id: 2, label: '侘寂风茶室', style: '日式', ratio: 'aspect-square', hue: '30 15% 18%' },
  { id: 3, label: '工业风书房', style: '工业', ratio: 'aspect-[3/4]', hue: '220 10% 12%' },
  { id: 4, label: '法式轻奢卧室', style: '法式', ratio: 'aspect-[4/3]', hue: '340 15% 16%' },
  { id: 5, label: '现代简约厨房', style: '现代', ratio: 'aspect-square', hue: '180 12% 14%' },
  { id: 6, label: '复古美式餐厅', style: '美式', ratio: 'aspect-[3/4]', hue: '25 20% 16%' },
];

const PALETTE_MAP: Record<string, string[]> = {
  '210 20% 15%': ['#1a2030', '#2a3550', '#3d5070', '#6080a0'],
  '30 15% 18%':  ['#2d2518', '#4a3d28', '#6b5a3e', '#9a8060'],
  '220 10% 12%': ['#181c24', '#2a2f3d', '#404858', '#606878'],
  '340 15% 16%': ['#28151e', '#42253a', '#5e3555', '#8a5070'],
  '180 12% 14%': ['#121e1e', '#1e3232', '#2d4a4a', '#4a7070'],
  '25 20% 16%':  ['#261810', '#402810', '#5e3d18', '#8a6030'],
};

export function GallerySection() {
  return (
    <section
      id="gallery"
      className="snap-section relative flex flex-col items-center justify-center px-6 py-24 overflow-hidden"
    >
      {/* Background glow */}
      <div className="absolute bottom-0 right-0 w-[400px] h-[300px] glow-accent opacity-15 pointer-events-none" />

      <div className="max-w-6xl w-full mx-auto">
        {/* Section header */}
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 text-xs text-[var(--text-tertiary)] mb-4 px-3 py-1 rounded-full border border-[var(--border-subtle)] bg-[var(--bg-card)]">
            <span className="w-1.5 h-1.5 rounded-full bg-[var(--accent)]" style={{ backgroundColor: 'var(--accent)' }} />
            AI 生成案例
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-[var(--text-primary)] mb-4 tracking-tight">
            每一帧都是杰作
          </h2>
          <p className="max-w-lg mx-auto text-[var(--text-secondary)] text-base leading-relaxed">
            以下所有图片均由 DesignAI 在 3 秒内生成，无任何后期修饰。
          </p>
        </div>

        {/* 3×2 Grid */}
        <div className="grid grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4">
          {GALLERY_ITEMS.map((item) => {
            const palette = PALETTE_MAP[item.hue] ?? ['#1a1a2e', '#16213e', '#0f3460', '#533483'];
            return (
              <div
                key={item.id}
                className={`group relative ${item.ratio} rounded-2xl overflow-hidden border border-[var(--border-subtle)] cursor-pointer`}
                style={{ boxShadow: 'var(--shadow-card)' }}
              >
                {/* Color placeholder simulating a designed room */}
                <div className="absolute inset-0" style={{ background: `hsl(${item.hue})` }}>
                  {/* Simulated room elements */}
                  <div className="absolute inset-0 flex flex-col justify-between p-3 sm:p-4">
                    {/* Ceiling light simulation */}
                    <div className="flex justify-center">
                      <div className="w-16 h-0.5 rounded-full opacity-20" style={{ background: palette[3] }} />
                    </div>
                    {/* Floor/furniture shapes */}
                    <div className="flex flex-col gap-2">
                      <div className="h-8 rounded-lg opacity-25" style={{ background: palette[2] }} />
                      <div className="flex gap-2">
                        <div className="flex-1 h-12 rounded-lg opacity-20" style={{ background: palette[1] }} />
                        <div className="w-1/3 h-12 rounded-lg opacity-15" style={{ background: palette[3] }} />
                      </div>
                    </div>
                  </div>
                  {/* Gradient overlay */}
                  <div className="absolute inset-0 bg-gradient-to-br from-transparent via-transparent to-black/30" />
                  {/* Subtle grid */}
                  <div
                    className="absolute inset-0 opacity-[0.04]"
                    style={{
                      backgroundImage: `linear-gradient(${palette[3]} 1px, transparent 1px), linear-gradient(90deg, ${palette[3]} 1px, transparent 1px)`,
                      backgroundSize: '20px 20px',
                    }}
                  />
                </div>

                {/* Hover overlay */}
                <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex flex-col justify-between p-4">
                  <div className="flex justify-end gap-2">
                    <button className="w-8 h-8 rounded-lg bg-white/10 backdrop-blur-sm border border-white/20 flex items-center justify-center text-white hover:bg-white/20 transition-colors">
                      <Heart size={13} />
                    </button>
                    <button className="w-8 h-8 rounded-lg bg-white/10 backdrop-blur-sm border border-white/20 flex items-center justify-center text-white hover:bg-white/20 transition-colors">
                      <Download size={13} />
                    </button>
                  </div>
                  <div>
                    <span className="text-xs text-white/60 mb-0.5 block">{item.style}风格</span>
                    <p className="text-sm font-medium text-white">{item.label}</p>
                  </div>
                </div>

                {/* Scale on hover */}
                <div className="absolute inset-0 scale-100 group-hover:scale-105 transition-transform duration-500 -z-10" />
              </div>
            );
          })}
        </div>

        {/* CTA */}
        <div className="text-center mt-10">
          <button className="text-sm text-[var(--text-secondary)] hover:text-[var(--text-primary)] transition-colors border border-[var(--border-default)] hover:border-[var(--border-strong)] px-6 py-2.5 rounded-xl bg-[var(--bg-card)] hover:bg-[var(--bg-card-hover)]">
            查看更多案例 →
          </button>
        </div>
      </div>
    </section>
  );
}
