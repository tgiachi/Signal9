import { useQuery } from '@tanstack/react-query';
import { apiJson } from '@/lib/api';
import type { ConfigSchemaDocument } from './schema-to-sections';

export const CONFIG_SCHEMA_QUERY_KEY = ['config', 'schema'] as const;

export function useConfigSchema() {
  return useQuery({
    queryKey: CONFIG_SCHEMA_QUERY_KEY,
    queryFn: () => apiJson<ConfigSchemaDocument>('/api/config/schema'),
  });
}
