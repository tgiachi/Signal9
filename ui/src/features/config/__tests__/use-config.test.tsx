import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';
import { useConfig } from '../use-config';

function makeWrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string, init?: RequestInit) => {
      if (input === '/api/config' && (!init || init.method === undefined || init.method === 'GET')) {
        return new Response('[logging]\nlevel = "information"\n', {
          status: 200,
          headers: { 'Content-Type': 'text/plain' },
        });
      }
      if (input === '/api/config' && init?.method === 'POST') {
        return new Response('', { status: 200 });
      }
      return new Response('not found', { status: 404 });
    }),
  );
});

describe('useConfig', () => {
  it('loads config text from GET /api/config', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useConfig(), { wrapper: makeWrapper(client) });
    await waitFor(() => expect(result.current.text).toBeDefined());
    expect(result.current.text).toContain('[logging]');
  });

  it('saves text via POST and reports success', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useConfig(), { wrapper: makeWrapper(client) });
    await waitFor(() => expect(result.current.text).toBeDefined());
    await result.current.save('[jwt]\nissuer = "s9"\n');
    await waitFor(() => expect(result.current.lastSaveStatus).toBe('success'));
  });
});
