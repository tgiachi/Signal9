import { cn } from '@/lib/cn';
import { SCHEMA } from './toml-schema';

type Props = { activeKey: string; onChange: (key: string) => void };

export function ConfigSectionNav({ activeKey, onChange }: Props) {
  return (
    <nav className="flex w-44 shrink-0 flex-col gap-1 border-r border-border-subtle bg-bg-0 p-2 text-[11px]">
      {SCHEMA.map((s) => (
        <button
          key={s.key}
          type="button"
          onClick={() => onChange(s.key)}
          className={cn(
            'rounded px-2 py-1 text-left transition-colors',
            s.key === activeKey ? 'bg-bg-3 text-on-air-2' : 'text-fg-1 hover:text-fg-0',
          )}
        >
          {s.label}
        </button>
      ))}
    </nav>
  );
}
