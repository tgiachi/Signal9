import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
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
    isSaving: create.isPending || update.isPending,
    isDeleting: remove.isPending,
  };
}
