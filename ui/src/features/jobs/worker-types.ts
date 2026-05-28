export type WorkerSnapshot = {
  workerId: string;
  name: string;
  version: string;
  runningJobs: number;
  maxConcurrentJobs: number;
  currentJobIds: string[];
  lastSeenAt: string;
  online: boolean;
};
