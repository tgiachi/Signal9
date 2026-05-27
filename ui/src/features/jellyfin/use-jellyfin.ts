import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type {
  JellyfinConnectionInput,
  JellyfinConnectionStatus,
  JellyfinLibrarySummary,
  JellyfinServerInfo,
} from './jellyfin-types';

export const JELLYFIN_CONNECTION_QUERY_KEY = ['jellyfin', 'connection'] as const;
export const JELLYFIN_LIBRARIES_QUERY_KEY = ['jellyfin', 'libraries'] as const;

export function useJellyfin() {
  const auth = useAuth();
  const qc = useQueryClient();

  const connection = useQuery({
    queryKey: JELLYFIN_CONNECTION_QUERY_KEY,
    queryFn: () => apiJson<JellyfinConnectionStatus>('/api/jellyfin/connection'),
    enabled: auth.authenticated,
    staleTime: 5_000,
    retry: 1,
  });

  const libraries = useQuery({
    queryKey: JELLYFIN_LIBRARIES_QUERY_KEY,
    queryFn: () => apiJson<JellyfinLibrarySummary[]>('/api/jellyfin/libraries'),
    enabled: auth.authenticated && connection.data?.isConfigured === true,
    staleTime: 5_000,
    retry: 1,
  });

  const saveConnection = useMutation({
    mutationFn: (input: JellyfinConnectionInput) =>
      apiEmpty('/api/jellyfin/connection', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: JELLYFIN_CONNECTION_QUERY_KEY });
    },
  });

  const disconnect = useMutation({
    mutationFn: () => apiEmpty('/api/jellyfin/connection', { method: 'DELETE' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: JELLYFIN_CONNECTION_QUERY_KEY });
      qc.removeQueries({ queryKey: JELLYFIN_LIBRARIES_QUERY_KEY });
    },
  });

  const testConnection = useMutation({
    mutationFn: () => apiJson<JellyfinServerInfo>('/api/jellyfin/test', { method: 'POST' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: JELLYFIN_CONNECTION_QUERY_KEY });
    },
  });

  return {
    authenticated: auth.authenticated,
    connection: connection.data,
    libraries: libraries.data ?? [],
    isConnectionLoading: connection.isLoading,
    isConnectionError: connection.isError,
    connectionError: connection.error,
    isLibrariesLoading: libraries.isLoading,
    isLibrariesError: libraries.isError,
    librariesError: libraries.error,
    saveConnection: saveConnection.mutateAsync,
    disconnect: disconnect.mutateAsync,
    testConnection: testConnection.mutateAsync,
    refreshLibraries: libraries.refetch,
    isSavingConnection: saveConnection.isPending,
    isDisconnecting: disconnect.isPending,
    isTesting: testConnection.isPending,
  };
}
