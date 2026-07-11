import React from 'react';
import { cn } from '@/utils/cn';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: React.ReactNode;
}

export function Input({ label, error, icon, className, ...props }: InputProps) {
  return (
    <div className="flex flex-col gap-1.5">
      {label && (
        <label className="text-[13px] font-medium text-[var(--text-secondary)] tracking-wide">
          {label}
        </label>
      )}
      <div className="relative">
        {icon && (
          <div className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-tertiary)]">
            {icon}
          </div>
        )}
        <input
          className={cn(
            'w-full h-10 rounded-xl text-sm transition-all duration-200',
            'bg-[var(--bg-input)] border border-[var(--border-default)]',
            'text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)]',
            'focus:outline-none focus:border-[var(--accent-border)] focus:ring-1 focus:ring-[var(--accent-glow)]',
            icon ? 'pl-9 pr-4' : 'px-4',
            error && 'border-red-500/50 focus:border-red-500',
            className
          )}
          {...props}
        />
      </div>
      {error && <p className="text-xs text-red-400">{error}</p>}
    </div>
  );
}
