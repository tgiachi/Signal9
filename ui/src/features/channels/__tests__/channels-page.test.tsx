import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { ChannelsPage } from '../channels-page';
import type { ChannelResponse } from '../channel-types';

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

const FIRST_CHANNEL: ChannelResponse = {
  id: 'channel-1',
  name: 'Signal Nine One',
  slug: 'signal-nine-one',
  description: 'Primary broadcast channel',
  logoUrl: null,
  displayOrder: 1,
  isActive: true,
  commercialsEnabled: true,
  commercialIntervalMinSeconds: 120,
  commercialIntervalMaxSeconds: 300,
  outputWidth: 1280,
  outputHeight: 720,
  outputVideoBitrateKbps: 2500,
  videoEffectsJson: null,
  createdAt: '2026-05-27T10:00:00Z',
  updatedAt: '2026-05-27T10:00:00Z',
};

describe('ChannelsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('loads and renders configured channels', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => Response.json([FIRST_CHANNEL])),
    );

    renderPage();

    expect(await screen.findAllByText('Signal Nine One')).toHaveLength(2);
    expect(screen.getAllByText('signal-nine-one')).toHaveLength(2);
    expect(screen.getAllByText('Primary broadcast channel').length).toBeGreaterThan(0);
    expect(screen.getByText(/1 configured · sorted by display order/i)).toBeInTheDocument();
  });

  it('creates a channel through the REST endpoint', async () => {
    const created = {
      ...FIRST_CHANNEL,
      id: 'channel-2',
      name: 'Late Night Signal',
      slug: 'late-night-signal',
      description: null,
      displayOrder: 2,
    };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/channels/logo' && init?.method === 'POST') {
        return Response.json({ logoUrl: '/assets/channel-logos/logo.png' });
      }

      if (String(input) === '/api/channels' && init?.method === 'POST') {
        return Response.json(created, { status: 201 });
      }

      return Response.json([FIRST_CHANNEL]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findAllByText('Signal Nine One');
    await userEvent.click(screen.getByRole('button', { name: /new channel/i }));
    const dialog = within(screen.getByRole('dialog'));
    const logo = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'logo.png', {
      type: 'image/png',
    });
    await userEvent.upload(dialog.getByLabelText('Logo file'), logo);
    await waitFor(() =>
      expect(dialog.getByLabelText('Logo URL')).toHaveValue('/assets/channel-logos/logo.png'),
    );
    await userEvent.type(dialog.getByLabelText('Name'), 'Late Night Signal');
    await userEvent.type(dialog.getByLabelText('Slug'), 'late-night-signal');
    await userEvent.clear(dialog.getByLabelText('Order'));
    await userEvent.type(dialog.getByLabelText('Order'), '2');
    await userEvent.click(dialog.getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(screen.getAllByText('Late Night Signal').length).toBeGreaterThan(0));
    const post = fetchMock.mock.calls.find(
      ([input, init]) => String(input) === '/api/channels' && init?.method === 'POST',
    );
    expect(post).toBeDefined();
    expect(JSON.parse(post![1]!.body as string)).toMatchObject({
      name: 'Late Night Signal',
      slug: 'late-night-signal',
      logoUrl: '/assets/channel-logos/logo.png',
      displayOrder: 2,
      commercialsEnabled: true,
    });
  });

  it('updates the selected channel', async () => {
    const updated = { ...FIRST_CHANNEL, name: 'Signal Nine Prime', isActive: false };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/channels/channel-1' && init?.method === 'PUT') {
        return Response.json(updated);
      }

      return Response.json([FIRST_CHANNEL]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    const [selectButton] = await screen.findAllByRole('button', { name: /signal nine one/i });
    await userEvent.click(selectButton!);
    const dialog = within(screen.getByRole('dialog'));
    expect(dialog.getByRole('heading', { name: 'Edit Channel' })).toBeInTheDocument();
    await userEvent.clear(dialog.getByLabelText('Name'));
    await userEvent.type(dialog.getByLabelText('Name'), 'Signal Nine Prime');
    await userEvent.click(dialog.getByRole('switch', { name: /channel active/i }));
    await userEvent.click(dialog.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(screen.getAllByText('Signal Nine Prime').length).toBeGreaterThan(0));
    const put = fetchMock.mock.calls.find(
      ([input, init]) => String(input) === '/api/channels/channel-1' && init?.method === 'PUT',
    );
    expect(put).toBeDefined();
    expect(JSON.parse(put![1]!.body as string)).toMatchObject({
      name: 'Signal Nine Prime',
      slug: 'signal-nine-one',
      isActive: false,
    });
  });

  it('deletes the selected channel after confirmation', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/channels/channel-1' && init?.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }

      return Response.json([FIRST_CHANNEL]);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderPage();

    const [selectButton] = await screen.findAllByRole('button', { name: /signal nine one/i });
    await userEvent.click(selectButton!);
    const dialog = within(screen.getByRole('dialog'));
    await userEvent.click(dialog.getByRole('button', { name: /^delete$/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/channels/channel-1' && init?.method === 'DELETE',
        ),
      ).toBe(true),
    );
    expect(screen.queryAllByText('Signal Nine One')).toHaveLength(0);
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
        <ChannelsPage />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
