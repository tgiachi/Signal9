import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError, apiText } from '@/lib/api';

const KEY = ['config'] as const;

export type SaveStatus = 'idle' | 'success' | 'error';

export type ConfigError = { message: string; line?: number; column?: number };

export function useConfig() {
  const qc = useQueryClient();
  const [lastSaveStatus, setLastSaveStatus] = useState<SaveStatus>('idle');
  const [lastError, setLastError] = useState<ConfigError | null>(null);

  const query = useQuery({
    queryKey: KEY,
    queryFn: () => apiText('/api/config'),
  });

  const mutation = useMutation({
    mutationFn: async (text: string) => {
      await apiText('/api/config', {
        method: 'POST',
        headers: { 'Content-Type': 'text/plain' },
        body: text,
      });
      return text;
    },
    onSuccess: (text) => {
      qc.setQueryData(KEY, text);
      setLastSaveStatus('success');
      setLastError(null);
    },
    onError: (e: unknown) => {
      setLastSaveStatus('error');
      if (e instanceof ApiError && e.status === 422 && e.body && typeof e.body === 'object') {
        const b = e.body as { message?: string; line?: number; column?: number };
        setLastError({
          message: b.message ?? 'Validation error',
          line: b.line,
          column: b.column,
        });
      } else {
        setLastError({ message: e instanceof Error ? e.message : 'Unknown error' });
      }
    },
  });

  return {
    text: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    save: (text: string) => mutation.mutateAsync(text),
    isSaving: mutation.isPending,
    lastSaveStatus,
    lastError,
  };
}
