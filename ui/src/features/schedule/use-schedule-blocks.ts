import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { ScheduleBlock, ScheduleBlockInput } from './schedule-types';

export function blocksKey(channelId: string) {
  return ['schedule-blocks', channelId] as const;
}

const DAY_INDEX: Record<string, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

type RawBlock = Omit<ScheduleBlock, 'dayOfWeek'> & { dayOfWeek: number | string };

function normalize(raw: RawBlock): ScheduleBlock {
  const day =
    typeof raw.dayOfWeek === 'number'
      ? raw.dayOfWeek
      : (DAY_INDEX[raw.dayOfWeek] ?? 0);
  return { ...raw, dayOfWeek: day };
}

export function useScheduleBlocks(channelId: string) {
  const auth = useAuth();
  const qc = useQueryClient();

  const list = useQuery({
    queryKey: blocksKey(channelId),
    queryFn: async () => {
      const data = await apiJson<RawBlock[]>(`/api/channels/${channelId}/schedule/blocks`);
      return data.map(normalize);
    },
    enabled: auth.authenticated && !!channelId,
  });

  const create = useMutation({
    mutationFn: (input: ScheduleBlockInput) =>
      apiJson<ScheduleBlock>(`/api/channels/${channelId}/schedule/blocks`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(channelId) }),
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: ScheduleBlockInput }) =>
      apiJson<ScheduleBlock>(`/api/schedule/blocks/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
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
