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
      return 'border-on-air/40 bg-on-air/10 text-on-air-2';
    case 'queued':
      return 'border-cyan/40 bg-cyan/10 text-cyan';
    case 'completed':
      return 'border-border bg-bg-3 text-fg-1';
    case 'failed':
      return 'border-error/50 bg-error-bg/60 text-error';
    case 'canceled':
      return 'border-warn/40 bg-warn/10 text-warn';
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
      return 'border-on-air/40 bg-on-air/10 text-on-air-2';
    case 'Standard':
      return 'border-cyan/40 bg-cyan/10 text-cyan';
    case 'Deep':
      return 'border-violet/40 bg-violet/10 text-violet';
  }
}

export function sortJobs(jobs: JobResponse[]): JobResponse[] {
  return jobs
    .slice()
    .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));
}
