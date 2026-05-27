import { Ban, Clock3, Loader2 } from 'lucide-react';
import { cn } from '@/lib/cn';
import type { JobResponse } from './job-types';
import {
  formatElapsed,
  formatJobId,
  modelClass,
  modelForJob,
  sortJobs,
  stateClass,
} from './job-utils';

type Props = {
  jobs: JobResponse[];
  maxConcurrentJobs: number;
  onCancel?: (jobId: string) => void;
  isCanceling?: boolean;
  compact?: boolean;
  emptyLabel?: string;
};

export function JobQueuePanel({
  jobs,
  maxConcurrentJobs,
  onCancel,
  isCanceling = false,
  compact = false,
  emptyLabel = 'No jobs in memory.',
}: Props) {
  const sorted = sortJobs(jobs);
  const running = sorted.filter((job) => job.state === 'running');
  const queued = sorted.filter((job) => job.state === 'queued');
  const terminal = sorted.filter((job) =>
    ['completed', 'failed', 'canceled'].includes(job.state),
  );
  const visible = compact ? [...running, ...queued, ...terminal].slice(0, 7) : sorted;

  return (
    <section className="flex min-h-0 flex-col overflow-hidden rounded-lg border border-border bg-panel">
      <header className="flex items-center gap-3 border-b border-border-subtle bg-panel-strong px-3 py-2">
        <div className="flex size-7 items-center justify-center rounded-md border border-border bg-bg-2 text-fg-1">
          <Loader2 className="size-4" />
        </div>
        <div className="min-w-0">
          <h2 className="text-sm font-semibold text-fg-0">Job Queue</h2>
          <p className="font-mono text-[10px] text-fg-2">
            {running.length}/{maxConcurrentJobs} slots running
          </p>
        </div>
        <div className="ml-auto flex items-center gap-1 font-mono text-[10px] text-fg-2">
          <span className="rounded border border-cyan/40 bg-cyan/10 px-2 py-1 text-cyan">
            {queued.length} queued
          </span>
          <span className="rounded border border-on-air/40 bg-on-air/10 px-2 py-1 text-on-air-2">
            {running.length} running
          </span>
        </div>
      </header>
      <div className="min-h-0 flex-1 overflow-auto">
        {visible.length === 0 ? (
          <div className="flex h-full min-h-[12rem] items-center justify-center text-sm text-fg-2">
            {emptyLabel}
          </div>
        ) : (
          <div className="min-w-[42rem]">
            <div className="grid grid-cols-[5rem_minmax(12rem,1fr)_6rem_8rem_5rem_3rem] gap-3 border-b border-border-subtle px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-2">
              <span>ID</span>
              <span>Job</span>
              <span>Model</span>
              <span>Progress</span>
              <span>Elapsed</span>
              <span className="text-right">Act</span>
            </div>
            {visible.map((job) => {
              const model = modelForJob(job);
              return (
                <div
                  key={job.id}
                  className="grid grid-cols-[5rem_minmax(12rem,1fr)_6rem_8rem_5rem_3rem] gap-3 border-b border-border-subtle/70 px-3 py-2 text-[12px] hover:bg-bg-2"
                >
                  <span className="font-mono text-fg-1">{formatJobId(job.id)}</span>
                  <div className="min-w-0">
                    <div className="truncate font-medium text-fg-0">{job.type}</div>
                    <div className="truncate font-mono text-[10px] text-fg-2">
                      {job.progressMessage || job.error || job.state}
                    </div>
                  </div>
                  <span
                    className={cn(
                      'h-fit w-fit rounded border px-1.5 py-0.5 font-mono text-[10px]',
                      modelClass(model),
                    )}
                  >
                    {model}
                  </span>
                  <div className="min-w-0">
                    <div className="h-1.5 overflow-hidden rounded-full bg-bg-3">
                      <div
                        className={cn(
                          'h-full rounded-full',
                          job.state === 'failed' ? 'bg-error' : 'bg-on-air',
                        )}
                        style={{ width: `${Math.max(0, Math.min(100, job.progressPercent))}%` }}
                      />
                    </div>
                    <div className="mt-1 flex items-center justify-between font-mono text-[10px] text-fg-2">
                      <span>{job.progressPercent}%</span>
                      <span className={cn('rounded border px-1', stateClass(job.state))}>
                        {job.state}
                      </span>
                    </div>
                  </div>
                  <span className="flex items-center gap-1 font-mono text-fg-1">
                    <Clock3 className="size-3 text-fg-2" />
                    {formatElapsed(job)}
                  </span>
                  <div className="flex justify-end">
                    <button
                      type="button"
                      disabled={
                        !onCancel ||
                        isCanceling ||
                        !['queued', 'running'].includes(job.state)
                      }
                      onClick={() => onCancel?.(job.id)}
                      className="flex size-7 items-center justify-center rounded border border-error/40 text-error transition hover:bg-error-bg disabled:border-border disabled:text-fg-2 disabled:opacity-40"
                      title="Cancel job"
                    >
                      <Ban className="size-3.5" />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
