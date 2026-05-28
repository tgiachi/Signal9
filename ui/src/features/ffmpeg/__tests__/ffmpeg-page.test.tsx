import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { FfmpegPage } from '../ffmpeg-page';
import type { FfmpegProcessSnapshot } from '../ffmpeg-types';

const signalr = vi.hoisted(() => {
  const hub = {
    on: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
  };

  return {
    hub,
    createFfmpegConnection: vi.fn(() => hub),
  };
});

vi.mock('@/lib/signalr', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/signalr')>();
  return {
    ...actual,
    createFfmpegConnection: signalr.createFfmpegConnection,
  };
});

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

const PROCESS: FfmpegProcessSnapshot = {
  id: 'process-1',
  pid: 1234,
  executable: 'ffmpeg',
  arguments: ['-i', 'movie.mkv'],
  status: 2,
  queuedAt: '2026-05-28T06:00:00Z',
  startedAt: '2026-05-28T06:01:00Z',
  endedAt: null,
  exitCode: null,
  lastProgress: {
    frameCount: 120,
    fps: 30,
    outTime: '00:00:04',
    speed: 1.2,
    bytesProcessed: 4096,
  },
  recentOutputLines: ['frame=120', 'speed=1.2x'],
  error: null,
};

describe('FfmpegPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    signalr.hub.on.mockClear();
    signalr.hub.start.mockClear();
    signalr.hub.stop.mockClear();
    signalr.createFfmpegConnection.mockClear();
  });

  it('lists FFmpeg processes and cancels a running process', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/ffmpeg/processes/process-1/cancel' && init?.method === 'POST') {
        return new Response(null, { status: 204 });
      }

      return Response.json([PROCESS]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByText('ffmpeg')).toBeInTheDocument();
    expect(screen.getByText('running')).toBeInTheDocument();
    expect(screen.getByText(/frame=120/)).toBeInTheDocument();
    expect(signalr.createFfmpegConnection).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole('button', { name: /cancel ffmpeg process process-1/i }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input) === '/api/ffmpeg/processes/process-1/cancel' &&
            init?.method === 'POST',
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
        <FfmpegPage />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
