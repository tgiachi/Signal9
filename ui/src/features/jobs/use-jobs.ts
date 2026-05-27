import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { createJobStatusConnection } from '@/lib/signalr';
import { useAuth } from '@/providers/auth-context';
import { normalizeJob, type JobResponse, type RawJobResponse } from './job-types';

export type JobsConnectionState = 'connected' | 'reconnecting' | 'disconnected';

export type EnqueueJobInput = {
  type: string;
  payload: Record<string, unknown>;
};

export const JOBS_QUERY_KEY = ['jobs'] as const;

export function useJobs() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [connection, setConnection] = useState<JobsConnectionState>('disconnected');

  const query = useQuery({
    queryKey: JOBS_QUERY_KEY,
    queryFn: async () => {
      const data = await apiJson<RawJobResponse[]>('/api/jobs/');
      return data.map(normalizeJob);
    },
    enabled: auth.authenticated,
    staleTime: 2_000,
    refetchInterval: auth.authenticated ? 10_000 : false,
    retry: 1,
  });

  useEffect(() => {
    if (!auth.authenticated) {
      setConnection('disconnected');
      return undefined;
    }

    const hub = createJobStatusConnection();
    hub.on('JobStatusChanged', (raw: RawJobResponse) => {
      const next = normalizeJob(raw);
      qc.setQueryData<JobResponse[]>(JOBS_QUERY_KEY, (current) => upsertJob(current ?? [], next));
    });
    hub.onreconnecting(() => setConnection('reconnecting'));
    hub.onreconnected(() => setConnection('connected'));
    hub.onclose(() => setConnection('disconnected'));
    hub
      .start()
      .then(() => setConnection('connected'))
      .catch(() => setConnection('disconnected'));

    return () => {
      hub.stop().catch(() => undefined);
    };
  }, [auth.authenticated, auth.token, qc]);

  const cancel = useMutation({
    mutationFn: (jobId: string) => apiEmpty(`/api/jobs/${jobId}/cancel`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: JOBS_QUERY_KEY }),
  });

  const enqueue = useMutation({
    mutationFn: async (input: EnqueueJobInput) => {
      const data = await apiJson<RawJobResponse>('/api/jobs/', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      });
      return normalizeJob(data);
    },
    onSuccess: (job) => {
      qc.setQueryData<JobResponse[]>(JOBS_QUERY_KEY, (current) => upsertJob(current ?? [], job));
    },
  });

  const jobs = query.data ?? [];
  const counts = useMemo(
    () => ({
      queued: jobs.filter((job) => job.state === 'queued').length,
      running: jobs.filter((job) => job.state === 'running').length,
      completed: jobs.filter((job) => job.state === 'completed').length,
      failed: jobs.filter((job) => job.state === 'failed').length,
      canceled: jobs.filter((job) => job.state === 'canceled').length,
    }),
    [jobs],
  );

  return {
    authenticated: auth.authenticated,
    connection,
    jobs,
    counts,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    cancelJob: cancel.mutateAsync,
    isCanceling: cancel.isPending,
    enqueueJob: enqueue.mutateAsync,
    isEnqueueing: enqueue.isPending,
  };
}

function upsertJob(jobs: JobResponse[], job: JobResponse): JobResponse[] {
  const existing = jobs.findIndex((item) => item.id === job.id);
  if (existing < 0) return [job, ...jobs];

  const next = jobs.slice();
  next[existing] = job;
  return next;
}
