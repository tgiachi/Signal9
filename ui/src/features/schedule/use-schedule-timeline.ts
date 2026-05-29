import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { ScheduleTimeline } from './schedule-types';

export function timelineKey(channelId: string, from: string, to: string) {
  return ['schedule-timeline', channelId, from, to] as const;
}

export function useScheduleTimeline(channelId: string, fromIso: string, toIso: string) {
  const auth = useAuth();
  return useQuery({
    queryKey: timelineKey(channelId, fromIso, toIso),
    queryFn: () => apiJson<ScheduleTimeline>(
      `/api/channels/${channelId}/schedule/timeline?from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`),
    enabled: auth.authenticated && !!channelId,
    refetchInterval: 30_000,
  });
}
