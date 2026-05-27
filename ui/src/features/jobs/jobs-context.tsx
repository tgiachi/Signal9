import { type ReactNode } from 'react';
import { JobsContext } from './jobs-context-value';
import { useJobs } from './use-jobs';

export function JobsProvider({ children }: { children: ReactNode }) {
  const value = useJobs();
  return <JobsContext.Provider value={value}>{children}</JobsContext.Provider>;
}
