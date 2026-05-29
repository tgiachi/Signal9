import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';

export type TagSummary = {
  id: string;
  name: string;
  label: string | null;
};

export const TAGS_QUERY_KEY = ['tags'] as const;

export function useTags() {
  const auth = useAuth();
  return useQuery({
    queryKey: TAGS_QUERY_KEY,
    queryFn: () => apiJson<TagSummary[]>('/api/tags'),
    enabled: auth.authenticated,
    staleTime: 60_000,
  });
}
