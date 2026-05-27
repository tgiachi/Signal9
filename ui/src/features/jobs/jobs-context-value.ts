import { createContext, useContext } from 'react';
import type { useJobs } from './use-jobs';

export type JobsState = ReturnType<typeof useJobs>;

export const JobsContext = createContext<JobsState | null>(null);

export function useJobsContext(): JobsState {
  const value = useContext(JobsContext);
  if (!value) throw new Error('useJobsContext must be inside JobsProvider');
  return value;
}
