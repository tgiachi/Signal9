import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        'bg-0': 'var(--bg-0)',
        'bg-1': 'var(--bg-1)',
        'bg-2': 'var(--bg-2)',
        'bg-3': 'var(--bg-3)',
        border: 'var(--border)',
        'border-subtle': 'var(--border-subtle)',
        'fg-0': 'var(--fg-0)',
        'fg-1': 'var(--fg-1)',
        'fg-2': 'var(--fg-2)',
        'on-air': 'var(--on-air)',
        'on-air-2': 'var(--on-air-2)',
        warn: 'var(--warn)',
        error: 'var(--error)',
        'error-bg': 'var(--error-bg)',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      letterSpacing: { 'tight-num': '0.05em', label: '0.08em' },
    },
  },
} satisfies Config;
