import { cn } from '@/lib/cn';
import { SCHEMA } from './toml-schema';

type Props = { activeKey: string; onChange: (key: string) => void };

export function ConfigSectionNav({ activeKey, onChange }: Props) {
  return (
    <nav className="flex w-44 shrink-0 flex-col gap-1 bg-bg-1 p-2 text-[11px]">
      {SCHEMA.map((s) => (
        <button
          key={s.key}
          type="button"
          onClick={() => onChange(s.key)}
          className={cn(
            'rounded-[6px] px-2 py-1.5 text-left transition-colors',
            s.key === activeKey
              ? 'bg-accent-live text-bg-5 font-semibold'
              : 'text-fg-3 hover:bg-bg-2 hover:text-fg-1',
          )}
        >
          {s.label}
        </button>
      ))}
    </nav>
  );
}
