import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthGate } from '@/providers/auth-gate';
import { AuthProvider } from '@/providers/auth-provider';

vi.mock('@/lib/signalr', () => {
  const connection = {
    start: () => Promise.resolve(),
    stop: () => Promise.resolve(),
    on: () => undefined,
    off: () => undefined,
    onclose: () => undefined,
    onreconnecting: () => undefined,
    onreconnected: () => undefined,
  };

  return {
    createLogsConnection: () => connection,
    createJobLogsConnection: () => connection,
    createJobStatusConnection: () => connection,
    setSignalRAccessTokenProvider: () => undefined,
  };
});

import App from '@/app';

describe('App authentication gate', () => {
  let store: Map<string, string>;

  beforeEach(() => {
    store = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => store.set(key, value),
      removeItem: (key: string) => store.delete(key),
      clear: () => store.clear(),
    });
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 401 })));
  });

  it('shows only the login screen when no JWT session exists', async () => {
    render(<App />);

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /control room access/i })).toBeInTheDocument(),
    );
    expect(screen.getByTestId('login-vanta-background')).toBeInTheDocument();
    expect(screen.queryByText('UI guarded')).not.toBeInTheDocument();
    expect(screen.queryByText('jobs locked')).not.toBeInTheDocument();
    expect(screen.queryByText('SignalR after login')).not.toBeInTheDocument();
    expect(screen.queryByText('Broadcast Monitor')).not.toBeInTheDocument();
  });

  it('unlocks the console after a successful login', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/auth/login' && init?.method === 'POST') {
        return Response.json({
          accessToken: 'token',
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
          user: { username: 'admin', role: 'Admin' },
        });
      }
      if (url === '/api/config') {
        return new Response(
          'LogLevel = 3\nLogToFile = true\nDatabaseType = 0\nDatabaseUrl = "sqlite://signalnine.db"\n[Jwt]\nIssuer = "SignalNine"\nExpirationMinutes = 60\n[JobSystem]\nMaxConcurrentJobs = 2\nMaxLogEntriesPerJob = 500\n',
          { status: 200, headers: { 'Content-Type': 'text/plain' } },
        );
      }
      if (url === '/health' || url === '/live') {
        return new Response('', { status: 200 });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);

    render(
      <AuthProvider>
        <AuthGate>
          <div>Console unlocked</div>
        </AuthGate>
      </AuthProvider>,
    );
    await userEvent.type(screen.getByLabelText(/password/i), 'correct-password');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByText('Console unlocked')).toBeInTheDocument());
    expect(store.get('signalnine.auth')).toContain('token');
  });
});
