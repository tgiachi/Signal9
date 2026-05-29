import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type { EffectCatalogItem } from './stream-types';

export function useEffectCatalog() {
  const auth = useAuth();
  return useQuery({
    queryKey: ['stream-catalog'],
    queryFn: () => apiJson<EffectCatalogItem[]>('/api/streaming/effects/catalog'),
    enabled: auth.authenticated,
    staleTime: Number.POSITIVE_INFINITY,
  });
}
