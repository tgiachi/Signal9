import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { normalizeJob, type JobResponse, type RawJobResponse } from '@/features/jobs/job-types';
import { JOBS_QUERY_KEY } from '@/features/jobs/use-jobs';
import { useAuth } from '@/providers/auth-context';
import type { ChannelMediaResponse } from './channel-media-types';

export const CHANNEL_MEDIA_QUERY_KEY = ['media'] as const;

function upsertJob(jobs: JobResponse[], job: JobResponse): JobResponse[] {
  const existing = jobs.findIndex((item) => item.id === job.id);
  if (existing < 0) return [job, ...jobs];

  const next = jobs.slice();
  next[existing] = job;
  return next;
}

export function useChannelMedia() {
  const auth = useAuth();
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: CHANNEL_MEDIA_QUERY_KEY,
    queryFn: () => apiJson<ChannelMediaResponse[]>('/api/media'),
    enabled: auth.authenticated,
    staleTime: 5_000,
    retry: 1,
  });

  const pipeline = useMutation({
    mutationFn: async (mediaId: string) => {
      const data = await apiJson<RawJobResponse>(`/api/media/${mediaId}/pipeline`, {
        method: 'POST',
      });
      return normalizeJob(data);
    },
    onSuccess: (job) => {
      qc.setQueryData<JobResponse[]>(JOBS_QUERY_KEY, (current) => upsertJob(current ?? [], job));
    },
  });

  const media = useMemo(
    () => (query.data ?? []).slice().sort((left, right) => left.title.localeCompare(right.title)),
    [query.data],
  );

  return {
    authenticated: auth.authenticated,
    media,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
    runPipeline: pipeline.mutateAsync,
    isRunningPipeline: pipeline.isPending,
  };
}
