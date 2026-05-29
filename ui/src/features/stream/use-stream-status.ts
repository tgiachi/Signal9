import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { ChannelStreamSnapshot } from './stream-types';

export function useStreamStatus(channelId: string) {
  const auth = useAuth();
  return useQuery({
    queryKey: ['stream-status', channelId] as const,
    queryFn: () => apiJson<ChannelStreamSnapshot | null>(`/api/channels/${channelId}/stream/status`),
    enabled: auth.authenticated && !!channelId,
    refetchInterval: 5_000,
  });
}
