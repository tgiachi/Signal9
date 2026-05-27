import {
  Activity,
  Database,
  RadioTower,
  ShieldCheck,
  Signal,
  SlidersHorizontal,
} from 'lucide-react';
import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { useHealthState, type EndpointStatus } from '@/lib/health';
import { useAuth } from '@/providers/auth-context';
import { readConfigSummary } from '@/features/config/config-summary';
import { useConfig } from '@/features/config/use-config';
import { useJobsContext } from '@/features/jobs/jobs-context-value';
import { JobQueuePanel } from '@/features/jobs/job-queue-panel';
import { useLogStreamContext } from '@/features/logs/log-stream-ctx';
import { LogRow } from '@/features/logs/log-row';
import { Pill } from '@/components/ui/pill';

export function MonitorPage() {
  const auth = useAuth();
  const health = useHealthState();
  const logs = useLogStreamContext();
  const jobs = useJobsContext();
  const config = useConfig();
  const summary = readConfigSummary(config.text);
  const recentLogs = logs.entries.slice(-18);

  return (
    <div className="grid h-full min-h-0 gap-3 p-3 xl:grid-cols-[minmax(0,1.35fr)_minmax(24rem,0.85fr)]">
      <section className="flex min-h-0 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex flex-wrap items-center gap-2 bg-bg-4 px-3 py-2">
          <div className="flex size-7 items-center justify-center rounded-[4px] bg-accent-live text-bg-5">
            <RadioTower className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Broadcast Monitor</h1>
            <p className="font-mono text-[10px] text-fg-3">
              SignalR logs: {logs.connection} · jobs:{' '}
              {auth.authenticated ? jobs.connection : 'locked'}
            </p>
          </div>
          <div className="ml-auto flex flex-wrap gap-1">
            <StatusPill label="/health" status={health.health.status} />
            <StatusPill label="/live" status={health.live.status} />
            <StatusPill label="JWT" status={auth.authenticated ? 'ok' : 'unknown'} />
          </div>
        </header>
        <div className="grid gap-3 bg-bg-2 p-3 md:grid-cols-4">
          <Metric
            icon={<Signal className="size-4" />}
            label="On-air"
            value={logs.connection === 'connected' ? 'Live' : 'Offline'}
            tone={logs.connection === 'connected' ? 'ok' : 'warn'}
          />
          <Metric
            icon={<Activity className="size-4" />}
            label="Running jobs"
            value={`${jobs.counts.running}/${summary.maxConcurrentJobs}`}
            tone={jobs.counts.failed > 0 ? 'error' : 'ok'}
          />
          <Metric
            icon={<ShieldCheck className="size-4" />}
            label="JWT"
            value={auth.user?.username ?? 'not logged in'}
            tone={auth.authenticated ? 'ok' : 'warn'}
          />
          <Metric
            icon={<Database className="size-4" />}
            label="Database"
            value={summary.databaseType}
            tone={summary.valid ? 'ok' : 'error'}
          />
        </div>
        <div className="min-h-0 flex-1 overflow-auto bg-bg-0">
          <div className="grid min-w-[44rem] grid-cols-[5.5rem_4rem_minmax(7rem,11rem)_minmax(0,1fr)] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
            <span>Time</span>
            <span>Level</span>
            <span>Source</span>
            <span>Message</span>
          </div>
          {recentLogs.length === 0 ? (
            <div className="flex h-56 items-center justify-center text-sm text-fg-3">
              Waiting for live log entries.
            </div>
          ) : (
            recentLogs.map((entry, index) => (
              <LogRow key={`${entry.ts}-${index}`} entry={entry} />
            ))
          )}
        </div>
        <footer className="flex items-center justify-between bg-bg-4 px-3 py-2 font-mono text-[10px] text-fg-3">
          <span>Messages: {logs.entries.length}</span>
          <span>Errors (1m): {logs.errorCountLastMinute}</span>
        </footer>
      </section>
      <aside className="grid min-h-0 gap-3 xl:grid-rows-[minmax(0,1fr)_auto]">
        <JobQueuePanel
          compact
          jobs={jobs.jobs}
          maxConcurrentJobs={summary.maxConcurrentJobs}
          emptyLabel={
            jobs.authenticated ? 'No jobs in memory.' : 'Login required for job queue.'
          }
          onCancel={
            jobs.authenticated
              ? (jobId) => {
                  void jobs.cancelJob(jobId);
                }
              : undefined
          }
          isCanceling={jobs.isCanceling}
        />
        <section className="overflow-hidden rounded-[6px] bg-bg-2">
          <header className="flex items-center gap-2 bg-bg-4 px-3 py-2">
            <SlidersHorizontal className="size-4 text-accent-cfg" />
            <h2 className="text-sm font-semibold text-fg-0">System Config</h2>
            <Pill variant={summary.valid ? 'live' : 'err'} className="ml-auto">
              TOML {summary.valid ? 'valid' : 'invalid'}
            </Pill>
          </header>
          <div className="grid gap-0 md:grid-cols-2">
            <ConfigRow label="Database URL" value={summary.databaseUrl} />
            <ConfigRow label="Logging" value={`${summary.logLevel} / file ${summary.logToFile}`} />
            <ConfigRow label="JWT issuer" value={summary.jwtIssuer} />
            <ConfigRow label="JWT expires" value={summary.jwtExpiration} />
            <ConfigRow label="Max jobs" value={String(summary.maxConcurrentJobs)} />
            <ConfigRow label="Job log cap" value={String(summary.maxLogEntriesPerJob)} />
          </div>
        </section>
      </aside>
    </div>
  );
}

function StatusPill({ label, status }: { label: string; status: EndpointStatus }) {
  const variant = status === 'ok' ? 'live' : status === 'down' ? 'err' : 'health';
  return (
    <Pill variant={variant}>
      {label} {status}
    </Pill>
  );
}

function Metric({
  icon,
  label,
  value,
  tone,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  tone: 'ok' | 'warn' | 'error';
}) {
  return (
    <div className="min-w-0 rounded-[6px] bg-bg-3 p-3">
      <div
        className={cn(
          'mb-2 flex size-7 items-center justify-center rounded-[4px]',
          tone === 'ok' && 'bg-accent-live text-bg-5',
          tone === 'warn' && 'bg-accent-warn text-bg-0',
          tone === 'error' && 'bg-accent-err text-fg-0',
        )}
      >
        {icon}
      </div>
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-base font-semibold text-fg-0">{value}</div>
    </div>
  );
}

function ConfigRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 bg-bg-2 px-3 py-2 md:odd:bg-bg-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate font-mono text-[12px] text-fg-1">{value}</div>
    </div>
  );
}
