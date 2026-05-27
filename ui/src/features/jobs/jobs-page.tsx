import { useState } from 'react';
import { Plus, RefreshCw, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/lib/api';
import { readConfigSummary } from '@/features/config/config-summary';
import { useConfig } from '@/features/config/use-config';
import { useJobsContext } from './jobs-context-value';
import { JobQueuePanel } from './job-queue-panel';
import type { EnqueueJobInput } from './use-jobs';

const DEFAULT_PAYLOAD = '{\n  "source": "demo.mp4"\n}';

export function JobsPage() {
  const jobs = useJobsContext();
  const config = useConfig();
  const configSummary = readConfigSummary(config.text);
  const [type, setType] = useState('transcode');
  const [payload, setPayload] = useState(DEFAULT_PAYLOAD);

  const enqueue = async () => {
    let parsed: Record<string, unknown>;
    try {
      parsed = JSON.parse(payload) as Record<string, unknown>;
    } catch {
      toast.error('Payload JSON is not valid');
      return;
    }

    try {
      const command: EnqueueJobInput = { type, payload: parsed };
      await jobs.enqueueJob(command);
      toast.success('Job enqueued');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (!jobs.authenticated) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="max-w-md rounded-lg border border-border bg-panel p-5 text-center">
          <ShieldAlert className="mx-auto mb-3 size-8 text-warn" />
          <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
          <p className="mt-2 text-sm text-fg-1">
            Job endpoints and job SignalR hubs require an authenticated session.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="grid h-full min-h-0 gap-3 p-3 xl:grid-cols-[minmax(0,1fr)_24rem]">
      <JobQueuePanel
        jobs={jobs.jobs}
        maxConcurrentJobs={configSummary.maxConcurrentJobs}
        onCancel={(jobId) => {
          void jobs.cancelJob(jobId).catch((error) => toast.error(errorMessage(error)));
        }}
        isCanceling={jobs.isCanceling}
      />
      <aside className="flex min-h-0 flex-col gap-3">
        <section className="rounded-lg border border-border bg-panel">
          <header className="flex items-center gap-2 border-b border-border-subtle bg-panel-strong px-3 py-2">
            <Plus className="size-4 text-on-air-2" />
            <h2 className="text-sm font-semibold">Enqueue Job</h2>
          </header>
          <div className="space-y-3 p-3">
            <label className="block">
              <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">
                Type
              </span>
              <input
                value={type}
                onChange={(event) => setType(event.target.value)}
                className="mt-1 w-full rounded border border-border bg-bg-1 px-2 py-1.5 font-mono text-[12px] outline-none focus:border-on-air"
              />
            </label>
            <label className="block">
              <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">
                Payload JSON
              </span>
              <textarea
                value={payload}
                onChange={(event) => setPayload(event.target.value)}
                rows={9}
                className="mt-1 w-full resize-none rounded border border-border bg-bg-1 px-2 py-1.5 font-mono text-[12px] outline-none focus:border-on-air"
              />
            </label>
            <button
              type="button"
              onClick={enqueue}
              disabled={!type.trim() || jobs.isEnqueueing}
              className="inline-flex w-full items-center justify-center gap-2 rounded-md border border-on-air/50 bg-on-air/15 px-3 py-2 text-sm font-semibold text-on-air-2 transition hover:bg-on-air/20 disabled:opacity-40"
            >
              <Plus className="size-4" />
              {jobs.isEnqueueing ? 'Enqueueing' : 'Enqueue'}
            </button>
          </div>
        </section>
        <section className="rounded-lg border border-border bg-panel p-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">SignalR</h2>
            <span className="rounded border border-border bg-bg-2 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-1">
              {jobs.connection}
            </span>
          </div>
          <div className="mt-3 grid grid-cols-2 gap-2 font-mono text-[11px] text-fg-1">
            <Metric label="Queued" value={jobs.counts.queued} />
            <Metric label="Running" value={jobs.counts.running} />
            <Metric label="Completed" value={jobs.counts.completed} />
            <Metric label="Failed" value={jobs.counts.failed} />
          </div>
          {jobs.isError && (
            <div className="mt-3 flex items-center gap-2 rounded border border-error/40 bg-error-bg/40 px-2 py-2 text-[11px] text-error">
              <RefreshCw className="size-3.5" />
              {errorMessage(jobs.error)}
            </div>
          )}
        </section>
      </aside>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded border border-border-subtle bg-bg-1 px-2 py-2">
      <div className="text-fg-2">{label}</div>
      <div className="text-base font-semibold text-fg-0">{value}</div>
    </div>
  );
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 401) return 'Login required for job APIs.';
  if (error instanceof Error) return error.message;
  return 'Job request failed.';
}
