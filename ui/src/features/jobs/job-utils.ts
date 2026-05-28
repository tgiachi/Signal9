import type { JobResponse, JobState } from './job-types';

export function formatJobId(id: string): string {
  return `#${id.replaceAll('-', '').slice(0, 6).toUpperCase()}`;
}

export function formatDateTime(value: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleTimeString('en-GB', { hour12: false });
}

export function formatElapsed(job: JobResponse): string {
  const start = job.startedAt ? Date.parse(job.startedAt) : Date.parse(job.createdAt);
  const end = job.finishedAt ? Date.parse(job.finishedAt) : Date.now();
  if (!Number.isFinite(start) || !Number.isFinite(end) || end < start) return '—';

  const totalSeconds = Math.floor((end - start) / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function stateClass(state: JobState): string {
  switch (state) {
    case 'running':
      return 'bg-accent-jobs text-fg-0';
    case 'queued':
      return 'bg-accent-cfg text-fg-0';
    case 'completed':
      return 'bg-accent-live text-bg-5';
    case 'failed':
      return 'bg-accent-err text-fg-0';
    case 'canceled':
      return 'bg-accent-warn text-bg-0';
  }
}

export function modelForJob(job: JobResponse): 'Spark' | 'Standard' | 'Deep' {
  const type = job.type.toLowerCase();
  if (type.includes('ai') || type.includes('detect') || type.includes('analysis')) return 'Deep';
  if (type.includes('transcode') || type.includes('thumbnail') || type.includes('ingest')) {
    return 'Standard';
  }
  return 'Spark';
}

export function modelClass(model: 'Spark' | 'Standard' | 'Deep'): string {
  switch (model) {
    case 'Spark':
      return 'bg-accent-live text-bg-5';
    case 'Standard':
      return 'bg-accent-jobs text-fg-0';
    case 'Deep':
      return 'bg-accent-cfg text-fg-0';
  }
}

export function sortJobs(jobs: JobResponse[]): JobResponse[] {
  return jobs
    .slice()
    .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));
}

// Order: running first (most recent at top), then queued (oldest first — FIFO preview),
// then terminal states (completed/failed/canceled) by createdAt desc.
const STATE_PRIORITY: Record<string, number> = {
  running: 0,
  queued: 1,
  completed: 2,
  failed: 2,
  canceled: 2,
};

export function sortJobsByPriority(jobs: JobResponse[]): JobResponse[] {
  return jobs.slice().sort((a, b) => {
    const pa = STATE_PRIORITY[a.state] ?? 3;
    const pb = STATE_PRIORITY[b.state] ?? 3;
    if (pa !== pb) return pa - pb;
    if (a.state === 'queued' && b.state === 'queued') {
      return Date.parse(a.createdAt) - Date.parse(b.createdAt);
    }
    return Date.parse(b.createdAt) - Date.parse(a.createdAt);
  });
}
