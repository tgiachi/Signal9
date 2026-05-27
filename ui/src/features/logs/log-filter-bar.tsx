import { Search } from 'lucide-react';
import { cn } from '@/lib/cn';
import type { LogLevel } from './log-entry';

export type LevelFilter = LogLevel | 'all';
const LEVELS: LevelFilter[] = ['all', 'info', 'warn', 'error'];

type Props = {
  level: LevelFilter;
  search: string;
  onLevelChange: (next: LevelFilter) => void;
  onSearchChange: (next: string) => void;
};

export function LogFilterBar({ level, search, onLevelChange, onSearchChange }: Props) {
  return (
    <div className="flex flex-wrap items-center gap-2 bg-bg-4 px-3 py-2">
      <div className="flex gap-1" aria-label="Log level filter">
        {LEVELS.map((l) => (
          <button
            key={l}
            type="button"
            onClick={() => onLevelChange(l)}
            className={cn(
              'rounded-[4px] px-2 py-1 font-mono text-[10px] uppercase tracking-label transition-colors',
              l === level
                ? 'bg-accent-live text-bg-5 font-semibold'
                : 'bg-bg-2 text-fg-2 hover:text-fg-0',
            )}
          >
            {l}
          </button>
        ))}
      </div>
      <label className="relative min-w-[14rem] flex-1">
        <Search className="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-fg-3" />
        <input
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="search…"
          className="w-full rounded-[6px] bg-bg-1 py-1 pl-7 pr-2 font-mono text-[11px] text-fg-1 outline-none placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        />
      </label>
    </div>
  );
}
