import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { MediaLibrariesPage } from '../media-libraries-page';

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

const LIBRARY = {
  id: 'library-1',
  name: 'Movies',
  description: 'Feature films',
  defaultMediaType: 3,
  sourceType: 0,
  sourceRef: 'jf-movies',
  isActive: true,
  lastScannedAt: '2026-05-27T10:00:00Z',
  createdAt: '2026-05-27T09:00:00Z',
  updatedAt: '2026-05-27T09:00:00Z',
};

describe('MediaLibrariesPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists libraries and creates a new media library through the API', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/media-libraries' && init?.method === 'POST') {
        return Response.json({ ...LIBRARY, id: 'library-2', name: 'Shows' }, { status: 201 });
      }
      return Response.json([LIBRARY]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByText('Feature films')).toBeInTheDocument();
    expect(screen.getByText('Movies media')).toBeInTheDocument();
    expect(screen.getByText(/Jellyfin.*jf-movies/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /new library/i }));
    const dialog = within(screen.getByRole('dialog'));
    await userEvent.type(dialog.getByLabelText('Name'), 'Shows');
    await userEvent.type(dialog.getByLabelText('Source reference'), 'jf-shows');
    await userEvent.click(dialog.getByRole('button', { name: /^create$/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries' &&
            init?.method === 'POST' &&
            JSON.parse(init.body as string).sourceRef === 'jf-shows',
        ),
      ).toBe(true),
    );
  });

  it('edits the active flag and deletes a library after confirmation', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/media-libraries/library-1' && init?.method === 'PUT') {
        return Response.json({ ...LIBRARY, isActive: false });
      }
      if (url === '/api/media-libraries/library-1' && init?.method === 'DELETE') {
        return new Response('', { status: 204 });
      }
      return Response.json([LIBRARY]);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderPage();

    await screen.findByText('Feature films');
    await userEvent.click(screen.getByRole('button', { name: /edit Movies/i }));
    const dialog = within(screen.getByRole('dialog'));
    await userEvent.click(dialog.getByLabelText('Active library'));
    await userEvent.click(dialog.getByRole('button', { name: /^save$/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries/library-1' &&
            init?.method === 'PUT' &&
            JSON.parse(init.body as string).isActive === false,
        ),
      ).toBe(true),
    );

    await userEvent.click(screen.getByRole('button', { name: /delete Movies/i }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries/library-1' && init?.method === 'DELETE',
        ),
      ).toBe(true),
    );
  });

  it('enqueues a scan job for an active library', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/media-libraries/library-1/scan' && init?.method === 'POST') {
        return Response.json(
          {
            id: 'job-1',
            type: 'library.scan',
            state: 'queued',
            progressPercent: 0,
            progressMessage: '',
            error: '',
            createdAt: '2026-05-28T07:00:00Z',
            startedAt: null,
            finishedAt: null,
          },
          { status: 202 },
        );
      }

      return Response.json([LIBRARY]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findByText('Feature films');
    await userEvent.click(screen.getByRole('button', { name: /scan Movies/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries/library-1/scan' &&
            init?.method === 'POST',
        ),
      ).toBe(true),
    );
  });

  it('force-processes all media after user confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (
        String(input) === '/api/media-libraries/library-1/process-all' &&
        init?.method === 'POST'
      ) {
        return Response.json({ enqueuedCount: 5 }, { status: 200 });
      }
      return Response.json([LIBRARY]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findByText('Feature films');
    await userEvent.click(screen.getByRole('button', { name: /force process all Movies/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media-libraries/library-1/process-all' &&
            init?.method === 'POST',
        ),
      ).toBe(true),
    );
    confirmSpy.mockRestore();
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
        <MediaLibrariesPage />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
