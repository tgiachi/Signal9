import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { ChannelMediaPage } from '../channel-media-page';
import type { ChannelMediaResponse } from '../channel-media-types';

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

const MEDIA: ChannelMediaResponse = {
  id: 'media-1',
  type: 3,
  title: 'The Signal',
  durationSeconds: 3661,
  isActive: true,
  sourceType: 0,
  sourceRef: 'jellyfin-item-1',
  movieReleaseYear: 1988,
  movieDirector: 'A. Director',
  tvSeriesName: null,
  tvSeason: null,
  tvEpisode: null,
  commercialAdvertiser: null,
  commercialCampaign: null,
  informationEdition: null,
  createdAt: '2026-05-28T06:00:00Z',
  updatedAt: '2026-05-28T06:00:00Z',
  tags: [],
};

describe('ChannelMediaPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists media rows and enqueues the media pipeline', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/media/media-1/pipeline' && init?.method === 'POST') {
        return Response.json(
          {
            id: 'job-1',
            type: 'media.pipeline',
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

      return Response.json([MEDIA]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByText('The Signal')).toBeInTheDocument();
    expect(screen.getByText('Movies media')).toBeInTheDocument();
    expect(screen.getByText('01:01:01')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /run pipeline for The Signal/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/media/media-1/pipeline' && init?.method === 'POST',
        ),
      ).toBe(true),
    );
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
        <ChannelMediaPage />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
