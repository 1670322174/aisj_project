import { Sparkles, Mail, MapPin, MessageCircle } from 'lucide-react';

const FOOTER_LINKS = {
  产品: ['AI 生成', '图库', '方案管理', '定价方案', '更新日志'],
  资源: ['帮助中心', '设计指南', 'API 文档', '开发者', '状态页'],
  公司: ['关于我们', '博客', '招聘', '媒体资源', '联系我们'],
};

export function FooterSection() {
  return (
    <footer
      id="about"
      className="snap-section relative flex flex-col justify-end px-6 pb-0 overflow-hidden"
    >
      <div className="absolute inset-0 glow-accent opacity-10 pointer-events-none" />

      <div className="max-w-6xl w-full mx-auto">
        {/* Top divider */}
        <div className="w-full h-px bg-gradient-to-r from-transparent via-[var(--border-default)] to-transparent mb-12" />

        {/* Main footer content */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10 mb-12">
          {/* Brand column */}
          <div className="md:col-span-1">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-7 h-7 rounded-lg bg-[var(--accent)] flex items-center justify-center shadow-[0_0_10px_var(--accent-glow)]">
                <Sparkles size={14} className="text-white" />
              </div>
              <span className="text-sm font-semibold text-[var(--text-primary)]">DesignAI</span>
            </div>
            <p className="text-sm text-[var(--text-secondary)] leading-relaxed mb-5">
              用人工智能重新定义室内设计体验，让每一个人都能拥有专业级的设计方案。
            </p>
            {/* Contact */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-2 text-xs text-[var(--text-tertiary)]">
                <Mail size={12} />
                <span>hello@designai.com</span>
              </div>
              <div className="flex items-center gap-2 text-xs text-[var(--text-tertiary)]">
                <MapPin size={12} />
                <span>上海市 · 中国</span>
              </div>
            </div>
            {/* Social */}
            <div className="flex items-center gap-2 mt-4">
              {[MessageCircle, MessageCircle].map((Icon, i) => (
                <button
                  key={i}
                  className="w-8 h-8 rounded-lg border border-[var(--border-subtle)] flex items-center justify-center text-[var(--text-tertiary)] hover:text-[var(--text-primary)] hover:border-[var(--border-default)] hover:bg-[var(--bg-card)] transition-all"
                >
                  <Icon size={13} />
                </button>
              ))}
            </div>
          </div>

          {/* Link columns */}
          {Object.entries(FOOTER_LINKS).map(([category, links]) => (
            <div key={category}>
              <h4 className="text-xs font-semibold text-[var(--text-primary)] mb-4 tracking-wider uppercase">
                {category}
              </h4>
              <ul className="flex flex-col gap-2.5">
                {links.map((link) => (
                  <li key={link}>
                    <a
                      href="#"
                      className="text-sm text-[var(--text-secondary)] hover:text-[var(--text-primary)] transition-colors"
                      onClick={(e) => e.preventDefault()}
                    >
                      {link}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        {/* Bottom bar */}
        <div className="border-t border-[var(--border-subtle)] py-5 flex flex-col sm:flex-row items-center justify-between gap-3">
          <p className="text-xs text-[var(--text-tertiary)]">
            © 2025 DesignAI. All rights reserved.
          </p>
          <div className="flex items-center gap-4">
            {['隐私政策', '服务条款', 'Cookie 设置'].map((item) => (
              <a
                key={item}
                href="#"
                className="text-xs text-[var(--text-tertiary)] hover:text-[var(--text-secondary)] transition-colors"
                onClick={(e) => e.preventDefault()}
              >
                {item}
              </a>
            ))}
          </div>
        </div>
      </div>
    </footer>
  );
}
