import { useEffect, useState } from 'react';
import { useScheduleTimeline } from './use-schedule-timeline';
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

export function ScheduleEpgStrip({ channelId }: { channelId: string }) {
  const [fromIso] = useState(nowIso);
  const [toIso] = useState(() => plusHoursIso(12));
  const { data, isLoading } = useScheduleTimeline(channelId, fromIso, toIso);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  if (isLoading) return <div className="p-2 text-fg-3">Loading EPG…</div>;
  if (!data || data.entries.length === 0) {
    return (
      <div className="p-2 text-fg-3">
        No timeline planned. Hit &ldquo;Rebuild&rdquo; to generate.
      </div>
    );
  }

  const span = new Date(data.to).getTime() - new Date(data.from).getTime();
  const nowOffset = Math.max(0, Math.min(1, (now - new Date(data.from).getTime()) / span));

  return (
    <div className="relative h-16 overflow-hidden rounded-md bg-bg-2 px-2 py-2">
      <div className="relative h-full w-full">
        {data.entries.map((entry: ScheduledEntry) => {
          const start = new Date(entry.startAt).getTime();
          const left = Math.max(0, (start - new Date(data.from).getTime()) / span) * 100;
          const width = ((entry.durationSeconds * 1000) / span) * 100;
          return (
            <div
              key={entry.id}
              className={
                'absolute top-0 h-full overflow-hidden rounded-sm px-1.5 ' +
                (KIND_COLOR[entry.kind] ?? 'bg-bg-3')
              }
              style={{ left: `${left}%`, width: `${Math.max(width, 0.6)}%` }}
              title={`${entry.title} (${entry.kind})`}
            >
              <div className="truncate text-[10px] text-fg-1">{entry.title}</div>
            </div>
          );
        })}
        <div
          className="absolute top-0 h-full w-px bg-accent-live"
          style={{ left: `${nowOffset * 100}%` }}
        />
      </div>
    </div>
  );
}
