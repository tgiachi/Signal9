import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';
import { useConfigSchema } from '../use-config-schema';

function makeWrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string) => {
      if (input === '/api/config/schema') {
        return Response.json({
          type: 'object',
          properties: {
            Pipeline: {
              type: 'object',
              title: 'Media pipeline',
            },
          },
        });
      }
      return new Response('not found', { status: 404 });
    }),
  );
});

describe('useConfigSchema', () => {
  it('loads config schema from GET /api/config/schema', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useConfigSchema(), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.properties?.Pipeline?.title).toBe('Media pipeline');
  });
});
