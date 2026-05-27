import type { LogEntry } from './log-entry';
import { cn } from '@/lib/cn';

const LEVEL_CLS: Record<LogEntry['level'], string> = {
  debug: 'text-fg-3',
  info: 'text-accent-live',
  warn: 'text-accent-warn',
  error: 'text-accent-err',
};

function formatTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString('en-GB', { hour12: false });
}

export function LogRow({ entry }: { entry: LogEntry }) {
  return (
    <div className="grid grid-cols-[5.5rem_4rem_minmax(6rem,10rem)_minmax(0,1fr)] gap-3 px-3 py-1 font-mono text-[12px] leading-[1.45] hover:bg-[#343b41]">
      <span data-testid="log-ts" className="shrink-0 text-fg-3">
        {formatTime(entry.ts)}
      </span>
      <span
        data-testid="log-lvl"
        className={cn('w-12 shrink-0 font-semibold', LEVEL_CLS[entry.level])}
      >
        {entry.level.toUpperCase()}
      </span>
      <span className="truncate text-accent-jobs">{entry.source}</span>
      <span className="min-w-0 break-words text-fg-0">{entry.message}</span>
    </div>
  );
}
