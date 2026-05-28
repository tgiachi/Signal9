import { Ban, Cpu, RefreshCw, ShieldAlert, TerminalSquare } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/lib/api';
import {
  formatBytes,
  isTerminalStatus,
  normalizeFfmpegStatus,
  type FfmpegProcessSnapshot,
  type FfmpegStatus,
} from './ffmpeg-types';
import { useFfmpegProcesses } from './use-ffmpeg-processes';

export function FfmpegPage() {
  const ffmpeg = useFfmpegProcesses();

  const cancel = async (process: FfmpegProcessSnapshot) => {
    try {
      await ffmpeg.cancelProcess(process.id);
      toast.success(`FFmpeg process canceled: ${process.id.slice(0, 8)}`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (!ffmpeg.authenticated) {
    return <AuthRequired />;
  }

  const running = ffmpeg.processes.filter((process) =>
    ['queued', 'starting', 'running'].includes(normalizeFfmpegStatus(process.status)),
  );

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3 xl:overflow-hidden">
      <div className="grid gap-3 md:grid-cols-3">
        <SummaryMetric label="Processes" value={String(ffmpeg.processes.length)} />
        <SummaryMetric label="Active" value={String(running.length)} />
        <SummaryMetric label="SignalR" value={ffmpeg.connection} />
      </div>

      <section className="flex min-h-[30rem] flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex flex-wrap items-center gap-3 bg-bg-4 px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-[4px] bg-accent-cfg text-fg-0">
            <Cpu className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">FFmpeg Pool</h1>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              processChanged hub: {ffmpeg.connection}
            </p>
          </div>
          <button
            type="button"
            onClick={() => {
              void ffmpeg.refresh();
            }}
            className="ml-auto inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0"
          >
            <RefreshCw className="size-3.5" />
            Refresh
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-auto bg-bg-0">
          <div className="grid min-w-[76rem] grid-cols-[7rem_9rem_minmax(15rem,1fr)_9rem_11rem_minmax(14rem,1fr)_4rem] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
            <span>ID</span>
            <span>Status</span>
            <span>Command</span>
            <span>Progress</span>
            <span>Runtime</span>
            <span>Output</span>
            <span>Act</span>
          </div>
          {ffmpeg.isLoading ? (
            <EmptyState text="Loading FFmpeg processes." />
          ) : ffmpeg.isError ? (
            <EmptyState text={errorMessage(ffmpeg.error)} tone="error" />
          ) : ffmpeg.processes.length === 0 ? (
            <EmptyState text="No FFmpeg processes in the registry." />
          ) : (
            ffmpeg.processes.map((process, index) => (
              <ProcessRow
                key={process.id}
                process={process}
                index={index}
                onCancel={cancel}
                isCanceling={ffmpeg.isCanceling}
              />
            ))
          )}
        </div>
      </section>
    </div>
  );
}

function ProcessRow({
  process,
  index,
  onCancel,
  isCanceling,
}: {
  process: FfmpegProcessSnapshot;
  index: number;
  onCancel: (process: FfmpegProcessSnapshot) => void;
  isCanceling: boolean;
}) {
  const status = normalizeFfmpegStatus(process.status);
  const output = process.recentOutputLines.slice(-2).join(' · ') || process.error || 'No output';

  return (
    <div
      className={
        'grid min-w-[76rem] grid-cols-[7rem_9rem_minmax(15rem,1fr)_9rem_11rem_minmax(14rem,1fr)_4rem] items-center gap-3 px-3 py-3 ' +
        (index % 2 ? 'bg-bg-3' : 'bg-bg-2')
      }
    >
      <span className="font-mono text-[12px] text-fg-1">{process.id.slice(0, 8)}</span>
      <span
        className={
          'w-fit rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-label ' +
          statusClass(status)
        }
      >
        {status}
      </span>
      <div className="min-w-0">
        <div className="truncate text-sm font-semibold text-fg-0">{process.executable}</div>
        <div className="truncate font-mono text-[10px] text-fg-3">
          {process.arguments.join(' ')}
        </div>
      </div>
      <ProgressCell process={process} />
      <span className="font-mono text-[12px] text-fg-2">{runtime(process)}</span>
      <span className="flex min-w-0 items-center gap-2">
        <TerminalSquare className="size-3.5 shrink-0 text-fg-3" />
        <span className="truncate font-mono text-[11px] text-fg-2">{output}</span>
      </span>
      <button
        type="button"
        aria-label={`Cancel FFmpeg process ${process.id}`}
        disabled={isCanceling || isTerminalStatus(status)}
        onClick={() => onCancel(process)}
        className="flex size-7 items-center justify-center rounded-[6px] bg-accent-err text-fg-0 transition hover:opacity-90 disabled:bg-bg-1 disabled:text-fg-3 disabled:opacity-40"
      >
        <Ban className="size-3.5" />
      </button>
    </div>
  );
}

function ProgressCell({ process }: { process: FfmpegProcessSnapshot }) {
  const progress = process.lastProgress;
  if (!progress) return <span className="font-mono text-[12px] text-fg-3">No progress</span>;

  return (
    <div className="font-mono text-[11px] text-fg-2">
      <div>{progress.outTime}</div>
      <div className="text-fg-3">
        {progress.frameCount}f · {progress.fps.toFixed(1)}fps · {progress.speed.toFixed(1)}x
      </div>
      <div className="text-fg-3">{formatBytes(progress.bytesProcessed)}</div>
    </div>
  );
}

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <section className="min-w-0 rounded-[6px] bg-bg-2 p-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-lg font-semibold text-fg-0">{value}</div>
    </section>
  );
}

function EmptyState({ text, tone = 'muted' }: { text: string; tone?: 'muted' | 'error' }) {
  return (
    <div
      className={
        'flex h-56 items-center justify-center text-sm ' +
        (tone === 'error' ? 'text-accent-err' : 'text-fg-3')
      }
    >
      {text}
    </div>
  );
}

function AuthRequired() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <div className="max-w-md rounded-[6px] bg-bg-2 p-5 text-center">
        <ShieldAlert className="mx-auto mb-3 size-8 text-accent-warn" />
        <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
        <p className="mt-2 text-sm text-fg-2">
          FFmpeg pool endpoints and SignalR require an authenticated session.
        </p>
      </div>
    </div>
  );
}

function statusClass(status: FfmpegStatus): string {
  if (status === 'completed') return 'bg-accent-live text-bg-5';
  if (status === 'failed') return 'bg-accent-err text-fg-0';
  if (status === 'canceled') return 'bg-bg-1 text-fg-3';
  if (status === 'running') return 'bg-accent-jobs text-fg-0';
  return 'bg-accent-warn text-bg-0';
}

function runtime(process: FfmpegProcessSnapshot): string {
  const start = Date.parse(process.startedAt ?? process.queuedAt);
  const end = Date.parse(process.endedAt ?? new Date().toISOString());
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return 'Unknown';

  const seconds = Math.round((end - start) / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  return `${minutes}m ${seconds % 60}s`;
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409 && typeof error.body === 'string') return error.body;
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'FFmpeg request failed.';
}
