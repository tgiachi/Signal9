export type FfmpegProcessStatusType = 0 | 1 | 2 | 3 | 4 | 5;

export type FfmpegProgressUpdate = {
  frameCount: number;
  fps: number;
  outTime: string;
  speed: number;
  bytesProcessed: number;
};

export type FfmpegProcessSnapshot = {
  id: string;
  pid: number | null;
  executable: string;
  arguments: string[];
  status: FfmpegProcessStatusType | string;
  queuedAt: string;
  startedAt: string | null;
  endedAt: string | null;
  exitCode: number | null;
  lastProgress: FfmpegProgressUpdate | null;
  recentOutputLines: string[];
  error: string | null;
};

const STATUSES = ['queued', 'starting', 'running', 'completed', 'failed', 'canceled'] as const;

export type FfmpegStatus = (typeof STATUSES)[number];

export function normalizeFfmpegStatus(value: FfmpegProcessSnapshot['status']): FfmpegStatus {
  if (typeof value === 'number') return STATUSES[value] ?? 'queued';

  const normalized = String(value).trim().toLowerCase();
  return STATUSES.find((status) => status === normalized) ?? 'queued';
}

export function isTerminalStatus(status: FfmpegStatus): boolean {
  return status === 'completed' || status === 'failed' || status === 'canceled';
}

export function formatBytes(value: number): string {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}
