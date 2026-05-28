import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { WorkerSnapshot } from './worker-types';

export const WORKERS_QUERY_KEY = ['workers'] as const;

export function useWorkers() {
  const auth = useAuth();
  return useQuery({
    queryKey: WORKERS_QUERY_KEY,
    queryFn: () => apiJson<WorkerSnapshot[]>('/api/workers'),
    enabled: auth.authenticated,
    refetchInterval: auth.authenticated ? 5_000 : false,
  });
}
