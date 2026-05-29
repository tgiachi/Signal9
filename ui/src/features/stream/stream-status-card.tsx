import { useStreamStatus } from './use-stream-status';

function fmtSecAgo(iso: string | null | undefined, now: number) {
  if (!iso) return '—';
  const diff = Math.max(0, Math.floor((now - new Date(iso).getTime()) / 1000));
  return `${diff}s ago`;
}

export function StreamStatusCard({ channelId }: { channelId: string }) {
  const { data } = useStreamStatus(channelId);
  const now = Date.now();
  if (!data) {
    return (
      <div className="rounded-md bg-bg-2 p-3 text-[12px] text-fg-3">
        Stream idle. Apri il player per avviarlo.
      </div>
    );
  }
  return (
    <div className="rounded-md bg-bg-2 p-3 text-[12px] text-fg-2">
      <div className="flex items-center gap-2">
        <span
          className={'size-2 rounded-full ' + (data.running ? 'bg-accent-live' : 'bg-fg-3')}
        />
        <span className="font-semibold text-fg-1">
          {data.running ? 'Running' : 'Stopped'}
        </span>
        {data.running && data.currentEntryTitle ? (
          <span className="truncate text-fg-3">
            · {data.currentEntryTitle}
            {data.currentEntryPartCount && data.currentEntryPartCount > 1
              ? ` (parte ${(data.currentEntryPartIndex ?? 0) + 1}/${data.currentEntryPartCount})`
              : ''}
          </span>
        ) : null}
      </div>
      <div className="mt-1 grid grid-cols-3 gap-2 text-[10px] text-fg-3">
        <div>last viewer {fmtSecAgo(data.lastViewerAt, now)}</div>
        <div>next segment #{data.nextSegmentNumber}</div>
        <div>ffmpeg pid {data.ffmpegPid ?? '—'}</div>
      </div>
    </div>
  );
}
