import { useState, useEffect } from 'react';
import {
  DndContext,
  closestCenter,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  useSortable,
  arrayMove,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, X } from 'lucide-react';
import { toast } from 'sonner';
import { useEffectCatalog } from './use-effect-catalog';
import { useChannelEffectsMutation } from './use-channel-effects';
import { EffectPicker } from './effect-picker';
import type { ChannelEffect, EffectCatalogItem } from './stream-types';

type Props = {
  channelId: string;
  initialEffects: ChannelEffect[];
};

export function EffectChainEditor({ channelId, initialEffects }: Props) {
  const [effects, setEffects] = useState<ChannelEffect[]>(initialEffects);
  useEffect(() => setEffects(initialEffects), [initialEffects]);

  const { data: catalog = [] } = useEffectCatalog();
  const byKind = new Map<string, EffectCatalogItem>(catalog.map((c) => [c.kind, c]));
  const mutation = useChannelEffectsMutation(channelId);

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));

  const onDragEnd = (e: DragEndEvent) => {
    if (!e.over || e.active.id === e.over.id) return;
    const from = effects.findIndex((x) => x.kind === e.active.id);
    const to = effects.findIndex((x) => x.kind === e.over!.id);
    if (from < 0 || to < 0) return;
    setEffects(arrayMove(effects, from, to));
  };

  const updateOne = (kind: string, patch: Partial<ChannelEffect>) => {
    setEffects((cur) => cur.map((e) => (e.kind === kind ? { ...e, ...patch } : e)));
  };

  const setParam = (kind: string, name: string, value: number) => {
    setEffects((cur) =>
      cur.map((e) => (e.kind === kind ? { ...e, params: { ...e.params, [name]: value } } : e))
    );
  };

  const remove = (kind: string) => setEffects((cur) => cur.filter((e) => e.kind !== kind));

  const save = async () => {
    try {
      await mutation.mutateAsync(effects);
      toast.success('Effetti salvati');
    } catch (err) {
      toast.error((err as Error).message);
    }
  };

  return (
    <div className="rounded-md bg-bg-2 p-3">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-[12px] font-semibold uppercase tracking-wide text-fg-2">
          Effect chain
        </h3>
        <div className="flex gap-2">
          <EffectPicker
            active={effects}
            onAdd={(eff) => setEffects((cur) => [...cur, eff])}
          />
          <button
            type="button"
            onClick={save}
            disabled={mutation.isPending}
            className="rounded bg-accent-live px-3 py-1.5 text-[12px] font-medium text-bg-5 hover:bg-accent-live-hover"
          >
            Salva
          </button>
        </div>
      </div>

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={effects.map((e) => e.kind)} strategy={verticalListSortingStrategy}>
          <div className="space-y-1.5">
            {effects.map((e) => {
              const desc = byKind.get(e.kind);
              return (
                <EffectRow
                  key={e.kind}
                  effect={e}
                  catalog={desc}
                  onToggle={(enabled) => updateOne(e.kind, { enabled })}
                  onParam={(name, val) => setParam(e.kind, name, val)}
                  onRemove={() => remove(e.kind)}
                />
              );
            })}
            {effects.length === 0 && (
              <div className="py-6 text-center text-[12px] text-fg-3">
                Nessun effetto. Aggiungine uno con "+ Aggiungi effetto".
              </div>
            )}
          </div>
        </SortableContext>
      </DndContext>
    </div>
  );
}

function EffectRow({
  effect,
  catalog,
  onToggle,
  onParam,
  onRemove,
}: {
  effect: ChannelEffect;
  catalog: EffectCatalogItem | undefined;
  onToggle: (enabled: boolean) => void;
  onParam: (name: string, value: number) => void;
  onRemove: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: effect.kind,
  });
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  };

  return (
    <div ref={setNodeRef} style={style} className="rounded bg-bg-1 p-2">
      <div className="flex items-center gap-2">
        <button
          type="button"
          {...attributes}
          {...listeners}
          className="cursor-grab text-fg-3 hover:text-fg-1"
        >
          <GripVertical className="size-4" />
        </button>
        <input
          type="checkbox"
          checked={effect.enabled}
          onChange={(e) => onToggle(e.target.checked)}
        />
        <span className="flex-1 text-[12px] font-medium text-fg-1">
          {catalog?.label ?? effect.kind}
        </span>
        <button
          type="button"
          onClick={onRemove}
          className="rounded p-1 text-fg-3 hover:bg-bg-3 hover:text-fg-1"
          aria-label="Rimuovi"
        >
          <X className="size-3.5" />
        </button>
      </div>
      {(catalog?.parameters ?? []).length > 0 && (
        <div className="mt-2 space-y-1.5 pl-7">
          {(catalog?.parameters ?? []).map((p) => {
            const val = effect.params[p.name] ?? p.default;
            return (
              <label key={p.name} className="flex items-center gap-2 text-[11px] text-fg-2">
                <span className="w-20 truncate">{p.label}</span>
                <input
                  type="range"
                  min={p.min ?? 0}
                  max={p.max ?? 1}
                  step={p.step ?? 0.05}
                  value={val}
                  onChange={(e) => onParam(p.name, Number(e.target.value))}
                  className="flex-1"
                />
                <span className="w-12 text-right font-mono text-fg-3">{val.toFixed(2)}</span>
              </label>
            );
          })}
        </div>
      )}
    </div>
  );
}
