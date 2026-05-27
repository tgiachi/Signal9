export type JobState = 'queued' | 'running' | 'completed' | 'failed' | 'canceled';

export type JobResponse = {
  id: string;
  type: string;
  state: JobState;
  progressPercent: number;
  progressMessage: string;
  error: string;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
};

export type RawJobResponse = Omit<JobResponse, 'state'> & {
  state: JobState | number | string;
};

export type JobLogLevel =
  | 'trace'
  | 'debug'
  | 'information'
  | 'warning'
  | 'error'
  | 'critical';

export type JobLogResponse = {
  sequence: number;
  jobId: string;
  timestamp: string;
  level: JobLogLevel | number | string;
  message: string;
};

const STATES: JobState[] = ['queued', 'running', 'completed', 'failed', 'canceled'];

export function normalizeJob(raw: RawJobResponse): JobResponse {
  return { ...raw, state: normalizeJobState(raw.state) };
}

export function normalizeJobState(value: JobState | number | string): JobState {
  if (typeof value === 'number') return STATES[value] ?? 'queued';

  const normalized = String(value).trim().toLowerCase();
  const state = STATE_BY_NAME[normalized];
  if (state) return state;

  return 'queued';
}

const STATE_BY_NAME: Record<string, JobState> = {
  queued: 'queued',
  running: 'running',
  completed: 'completed',
  failed: 'failed',
  canceled: 'canceled',
};
