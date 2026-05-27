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
    <section className="flex min-h-0 flex-col overflow-hidden rounded-[6px] bg-bg-2">
      <header className="flex items-center gap-3 bg-bg-4 px-3 py-2">
        <div className="flex size-7 items-center justify-center rounded-[4px] bg-bg-2 text-fg-2">
          <Loader2 className="size-4" />
        </div>
        <div className="min-w-0">
          <h2 className="text-sm font-semibold text-fg-0">Job Queue</h2>
          <p className="font-mono text-[10px] text-fg-3">
            {running.length}/{maxConcurrentJobs} slots running
          </p>
        </div>
        <div className="ml-auto flex items-center gap-1 font-mono text-[10px]">
          <span className="rounded-[3px] bg-accent-cfg px-2 py-1 text-fg-0">
            {queued.length} queued
          </span>
          <span className="rounded-[3px] bg-accent-jobs px-2 py-1 text-fg-0">
            {running.length} running
          </span>
        </div>
      </header>
      <div className="min-h-0 flex-1 overflow-auto">
        {visible.length === 0 ? (
          <div className="flex h-full min-h-[12rem] items-center justify-center text-sm text-fg-3">
            {emptyLabel}
          </div>
        ) : (
          <div className="min-w-[42rem]">
            <div className="grid grid-cols-[5rem_minmax(12rem,1fr)_6rem_8rem_5rem_3rem] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
              <span>ID</span>
              <span>Job</span>
              <span>Model</span>
              <span>Progress</span>
              <span>Elapsed</span>
              <span className="text-right">Act</span>
            </div>
            {visible.map((job, idx) => {
              const model = modelForJob(job);
              return (
                <div
                  key={job.id}
                  className={cn(
                    'grid grid-cols-[5rem_minmax(12rem,1fr)_6rem_8rem_5rem_3rem] gap-3 px-3 py-2 text-[12px] hover:bg-[#343b41]',
                    idx % 2 ? 'bg-bg-3' : 'bg-bg-2',
                  )}
                >
                  <span className="font-mono text-fg-1">{formatJobId(job.id)}</span>
                  <div className="min-w-0">
                    <div className="truncate font-medium text-fg-0">{job.type}</div>
                    <div className="truncate font-mono text-[10px] text-fg-3">
                      {job.progressMessage || job.error || job.state}
                    </div>
                  </div>
                  <span
                    className={cn(
                      'h-fit w-fit rounded-[3px] px-1.5 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.08em]',
                      modelClass(model),
                    )}
                  >
                    {model}
                  </span>
                  <div className="min-w-0">
                    <div className="h-1.5 overflow-hidden rounded-full bg-bg-1">
                      <div
                        className={cn(
                          'h-full rounded-full',
                          job.state === 'failed' ? 'bg-accent-err' : 'bg-accent-live',
                        )}
                        style={{
                          width: `${Math.max(0, Math.min(100, job.progressPercent))}%`,
                        }}
                      />
                    </div>
                    <div className="mt-1 flex items-center justify-between font-mono text-[10px] text-fg-3">
                      <span>{job.progressPercent}%</span>
                      <span
                        className={cn(
                          'rounded-[3px] px-1.5 py-0.5 font-bold uppercase tracking-[0.08em]',
                          stateClass(job.state),
                        )}
                      >
                        {job.state}
                      </span>
                    </div>
                  </div>
                  <span className="flex items-center gap-1 font-mono text-fg-1">
                    <Clock3 className="size-3 text-fg-3" />
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
                      className="flex size-7 items-center justify-center rounded-[6px] bg-accent-err text-fg-0 transition hover:opacity-90 disabled:bg-bg-2 disabled:text-fg-3 disabled:opacity-40"
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
