import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiEmpty } from '@/lib/api';
import type { ChannelEffect } from './stream-types';

export function useChannelEffectsMutation(channelId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (effects: ChannelEffect[]) =>
      apiEmpty(`/api/channels/${channelId}/effects`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ effects }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['channels'] });
      qc.invalidateQueries({ queryKey: ['stream-status', channelId] });
    },
  });
}
