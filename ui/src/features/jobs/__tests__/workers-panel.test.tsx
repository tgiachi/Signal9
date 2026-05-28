import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from '@/providers/auth-context';
import { WorkersPanel } from '../workers-panel';
import type { WorkerSnapshot } from '../worker-types';

vi.mock('../use-workers', () => ({
  useWorkers: vi.fn(),
}));

import { useWorkers } from '../use-workers';

const AUTH_STATE: AuthState = {
  authenticated: true,
  token: 'token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { username: 'admin', role: 'Admin' },
  login: async () => undefined,
  logout: () => undefined,
};

const ONLINE_WORKER: WorkerSnapshot = {
  workerId: 'worker-1',
  name: 'Worker Alpha',
  version: '1.0.0.0',
  runningJobs: 2,
  maxConcurrentJobs: 4,
  currentJobIds: ['job-1', 'job-2'],
  lastSeenAt: new Date(Date.now() - 5_000).toISOString(),
  online: true,
};

const OFFLINE_WORKER: WorkerSnapshot = {
  workerId: 'worker-2',
  name: 'Worker Beta',
  version: '1.0.0.0',
  runningJobs: 0,
  maxConcurrentJobs: 4,
  currentJobIds: [],
  lastSeenAt: new Date(Date.now() - 60_000).toISOString(),
  online: false,
};

describe('WorkersPanel', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows empty state when no workers are registered', () => {
    vi.mocked(useWorkers).mockReturnValue({ data: [], isLoading: false } as unknown as ReturnType<typeof useWorkers>);

    renderPanel();

    expect(screen.getByText('No workers have registered yet.')).toBeInTheDocument();
  });

  it('shows loading state when data is undefined and isLoading is true', () => {
    vi.mocked(useWorkers).mockReturnValue({ data: undefined, isLoading: true } as unknown as ReturnType<typeof useWorkers>);

    renderPanel();

    expect(screen.getByText('Loading workers…')).toBeInTheDocument();
    expect(screen.getByText('…')).toBeInTheDocument();
  });

  it('renders a single online worker row with job badge and green dot', () => {
    vi.mocked(useWorkers).mockReturnValue({ data: [ONLINE_WORKER], isLoading: false } as unknown as ReturnType<typeof useWorkers>);

    renderPanel();

    expect(screen.getByText('Worker Alpha')).toBeInTheDocument();
    expect(screen.getByText('2/4 jobs')).toBeInTheDocument();
    expect(screen.getByLabelText('online')).toBeInTheDocument();
  });

  it('renders a single offline worker with gray dot', () => {
    vi.mocked(useWorkers).mockReturnValue({ data: [OFFLINE_WORKER], isLoading: false } as unknown as ReturnType<typeof useWorkers>);

    renderPanel();

    expect(screen.getByText('Worker Beta')).toBeInTheDocument();
    expect(screen.getByLabelText('offline')).toBeInTheDocument();
  });

  it('counter reflects online count across multiple workers', () => {
    const workers: WorkerSnapshot[] = [
      ONLINE_WORKER,
      { ...ONLINE_WORKER, workerId: 'worker-3', name: 'Worker Gamma' },
      OFFLINE_WORKER,
    ];
    vi.mocked(useWorkers).mockReturnValue({ data: workers, isLoading: false } as unknown as ReturnType<typeof useWorkers>);

    renderPanel();

    expect(screen.getByText('2/3')).toBeInTheDocument();
  });
});

function renderPanel() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <AuthContext.Provider value={AUTH_STATE}>
      <QueryClientProvider client={client}>
        <WorkersPanel />
      </QueryClientProvider>
    </AuthContext.Provider>,
  );
}
