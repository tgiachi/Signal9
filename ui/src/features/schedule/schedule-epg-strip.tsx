import { useEffect, useMemo, useState } from 'react';
import { Clock3, PlayCircle, Tv } from 'lucide-react';
import { useScheduleTimeline } from './use-schedule-timeline';
import { useScheduleNow } from './use-schedule-now';
import type { ScheduledEntry } from './schedule-types';

const KIND_COLOR: Record<string, string> = {
  Media: 'bg-bg-3',
  Bumper: 'bg-accent-cfg',
  Commercial: 'bg-accent-jobs',
};

function nowIso() {
  return new Date().toISOString();
}

function plusHoursIso(hours: number) {
  return new Date(Date.now() + hours * 3_600_000).toISOString();
}

function formatHm(date: Date) {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
}

function formatDuration(seconds: number) {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  if (m >= 60) {
    const h = Math.floor(m / 60);
    const rem = m % 60;
    return `${h}h${String(rem).padStart(2, '0')}`;
  }
  return `${m}:${String(s).padStart(2, '0')}`;
}

function kindBadge(kind: string) {
  switch (kind) {
    case 'Media':
      return { color: 'bg-accent-live', label: 'Media' };
    case 'Bumper':
      return { color: 'bg-accent-cfg', label: 'Bumper' };
    case 'Commercial':
      return { color: 'bg-accent-jobs', label: 'Spot' };
    default:
      return { color: 'bg-bg-3', label: kind };
  }
}

export function ScheduleEpgStrip({ channelId }: { channelId: string }) {
  const [fromIso] = useState(nowIso);
  const [toIso] = useState(() => plusHoursIso(2));
  const { data: timeline, isLoading: timelineLoading } = useScheduleTimeline(channelId, fromIso, toIso);
  const { data: nowData } = useScheduleNow(channelId);

  const [tick, setTick] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  const upNext = useMemo(() => {
    if (!timeline) return [];
    return timeline.entries
      .filter((e) => e.kind === 'Media')
      .filter((e) => new Date(e.startAt).getTime() > tick)
      .slice(0, 5);
  }, [timeline, tick]);

  if (timelineLoading) {
    return <div className="rounded-md bg-bg-2 p-3 text-[12px] text-fg-3">Loading EPG…</div>;
  }
  if (!timeline || timeline.entries.length === 0) {
    return (
      <div className="rounded-md bg-bg-2 p-3 text-[12px] text-fg-3">
        Nessuna timeline programmata. Premi <strong>Rebuild</strong> per generarla.
      </div>
    );
  }

  const current = nowData?.current ?? null;
  const next = nowData?.next ?? null;
  const secondsIn = nowData?.secondsIntoCurrent ?? 0;
  const totalCurrent = current?.durationSeconds ?? 0;
  const remainCurrent = Math.max(0, totalCurrent - secondsIn);
  const pct = totalCurrent > 0 ? Math.min(100, (secondsIn / totalCurrent) * 100) : 0;

  const span = new Date(timeline.to).getTime() - new Date(timeline.from).getTime();
  const nowOffset = Math.max(0, Math.min(1, (tick - new Date(timeline.from).getTime()) / span));

  return (
    <div className="space-y-3">
      <div className="grid gap-3 md:grid-cols-[2fr_1fr]">
        {/* Now Playing */}
        <div className="rounded-md bg-bg-2 p-3">
          <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wide text-fg-3">
            <PlayCircle className="size-3.5 text-accent-live" />
            On air
          </div>
          {current ? (
            <>
              <div className="mt-1 truncate text-[15px] font-semibold text-fg-0">
                {current.title || '(senza titolo)'}
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-fg-3">
                <span className={`inline-block size-2 rounded-full ${kindBadge(current.kind).color}`} />
                <span>{kindBadge(current.kind).label}</span>
                {current.partCount > 1 ? (
                  <span>
                    parte {current.partIndex + 1}/{current.partCount}
                  </span>
                ) : null}
                <span>
                  {formatDuration(secondsIn)} / {formatDuration(totalCurrent)}
                </span>
                <span>resta {formatDuration(remainCurrent)}</span>
              </div>
              <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-bg-1">
                <div
                  className="h-full bg-accent-live transition-[width] duration-1000 ease-linear"
                  style={{ width: `${pct}%` }}
                />
              </div>
            </>
          ) : (
            <div className="mt-1 text-[12px] text-fg-3">Nessun media in onda.</div>
          )}
        </div>

        {/* Next */}
        <div className="rounded-md bg-bg-2 p-3">
          <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wide text-fg-3">
            <Clock3 className="size-3.5 text-accent-jobs" />
            Adesso poi
          </div>
          {next ? (
            <>
              <div className="mt-1 truncate text-[13px] font-semibold text-fg-1">
                {next.title || '(senza titolo)'}
              </div>
              <div className="mt-1 text-[11px] text-fg-3">
                {formatHm(new Date(next.startAt))} · {kindBadge(next.kind).label} ·{' '}
                {formatDuration(next.durationSeconds)}
              </div>
            </>
          ) : (
            <div className="mt-1 text-[12px] text-fg-3">Nessun entry successivo.</div>
          )}
        </div>
      </div>

      {/* Up next list (next 5 media only) */}
      {upNext.length > 0 && (
        <div className="rounded-md bg-bg-2">
          <div className="border-b border-bg-1 px-3 py-2 text-[10px] font-semibold uppercase tracking-wide text-fg-3">
            <Tv className="mr-1 inline size-3.5" /> Prossimi media
          </div>
          <div>
            {upNext.map((e, idx) => (
              <div
                key={e.id}
                className={
                  'flex items-center gap-3 px-3 py-1.5 ' +
                  (idx % 2 ? 'bg-bg-3' : 'bg-bg-2')
                }
              >
                <span className="font-mono text-[11px] text-fg-3">
                  {formatHm(new Date(e.startAt))}
                </span>
                <span className="flex-1 truncate text-[12px] text-fg-1">{e.title}</span>
                <span className="font-mono text-[10px] text-fg-3">
                  {formatDuration(e.durationSeconds)}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Mini-strip (next 2h) */}
      <div className="relative h-10 overflow-hidden rounded-md bg-bg-2 px-1 py-1">
        <div className="relative h-full w-full">
          {timeline.entries.map((entry: ScheduledEntry) => {
            const start = new Date(entry.startAt).getTime();
            const left = Math.max(0, (start - new Date(timeline.from).getTime()) / span) * 100;
            const width = ((entry.durationSeconds * 1000) / span) * 100;
            return (
              <div
                key={entry.id}
                className={
                  'absolute top-0 h-full overflow-hidden ' +
                  (KIND_COLOR[entry.kind] ?? 'bg-bg-3')
                }
                style={{ left: `${left}%`, width: `${Math.max(width, 0.3)}%` }}
                title={`${entry.title} (${entry.kind}, ${formatDuration(entry.durationSeconds)})`}
              />
            );
          })}
          <div
            className="absolute top-0 z-10 h-full w-px bg-accent-live"
            style={{ left: `${nowOffset * 100}%` }}
          />
        </div>
        <div className="pointer-events-none mt-1 flex justify-between text-[9px] text-fg-3">
          <span>{formatHm(new Date(timeline.from))}</span>
          <span>{formatHm(new Date((new Date(timeline.from).getTime() + new Date(timeline.to).getTime()) / 2))}</span>
          <span>{formatHm(new Date(timeline.to))}</span>
        </div>
      </div>
    </div>
  );
}
