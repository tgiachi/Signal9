import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { ScheduleBlock, ScheduleBlockInput } from './schedule-types';

export function blocksKey(channelId: string) {
  return ['schedule-blocks', channelId] as const;
}

export function useScheduleBlocks(channelId: string) {
  const auth = useAuth();
  const qc = useQueryClient();

  const list = useQuery({
    queryKey: blocksKey(channelId),
    queryFn: () => apiJson<ScheduleBlock[]>(`/api/channels/${channelId}/schedule/blocks`),
    enabled: auth.authenticated && !!channelId,
  });

  const create = useMutation({
    mutationFn: (input: ScheduleBlockInput) =>
      apiJson<ScheduleBlock>(`/api/channels/${channelId}/schedule/blocks`, {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(channelId) }),
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: ScheduleBlockInput }) =>
      apiJson<ScheduleBlock>(`/api/schedule/blocks/${id}`, {
        method: 'PUT',
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(channelId) }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => apiEmpty(`/api/schedule/blocks/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(channelId) }),
  });

  return { list, create, update, remove };
}
