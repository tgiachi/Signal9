import { useState } from 'react';
import { useParams } from 'react-router';
import { toast } from 'sonner';
import { ScheduleBlockGrid } from './schedule-block-grid';
import { ScheduleBlockModal } from './schedule-block-modal';
import { ScheduleEpgStrip } from './schedule-epg-strip';
import { useScheduleBlocks } from './use-schedule-blocks';
import { useRebuildSchedule } from './use-rebuild-schedule';
import type { ScheduleBlock, ScheduleBlockInput } from './schedule-types';

export function SchedulePage() {
  const { channelId = '' } = useParams<{ channelId: string }>();
  const { list, create, update, remove } = useScheduleBlocks(channelId);
  const rebuild = useRebuildSchedule(channelId);

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ScheduleBlock | null>(null);
  const [defaultDay, setDefaultDay] = useState<number>(1);
  const [defaultStartTime, setDefaultStartTime] = useState<string>('20:00:00');
  const [defaultDurationMinutes, setDefaultDurationMinutes] = useState<number>(60);

  const onSelectSlot = (info: { start: Date; end: Date }) => {
    setEditing(null);
    setDefaultDay(info.start.getDay());
    setDefaultStartTime(
      `${String(info.start.getHours()).padStart(2, '0')}:${String(info.start.getMinutes()).padStart(2, '0')}:00`,
    );
    setDefaultDurationMinutes(
      Math.max(1, Math.round((info.end.getTime() - info.start.getTime()) / 60_000)),
    );
    setModalOpen(true);
  };

  const onSelectEvent = (block: ScheduleBlock) => {
    setEditing(block);
    setModalOpen(true);
  };

  const onSubmit = async (input: ScheduleBlockInput) => {
    try {
      if (editing) {
        await update.mutateAsync({ id: editing.id, input });
        toast.success('Blocco aggiornato');
      } else {
        await create.mutateAsync(input);
        toast.success('Blocco creato');
      }
    } catch (err) {
      toast.error((err as Error).message);
    }
  };

  return (
    <div className="flex h-full flex-col gap-3 p-4">
      <div className="flex items-center gap-3">
        <h1 className="text-base font-semibold text-fg-0">
          Schedule · {channelId.slice(0, 8)}
        </h1>
        <div className="ml-auto flex gap-2">
          <button
            type="button"
            onClick={() => rebuild.mutate({ hoursAhead: 48 })}
            disabled={rebuild.isPending}
            className="rounded bg-accent-cfg px-3 py-1 text-[12px] text-fg-0"
          >
            Rebuild
          </button>
        </div>
      </div>

      <ScheduleEpgStrip channelId={channelId} />

      <ScheduleBlockGrid
        blocks={list.data ?? []}
        onSelectSlot={onSelectSlot}
        onSelectEvent={onSelectEvent}
      />

      {editing ? (
        <div className="flex justify-end">
          <button
            type="button"
            onClick={async () => {
              try {
                await remove.mutateAsync(editing.id);
                toast.success('Blocco eliminato');
                setEditing(null);
              } catch (err) {
                toast.error((err as Error).message);
              }
            }}
            className="rounded bg-bg-1 px-3 py-1 text-[12px] text-fg-2"
          >
            Elimina blocco selezionato
          </button>
        </div>
      ) : null}

      <ScheduleBlockModal
        open={modalOpen}
        initial={editing}
        defaultDay={defaultDay}
        defaultStartTime={defaultStartTime}
        defaultDurationMinutes={defaultDurationMinutes}
        onSubmit={onSubmit}
        onClose={() => setModalOpen(false)}
      />
    </div>
  );
}
