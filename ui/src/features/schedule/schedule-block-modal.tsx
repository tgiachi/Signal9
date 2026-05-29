import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogBody,
  DialogFooter,
  DialogCloseIcon,
} from '@/components/ui/dialog';
import type { ScheduleBlock, ScheduleBlockInput, ScheduleBlockRuleType } from './schedule-types';

const DAY_OPTIONS = [
  { value: 1, label: 'Lunedì' },
  { value: 2, label: 'Martedì' },
  { value: 3, label: 'Mercoledì' },
  { value: 4, label: 'Giovedì' },
  { value: 5, label: 'Venerdì' },
  { value: 6, label: 'Sabato' },
  { value: 0, label: 'Domenica' },
];

type Props = {
  open: boolean;
  initial?: ScheduleBlock | null;
  defaultDay?: number;
  defaultStartTime?: string;
  defaultDurationMinutes?: number;
  onSubmit: (input: ScheduleBlockInput) => void | Promise<void>;
  onClose: () => void;
};

export function ScheduleBlockModal({
  open,
  initial,
  defaultDay = 1,
  defaultStartTime = '20:00:00',
  defaultDurationMinutes = 60,
  onSubmit,
  onClose,
}: Props) {
  const [name, setName] = useState('');
  const [dayOfWeek, setDayOfWeek] = useState<number>(defaultDay);
  const [startTime, setStartTime] = useState<string>(defaultStartTime);
  const [durationMinutes, setDurationMinutes] = useState<number>(defaultDurationMinutes);
  const [ruleType, setRuleType] = useState<ScheduleBlockRuleType>('TagPool');
  const [pinnedChannelMediaId, setPinned] = useState<string>('');
  const [seriesName, setSeriesName] = useState<string>('');
  const [tagFilterCsv, setTagFilterCsv] = useState<string>('');
  const [typeFilterCsv, setTypeFilterCsv] = useState<string>('Movies');
  const [isActive, setIsActive] = useState<boolean>(true);

  useEffect(() => {
    if (initial) {
      setName(initial.name);
      setDayOfWeek(initial.dayOfWeek);
      setStartTime(initial.startTime);
      setDurationMinutes(initial.durationMinutes);
      setRuleType(initial.ruleType);
      setPinned(initial.pinnedChannelMediaId ?? '');
      setSeriesName(initial.seriesName ?? '');
      setTagFilterCsv(initial.tagFilterCsv ?? '');
      setTypeFilterCsv(initial.typeFilterCsv ?? '');
      setIsActive(initial.isActive);
    } else {
      setName('');
      setDayOfWeek(defaultDay);
      setStartTime(defaultStartTime);
      setDurationMinutes(defaultDurationMinutes);
      setRuleType('TagPool');
      setPinned('');
      setSeriesName('');
      setTagFilterCsv('');
      setTypeFilterCsv('Movies');
      setIsActive(true);
    }
  }, [initial, defaultDay, defaultStartTime, defaultDurationMinutes]);

  const submit = async () => {
    await onSubmit({
      name,
      dayOfWeek,
      startTime,
      durationMinutes,
      ruleType,
      pinnedChannelMediaId: ruleType === 'Pin' ? pinnedChannelMediaId || null : null,
      seriesName: ruleType === 'Series' ? seriesName || null : null,
      tagFilterCsv: ruleType === 'TagPool' ? tagFilterCsv || null : null,
      typeFilterCsv: ruleType === 'TagPool' ? typeFilterCsv || null : null,
      isActive,
    });
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{initial ? 'Modifica blocco' : 'Nuovo blocco'}</DialogTitle>
          <DialogCloseIcon />
        </DialogHeader>

        <DialogBody>
          <label className="block text-[12px] text-fg-2">
            Nome
            <input
              className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>

          <div className="flex gap-3">
            <label className="flex-1 text-[12px] text-fg-2">
              Giorno
              <select
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                value={dayOfWeek}
                onChange={(e) => setDayOfWeek(Number(e.target.value))}
              >
                {DAY_OPTIONS.map((d) => (
                  <option key={d.value} value={d.value}>
                    {d.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex-1 text-[12px] text-fg-2">
              Inizio
              <input
                type="time"
                step={60}
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                value={startTime.slice(0, 5)}
                onChange={(e) => setStartTime(`${e.target.value}:00`)}
              />
            </label>
            <label className="flex-1 text-[12px] text-fg-2">
              Durata (min)
              <input
                type="number"
                min={1}
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                value={durationMinutes}
                onChange={(e) => setDurationMinutes(Number(e.target.value))}
              />
            </label>
          </div>

          <div className="flex gap-2">
            {(['Pin', 'Series', 'TagPool'] as ScheduleBlockRuleType[]).map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setRuleType(r)}
                className={
                  'rounded px-3 py-1 text-[12px] ' +
                  (r === ruleType ? 'bg-accent-jobs text-fg-0' : 'bg-bg-1 text-fg-2')
                }
              >
                {r}
              </button>
            ))}
          </div>

          {ruleType === 'Pin' && (
            <label className="block text-[12px] text-fg-2">
              Channel Media Id (Pin)
              <input
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                value={pinnedChannelMediaId}
                onChange={(e) => setPinned(e.target.value)}
              />
            </label>
          )}
          {ruleType === 'Series' && (
            <label className="block text-[12px] text-fg-2">
              Series Name
              <input
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                value={seriesName}
                onChange={(e) => setSeriesName(e.target.value)}
              />
            </label>
          )}
          {ruleType === 'TagPool' && (
            <div className="space-y-2">
              <label className="block text-[12px] text-fg-2">
                Tag filter (csv)
                <input
                  className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                  value={tagFilterCsv}
                  onChange={(e) => setTagFilterCsv(e.target.value)}
                />
              </label>
              <label className="block text-[12px] text-fg-2">
                Type filter (csv: Movies,TvShow)
                <input
                  className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
                  value={typeFilterCsv}
                  onChange={(e) => setTypeFilterCsv(e.target.value)}
                />
              </label>
            </div>
          )}

          <label className="inline-flex items-center gap-2 text-[12px] text-fg-2">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
            />
            Attivo
          </label>
        </DialogBody>

        <DialogFooter>
          <button
            type="button"
            onClick={onClose}
            className="rounded bg-bg-1 px-3 py-1 text-[12px] text-fg-2"
          >
            Annulla
          </button>
          <button
            type="button"
            onClick={submit}
            className="rounded bg-accent-live px-3 py-1 text-[12px] text-bg-5"
          >
            Salva
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
