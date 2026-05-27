import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { JellyfinPage } from '../jellyfin-page';

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

describe('JellyfinPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('tests the configured server and registers a Jellyfin library as a media library', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/jellyfin/connection') {
        return Response.json({
          isConfigured: true,
          baseUrl: 'http://jellyfin.local:8096',
          lastVerifiedAt: null,
        });
      }
      if (url === '/api/jellyfin/libraries') {
        return Response.json([
          { id: 'jf-movies', name: 'Movies', collectionType: 'movies' },
        ]);
      }
      if (url === '/api/jellyfin/test' && init?.method === 'POST') {
        return Response.json({ serverName: 'Living Room', version: '10.10.6', id: 'server-1' });
      }
      if (url === '/api/media-libraries' && init?.method === 'POST') {
        return Response.json(
          {
            id: 'ml-1',
            name: 'Movies',
            description: null,
            defaultMediaType: 3,
            sourceType: 0,
            sourceRef: 'jf-movies',
            isActive: true,
            lastScannedAt: null,
            createdAt: '2026-05-27T10:00:00Z',
            updatedAt: '2026-05-27T10:00:00Z',
          },
          { status: 201 },
        );
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByText('http://jellyfin.local:8096')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /test connection/i }));
    expect(await screen.findByText(/Living Room/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /register as medialibrary/i }));
    const dialog = within(screen.getByRole('dialog'));
    expect(dialog.getByLabelText('Source reference')).toHaveValue('jf-movies');
    await userEvent.click(dialog.getByRole('button', { name: /^create$/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries' &&
            init?.method === 'POST' &&
            JSON.parse(init.body as string).sourceRef === 'jf-movies',
        ),
      ).toBe(true),
    );
  });

  it('shows a disabled library section until Jellyfin is configured', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        if (String(input) === '/api/jellyfin/connection') {
          return Response.json({ isConfigured: false, baseUrl: null, lastVerifiedAt: null });
        }
        return new Response('', { status: 404 });
      }),
    );

    renderPage();

    expect(await screen.findByText('Configure connection first')).toBeInTheDocument();
    expect(screen.queryByText('Libraries on this server')).toBeInTheDocument();
  });

  it('marks a Jellyfin library as already registered when create returns conflict', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/jellyfin/connection') {
        return Response.json({
          isConfigured: true,
          baseUrl: 'http://jellyfin.local:8096',
          lastVerifiedAt: null,
        });
      }
      if (url === '/api/jellyfin/libraries') {
        return Response.json([
          { id: 'jf-shows', name: 'Shows', collectionType: 'tvshows' },
        ]);
      }
      if (url === '/api/media-libraries' && init?.method === 'POST') {
        return new Response('duplicate', { status: 409 });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findByText('Shows');
    await userEvent.click(screen.getByRole('button', { name: /register as medialibrary/i }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^create$/i }));

    expect(await screen.findByText('already registered')).toBeInTheDocument();
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <AuthContext.Provider value={AUTH_STATE}>
      <QueryClientProvider client={client}>
        <JellyfinPage />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
