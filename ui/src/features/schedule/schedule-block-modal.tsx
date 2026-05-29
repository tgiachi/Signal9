import { useEffect, useMemo, useState } from 'react';
import { Search, X } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogBody,
  DialogFooter,
  DialogCloseIcon,
} from '@/components/ui/dialog';
import { useChannelMedia } from '@/features/media/use-channel-media';
import { mediaTypeLabel } from '@/features/media/channel-media-types';
import { useTags } from './use-tags';
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

const TYPE_OPTIONS = [
  { value: 'Movies', label: 'Movies' },
  { value: 'TvShow', label: 'TV show' },
  { value: 'Bumper', label: 'Bumper' },
  { value: 'Commercial', label: 'Commercial' },
  { value: 'Information', label: 'Information' },
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

function parseCsv(csv: string | null | undefined): string[] {
  if (!csv) return [];
  return csv.split(',').map((s) => s.trim()).filter(Boolean);
}

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
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [selectedTypes, setSelectedTypes] = useState<string[]>(['Movies']);
  const [isActive, setIsActive] = useState<boolean>(true);

  const [tagInput, setTagInput] = useState('');
  const [pinSearch, setPinSearch] = useState('');

  const tagsQuery = useTags();
  const { media: mediaList, isLoading: mediaLoading } = useChannelMedia();

  useEffect(() => {
    if (initial) {
      setName(initial.name);
      setDayOfWeek(initial.dayOfWeek);
      setStartTime(initial.startTime);
      setDurationMinutes(initial.durationMinutes);
      setRuleType(initial.ruleType);
      setPinned(initial.pinnedChannelMediaId ?? '');
      setSeriesName(initial.seriesName ?? '');
      setSelectedTags(parseCsv(initial.tagFilterCsv));
      const initialTypes = parseCsv(initial.typeFilterCsv);
      setSelectedTypes(initialTypes.length > 0 ? initialTypes : ['Movies']);
      setIsActive(initial.isActive);
    } else {
      setName('');
      setDayOfWeek(defaultDay);
      setStartTime(defaultStartTime);
      setDurationMinutes(defaultDurationMinutes);
      setRuleType('TagPool');
      setPinned('');
      setSeriesName('');
      setSelectedTags([]);
      setSelectedTypes(['Movies']);
      setIsActive(true);
    }
    setTagInput('');
    setPinSearch('');
  }, [initial, defaultDay, defaultStartTime, defaultDurationMinutes]);

  const tagSuggestions = useMemo(() => {
    const all = tagsQuery.data ?? [];
    const lower = tagInput.trim().toLowerCase();
    const filtered = lower
      ? all.filter((t) => t.name.toLowerCase().includes(lower) && !selectedTags.includes(t.name))
      : all.filter((t) => !selectedTags.includes(t.name)).slice(0, 10);
    return filtered.slice(0, 10);
  }, [tagsQuery.data, tagInput, selectedTags]);

  const seriesSuggestions = useMemo(() => {
    const all = mediaList ?? [];
    const lower = seriesName.trim().toLowerCase();
    const set = new Set<string>();
    for (const m of all) {
      if (m.tvSeriesName && (!lower || m.tvSeriesName.toLowerCase().includes(lower))) {
        set.add(m.tvSeriesName);
      }
    }
    return [...set].sort().slice(0, 10);
  }, [mediaList, seriesName]);

  const pinSuggestions = useMemo(() => {
    const all = mediaList ?? [];
    const lower = pinSearch.trim().toLowerCase();
    if (!lower) return all.slice(0, 12);
    return all
      .filter((m) =>
        m.title.toLowerCase().includes(lower) ||
        (m.tvSeriesName ?? '').toLowerCase().includes(lower)
      )
      .slice(0, 20);
  }, [mediaList, pinSearch]);

  const pinnedMedia = useMemo(() => {
    if (!pinnedChannelMediaId) return null;
    return (mediaList ?? []).find((m) => m.id === pinnedChannelMediaId) ?? null;
  }, [mediaList, pinnedChannelMediaId]);

  const addTag = (name: string) => {
    const trimmed = name.trim();
    if (!trimmed) return;
    if (selectedTags.some((t) => t.toLowerCase() === trimmed.toLowerCase())) return;
    setSelectedTags([...selectedTags, trimmed]);
    setTagInput('');
  };
  const removeTag = (name: string) => {
    setSelectedTags(selectedTags.filter((t) => t !== name));
  };
  const toggleType = (value: string) => {
    setSelectedTypes((current) =>
      current.includes(value) ? current.filter((t) => t !== value) : [...current, value]
    );
  };

  const submit = async () => {
    await onSubmit({
      name,
      dayOfWeek,
      startTime,
      durationMinutes,
      ruleType,
      pinnedChannelMediaId: ruleType === 'Pin' ? pinnedChannelMediaId || null : null,
      seriesName: ruleType === 'Series' ? seriesName.trim() || null : null,
      tagFilterCsv: ruleType === 'TagPool' && selectedTags.length > 0 ? selectedTags.join(',') : null,
      typeFilterCsv: ruleType === 'TagPool' && selectedTypes.length > 0 ? selectedTypes.join(',') : null,
      isActive,
    });
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{initial ? 'Modifica blocco' : 'Nuovo blocco'}</DialogTitle>
          <DialogCloseIcon />
        </DialogHeader>

        <DialogBody className="space-y-3">
          <label className="block text-[12px] text-fg-2">
            Nome
            <input
              className="mt-1 w-full rounded bg-bg-1 px-2 py-1.5 text-fg-0"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="es. Prime time movies"
            />
          </label>

          <div className="flex gap-3">
            <label className="flex-1 text-[12px] text-fg-2">
              Giorno
              <select
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1.5 text-fg-0"
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
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1.5 text-fg-0"
                value={startTime.slice(0, 5)}
                onChange={(e) => setStartTime(`${e.target.value}:00`)}
              />
            </label>
            <label className="flex-1 text-[12px] text-fg-2">
              Durata (min)
              <input
                type="number"
                min={1}
                className="mt-1 w-full rounded bg-bg-1 px-2 py-1.5 text-fg-0"
                value={durationMinutes}
                onChange={(e) => setDurationMinutes(Number(e.target.value))}
              />
            </label>
          </div>

          <div className="flex gap-2 border-t border-bg-1 pt-3">
            {(['Pin', 'Series', 'TagPool'] as ScheduleBlockRuleType[]).map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setRuleType(r)}
                className={
                  'rounded px-3 py-1.5 text-[11px] font-semibold uppercase tracking-wide ' +
                  (r === ruleType ? 'bg-accent-jobs text-fg-0' : 'bg-bg-1 text-fg-2 hover:bg-bg-3')
                }
              >
                {r}
              </button>
            ))}
          </div>

          {ruleType === 'Pin' && (
            <div className="space-y-2">
              <label className="block text-[12px] text-fg-2">Pin un media specifico</label>
              {pinnedMedia ? (
                <div className="flex items-center justify-between gap-2 rounded bg-bg-1 px-3 py-2">
                  <div className="min-w-0">
                    <div className="truncate text-[13px] font-medium text-fg-0">
                      {pinnedMedia.title || '(senza titolo)'}
                    </div>
                    <div className="truncate text-[10px] text-fg-3">
                      {mediaTypeLabel(pinnedMedia.type)}
                      {pinnedMedia.tvSeriesName ? ` · ${pinnedMedia.tvSeriesName}` : ''}
                      {pinnedMedia.tvSeason != null && pinnedMedia.tvEpisode != null
                        ? ` S${pinnedMedia.tvSeason}E${pinnedMedia.tvEpisode}`
                        : ''}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setPinned('')}
                    className="rounded bg-bg-3 p-1 text-fg-3 hover:bg-bg-4 hover:text-fg-1"
                    title="Rimuovi"
                  >
                    <X className="size-3" />
                  </button>
                </div>
              ) : (
                <>
                  <div className="relative">
                    <Search className="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-fg-3" />
                    <input
                      className="w-full rounded bg-bg-1 py-1.5 pl-7 pr-2 text-fg-0 placeholder:text-fg-3"
                      value={pinSearch}
                      onChange={(e) => setPinSearch(e.target.value)}
                      placeholder="Cerca per titolo o serie…"
                    />
                  </div>
                  <div className="max-h-44 overflow-y-auto rounded bg-bg-1">
                    {mediaLoading ? (
                      <div className="p-2 text-[11px] text-fg-3">Caricamento…</div>
                    ) : pinSuggestions.length === 0 ? (
                      <div className="p-2 text-[11px] text-fg-3">Nessun media trovato.</div>
                    ) : (
                      pinSuggestions.map((m) => (
                        <button
                          key={m.id}
                          type="button"
                          onClick={() => {
                            setPinned(m.id);
                            setPinSearch('');
                          }}
                          className="block w-full px-3 py-1.5 text-left hover:bg-bg-3"
                        >
                          <div className="truncate text-[12px] text-fg-1">
                            {m.title || '(senza titolo)'}
                          </div>
                          <div className="truncate text-[10px] text-fg-3">
                            {mediaTypeLabel(m.type)}
                            {m.tvSeriesName ? ` · ${m.tvSeriesName}` : ''}
                            {m.tvSeason != null && m.tvEpisode != null
                              ? ` S${m.tvSeason}E${m.tvEpisode}`
                              : ''}
                          </div>
                        </button>
                      ))
                    )}
                  </div>
                </>
              )}
            </div>
          )}

          {ruleType === 'Series' && (
            <div className="space-y-2">
              <label className="block text-[12px] text-fg-2">Series Name</label>
              <input
                className="w-full rounded bg-bg-1 px-2 py-1.5 text-fg-0"
                value={seriesName}
                onChange={(e) => setSeriesName(e.target.value)}
                placeholder="es. Ken il guerriero"
                list="schedule-series-suggestions"
              />
              <datalist id="schedule-series-suggestions">
                {seriesSuggestions.map((s) => (
                  <option key={s} value={s} />
                ))}
              </datalist>
              <p className="text-[10px] text-fg-3">
                Il motore va in ordine S/E e ricorda l'ultimo episodio andato in onda.
              </p>
            </div>
          )}

          {ruleType === 'TagPool' && (
            <div className="space-y-3">
              <div className="space-y-1">
                <div className="text-[12px] text-fg-2">Tag</div>
                <div className="flex flex-wrap gap-1.5">
                  {selectedTags.length === 0 ? (
                    <span className="text-[11px] text-fg-3">Nessun tag selezionato</span>
                  ) : (
                    selectedTags.map((t) => (
                      <span
                        key={t}
                        className="inline-flex items-center gap-1 rounded bg-accent-cfg/30 px-2 py-0.5 text-[11px] text-fg-0"
                      >
                        {t}
                        <button
                          type="button"
                          onClick={() => removeTag(t)}
                          className="rounded text-fg-2 hover:text-fg-0"
                          aria-label={`Rimuovi ${t}`}
                        >
                          <X className="size-3" />
                        </button>
                      </span>
                    ))
                  )}
                </div>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-fg-3" />
                  <input
                    className="w-full rounded bg-bg-1 py-1.5 pl-7 pr-2 text-fg-0 placeholder:text-fg-3"
                    value={tagInput}
                    onChange={(e) => setTagInput(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        addTag(tagInput);
                      }
                    }}
                    placeholder="Cerca o digita un tag e premi Invio…"
                  />
                </div>
                {tagSuggestions.length > 0 && (
                  <div className="max-h-32 overflow-y-auto rounded bg-bg-1">
                    {tagSuggestions.map((t) => (
                      <button
                        key={t.id}
                        type="button"
                        onClick={() => addTag(t.name)}
                        className="block w-full px-3 py-1 text-left text-[12px] text-fg-1 hover:bg-bg-3"
                      >
                        {t.name}
                        {t.label && t.label !== t.name ? (
                          <span className="ml-2 text-[10px] text-fg-3">{t.label}</span>
                        ) : null}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              <div className="space-y-1">
                <div className="text-[12px] text-fg-2">Type</div>
                <div className="flex flex-wrap gap-1.5">
                  {TYPE_OPTIONS.map((t) => {
                    const active = selectedTypes.includes(t.value);
                    return (
                      <button
                        key={t.value}
                        type="button"
                        onClick={() => toggleType(t.value)}
                        className={
                          'rounded px-2 py-1 text-[11px] ' +
                          (active
                            ? 'bg-accent-live text-bg-5'
                            : 'bg-bg-1 text-fg-2 hover:bg-bg-3')
                        }
                      >
                        {t.label}
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
          )}

          <label className="inline-flex items-center gap-2 border-t border-bg-1 pt-3 text-[12px] text-fg-2">
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
            className="rounded bg-bg-1 px-3 py-1.5 text-[12px] text-fg-2 hover:bg-bg-3"
          >
            Annulla
          </button>
          <button
            type="button"
            onClick={submit}
            className="rounded bg-accent-live px-3 py-1.5 text-[12px] font-medium text-bg-5 hover:bg-accent-live-hover"
          >
            Salva
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
