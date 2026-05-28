import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { createFfmpegConnection } from '@/lib/signalr';
import { useAuth } from '@/providers/auth-context';
import type { FfmpegProcessSnapshot } from './ffmpeg-types';

export type FfmpegConnectionState = 'connected' | 'reconnecting' | 'disconnected';

export const FFMPEG_PROCESSES_QUERY_KEY = ['ffmpeg', 'processes'] as const;

export function useFfmpegProcesses() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [connection, setConnection] = useState<FfmpegConnectionState>('disconnected');

  const query = useQuery({
    queryKey: FFMPEG_PROCESSES_QUERY_KEY,
    queryFn: () => apiJson<FfmpegProcessSnapshot[]>('/api/ffmpeg/processes'),
    enabled: auth.authenticated,
    staleTime: 2_000,
    refetchInterval: auth.authenticated ? 10_000 : false,
    retry: 1,
  });

  useEffect(() => {
    if (!auth.authenticated) return undefined;

    const hub = createFfmpegConnection();
    hub.on('processChanged', (snapshot: FfmpegProcessSnapshot) => {
      qc.setQueryData<FfmpegProcessSnapshot[]>(FFMPEG_PROCESSES_QUERY_KEY, (current) =>
        upsertProcess(current ?? [], snapshot),
      );
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
    mutationFn: (processId: string) =>
      apiEmpty(`/api/ffmpeg/processes/${processId}/cancel`, { method: 'POST' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: FFMPEG_PROCESSES_QUERY_KEY });
    },
  });

  const processes = useMemo(
    () =>
      (query.data ?? [])
        .slice()
        .sort((left, right) => Date.parse(right.queuedAt) - Date.parse(left.queuedAt)),
    [query.data],
  );

  return {
    authenticated: auth.authenticated,
    connection,
    processes,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
    cancelProcess: cancel.mutateAsync,
    isCanceling: cancel.isPending,
  };
}

function upsertProcess(
  processes: FfmpegProcessSnapshot[],
  snapshot: FfmpegProcessSnapshot,
): FfmpegProcessSnapshot[] {
  const existing = processes.findIndex((item) => item.id === snapshot.id);
  if (existing < 0) return [snapshot, ...processes];

  const next = processes.slice();
  next[existing] = snapshot;
  return next;
}
