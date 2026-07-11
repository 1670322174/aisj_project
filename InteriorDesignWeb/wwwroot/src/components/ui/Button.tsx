import React from 'react';
import { cn } from '@/utils/cn';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'ghost' | 'outline' | 'glass';
  size?: 'sm' | 'md' | 'lg';
  children: React.ReactNode;
}

export function Button({
  variant = 'primary',
  size = 'md',
  className,
  children,
  ...props
}: ButtonProps) {
  const base =
    'inline-flex items-center justify-center gap-2 font-semibold rounded-xl transition-all duration-200 cursor-pointer select-none disabled:opacity-40 disabled:pointer-events-none';

  const sizes = {
    sm: 'h-9 px-3 text-[13px]',
    md: 'h-10 px-4 text-sm',
    lg: 'h-11 px-6 text-sm',
  };

  const variants = {
    primary: [
      'relative overflow-hidden',
      'bg-[var(--accent)] text-white',
      'border border-[var(--accent-border)]',
      'shadow-[0_2px_8px_var(--accent-glow)]',
      'hover:brightness-110 hover:shadow-[0_4px_16px_var(--accent-glow)]',
      'active:scale-[0.98]',
    ].join(' '),

    ghost: [
      'text-[var(--text-secondary)]',
      'hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)]',
      'active:scale-[0.98]',
    ].join(' '),

    outline: [
      'border border-[var(--border-default)]',
      'text-[var(--text-secondary)]',
      'hover:border-[var(--border-strong)] hover:text-[var(--text-primary)]',
      'hover:bg-[var(--bg-card)]',
      'active:scale-[0.98]',
    ].join(' '),

    glass: [
      'glass',
      'text-[var(--text-primary)]',
      'hover:bg-[var(--bg-card-hover)] hover:border-[var(--border-strong)]',
      'active:scale-[0.98]',
    ].join(' '),
  };

  return (
    <button className={cn(base, sizes[size], variants[variant], className)} {...props}>
      {children}
    </button>
  );
}
