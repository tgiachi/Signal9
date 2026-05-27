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
    <div className="grid grid-cols-[5.5rem_4rem_minmax(6rem,10rem)_minmax(0,1fr)] gap-3 border-b border-border-subtle/50 px-3 py-1 font-mono text-[12px] leading-[1.45] hover:bg-bg-2">
      <span data-testid="log-ts" className="shrink-0 text-fg-2">
        {formatTime(entry.ts)}
      </span>
      <span
        data-testid="log-lvl"
        className={cn('w-12 shrink-0 font-semibold', LEVEL_CLS[entry.level])}
      >
        {entry.level.toUpperCase()}
      </span>
      <span className="truncate text-[color:var(--syn-key)]">{entry.source}</span>
      <span className="min-w-0 break-words text-fg-0">{entry.message}</span>
    </div>
  );
}
