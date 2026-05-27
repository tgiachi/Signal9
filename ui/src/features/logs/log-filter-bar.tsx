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
    <div className="flex items-center gap-2 border-b border-border-subtle bg-bg-1 px-3 py-1.5">
      <div className="flex gap-1">
        {LEVELS.map((l) => (
          <button
            key={l}
            type="button"
            onClick={() => onLevelChange(l)}
            className={cn(
              'rounded-full border px-2 py-0.5 font-mono text-[10px] uppercase transition-colors',
              l === level
                ? 'border-on-air bg-on-air text-black'
                : 'border-border bg-bg-2 text-fg-1 hover:text-fg-0',
            )}
          >
            {l}
          </button>
        ))}
      </div>
      <input
        value={search}
        onChange={(e) => onSearchChange(e.target.value)}
        placeholder="search…"
        className="ml-3 flex-1 rounded border border-border bg-bg-2 px-2 py-0.5 font-mono text-[11px] text-fg-0 outline-none placeholder:text-fg-2 focus:border-on-air"
      />
    </div>
  );
}
