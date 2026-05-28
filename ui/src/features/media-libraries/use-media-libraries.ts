import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { normalizeJob, type JobResponse, type RawJobResponse } from '@/features/jobs/job-types';
import { JOBS_QUERY_KEY } from '@/features/jobs/use-jobs';
import { useAuth } from '@/providers/auth-context';
import type {
  CreateMediaLibraryRequest,
  MediaLibraryResponse,
  UpdateMediaLibraryRequest,
} from './media-library-types';

export const MEDIA_LIBRARIES_QUERY_KEY = ['media-libraries'] as const;
export const mediaLibraryQueryKey = (id: string) => ['media-libraries', id] as const;

type UpdateMediaLibraryCommand = {
  id: string;
  input: UpdateMediaLibraryRequest;
};

function upsertJob(jobs: JobResponse[], job: JobResponse): JobResponse[] {
  const existing = jobs.findIndex((item) => item.id === job.id);
  if (existing < 0) return [job, ...jobs];

  const next = jobs.slice();
  next[existing] = job;
  return next;
}

export function useMediaLibrary(id: string | null) {
  const auth = useAuth();

  return useQuery({
    queryKey: mediaLibraryQueryKey(id ?? 'pending'),
    queryFn: () => apiJson<MediaLibraryResponse>(`/api/media-libraries/${id}`),
    enabled: auth.authenticated && id !== null,
    staleTime: 5_000,
    retry: 1,
  });
}

export function useMediaLibraries() {
  const auth = useAuth();
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: MEDIA_LIBRARIES_QUERY_KEY,
    queryFn: () => apiJson<MediaLibraryResponse[]>('/api/media-libraries'),
    enabled: auth.authenticated,
    staleTime: 5_000,
    retry: 1,
  });

  const create = useMutation({
    mutationFn: (input: CreateMediaLibraryRequest) =>
      apiJson<MediaLibraryResponse>('/api/media-libraries', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: MEDIA_LIBRARIES_QUERY_KEY });
    },
  });

  const update = useMutation({
    mutationFn: ({ id, input }: UpdateMediaLibraryCommand) =>
      apiJson<MediaLibraryResponse>(`/api/media-libraries/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: MEDIA_LIBRARIES_QUERY_KEY });
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => apiEmpty(`/api/media-libraries/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: MEDIA_LIBRARIES_QUERY_KEY });
    },
  });

  const scan = useMutation({
    mutationFn: async (id: string) => {
      const data = await apiJson<RawJobResponse>(`/api/media-libraries/${id}/scan`, {
        method: 'POST',
      });
      return normalizeJob(data);
    },
    onSuccess: (job) => {
      qc.setQueryData<JobResponse[]>(JOBS_QUERY_KEY, (current) => upsertJob(current ?? [], job));
      void qc.invalidateQueries({ queryKey: MEDIA_LIBRARIES_QUERY_KEY });
    },
  });

  const libraries = useMemo(
    () => (query.data ?? []).slice().sort((left, right) => left.name.localeCompare(right.name)),
    [query.data],
  );

  return {
    authenticated: auth.authenticated,
    libraries,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
    createMediaLibrary: create.mutateAsync,
    updateMediaLibrary: update.mutateAsync,
    deleteMediaLibrary: remove.mutateAsync,
    scanMediaLibrary: scan.mutateAsync,
    isSaving: create.isPending || update.isPending,
    isDeleting: remove.isPending,
    isScanning: scan.isPending,
  };
}
