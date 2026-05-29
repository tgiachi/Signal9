import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiEmpty } from '@/lib/api';

export function useRebuildSchedule(channelId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { fromUtc?: string; hoursAhead?: number }) =>
      apiEmpty(`/api/channels/${channelId}/schedule/rebuild`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['schedule-timeline', channelId] });
      qc.invalidateQueries({ queryKey: ['schedule-now', channelId] });
    },
  });
}
