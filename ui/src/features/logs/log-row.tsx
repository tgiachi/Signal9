import type { LogEntry } from './log-entry';
import { cn } from '@/lib/cn';

const LEVEL_CLS: Record<LogEntry['level'], string> = {
  debug: 'text-fg-2',
  info: 'text-on-air-2',
  warn: 'text-warn',
  error: 'text-error',
};

function formatTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString('en-GB', { hour12: false });
}

export function LogRow({ entry }: { entry: LogEntry }) {
  return (
    <div className="flex gap-3 px-3 py-0.5 font-mono text-[12px] leading-[1.6] hover:bg-bg-2">
      <span data-testid="log-ts" className="shrink-0 text-fg-2">
        {formatTime(entry.ts)}
      </span>
      <span
        data-testid="log-lvl"
        className={cn('w-12 shrink-0 font-semibold', LEVEL_CLS[entry.level])}
      >
        {entry.level.toUpperCase()}
      </span>
      <span className="shrink-0 text-[color:var(--syn-key)]">{entry.source}</span>
      <span className="text-fg-0">{entry.message}</span>
    </div>
  );
}
