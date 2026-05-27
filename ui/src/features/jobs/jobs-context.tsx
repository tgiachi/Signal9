import { createContext, type ReactNode, useContext } from 'react';
import { useJobs } from './use-jobs';

type JobsState = ReturnType<typeof useJobs>;

const JobsContext = createContext<JobsState | null>(null);

export function JobsProvider({ children }: { children: ReactNode }) {
  const value = useJobs();
  return <JobsContext.Provider value={value}>{children}</JobsContext.Provider>;
}

export function useJobsContext(): JobsState {
  const value = useContext(JobsContext);
  if (!value) throw new Error('useJobsContext must be inside JobsProvider');
  return value;
}
