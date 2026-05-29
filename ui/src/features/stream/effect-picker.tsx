import { useState } from 'react';
import { Plus } from 'lucide-react';
import { useEffectCatalog } from './use-effect-catalog';
import type { ChannelEffect } from './stream-types';

type Props = {
  active: ChannelEffect[];
  onAdd: (effect: ChannelEffect) => void;
};

export function EffectPicker({ active, onAdd }: Props) {
  const { data: catalog = [] } = useEffectCatalog();
  const [open, setOpen] = useState(false);

  const remaining = catalog.filter((c) => !active.some((a) => a.kind === c.kind));

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="inline-flex items-center gap-1.5 rounded bg-bg-1 px-3 py-1.5 text-[12px] text-fg-1 hover:bg-bg-3"
        disabled={remaining.length === 0}
      >
        <Plus className="size-3.5" />
        Aggiungi effetto
      </button>
      {open && remaining.length > 0 && (
        <div className="absolute z-10 mt-1 max-h-60 w-60 overflow-y-auto rounded bg-bg-1 shadow-lg">
          {remaining.map((c) => (
            <button
              key={c.kind}
              type="button"
              onClick={() => {
                const params: Record<string, number> = {};
                for (const p of c.parameters) params[p.name] = p.default;
                onAdd({ kind: c.kind, enabled: true, params });
                setOpen(false);
              }}
              className="block w-full px-3 py-2 text-left text-[12px] text-fg-1 hover:bg-bg-3"
            >
              {c.label}
              <div className="truncate text-[10px] text-fg-3">{c.description}</div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
