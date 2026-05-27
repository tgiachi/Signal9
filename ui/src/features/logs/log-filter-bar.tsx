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
    <div className="flex flex-wrap items-center gap-2 border-b border-border-subtle bg-panel-strong px-3 py-2">
      <div className="flex gap-1" aria-label="Log level filter">
        {LEVELS.map((l) => (
          <button
            key={l}
            type="button"
            onClick={() => onLevelChange(l)}
            className={cn(
              'rounded-md border px-2 py-1 font-mono text-[10px] uppercase transition-colors',
              l === level
                ? 'border-on-air bg-on-air/15 text-on-air-2'
                : 'border-border bg-bg-2 text-fg-1 hover:text-fg-0',
            )}
          >
            {l}
          </button>
        ))}
      </div>
      <label className="relative min-w-[14rem] flex-1">
        <Search className="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-fg-2" />
        <input
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="search…"
          className="w-full rounded-md border border-border bg-bg-1 py-1 pl-7 pr-2 font-mono text-[11px] text-fg-0 outline-none placeholder:text-fg-2 focus:border-on-air"
        />
      </label>
    </div>
  );
}
