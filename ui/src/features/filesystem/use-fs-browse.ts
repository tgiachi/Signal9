import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import type { FsBrowseResponse } from './fs-types';

export function useFsBrowse(path: string | null) {
  return useQuery({
    queryKey: ['fs-browse', path],
    enabled: path !== null,
    staleTime: 30_000,
    queryFn: () => {
      const target = path ?? '/';
      return apiJson<FsBrowseResponse>(
        `/api/fs/browse?path=${encodeURIComponent(target)}`,
      );
    },
  });
}
