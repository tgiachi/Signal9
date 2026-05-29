import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { ScheduleNow } from './schedule-types';

export function nowKey(channelId: string) {
  return ['schedule-now', channelId] as const;
}

export function useScheduleNow(channelId: string) {
  const auth = useAuth();
  return useQuery({
    queryKey: nowKey(channelId),
    queryFn: () => apiJson<ScheduleNow>(`/api/channels/${channelId}/schedule/now`),
    enabled: auth.authenticated && !!channelId,
    refetchInterval: 5_000,
  });
}
