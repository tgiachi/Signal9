import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DirectoryPicker } from '../directory-picker';
import type { FsBrowseResponse } from '@/features/filesystem/fs-types';

vi.mock('@/features/filesystem/use-fs-browse', () => ({
  useFsBrowse: (path: string | null) => mockState(path),
}));

let mockState: (path: string | null) => {
  data?: FsBrowseResponse;
  isLoading: boolean;
  isError: boolean;
  error?: unknown;
};

beforeEach(() => {
  mockState = () => ({ isLoading: false, isError: false });
});

function renderPicker(props: Partial<React.ComponentProps<typeof DirectoryPicker>> = {}) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <DirectoryPicker
        open
        initialPath="/home/squid/media"
        onOpenChange={() => undefined}
        onSelect={() => undefined}
        {...props}
      />
    </QueryClientProvider>,
  );
}

describe('DirectoryPicker', () => {
  it('renders breadcrumb segments split on /', () => {
    mockState = () => ({
      isLoading: false,
      isError: false,
      data: { path: '/home/squid/media', parent: '/home/squid', entries: [] },
    });
    renderPicker();
    expect(screen.getByRole('button', { name: '/' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'home' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'squid' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'media' })).toBeInTheDocument();
  });

  it('renders folder rows as clickable and file rows as plain text', () => {
    mockState = () => ({
      isLoading: false,
      isError: false,
      data: {
        path: '/x',
        parent: '/',
        entries: [
          { name: 'movies', path: '/x/movies', isDirectory: true },
          { name: 'README', path: '/x/README', isDirectory: false },
        ],
      },
    });
    renderPicker({ initialPath: '/x' });
    expect(screen.getByRole('button', { name: /movies/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /README/ })).toBeNull();
    expect(screen.getByText('README')).toBeInTheDocument();
  });

  it('hides hidden entries by default and reveals them when toggle is on', async () => {
    mockState = () => ({
      isLoading: false,
      isError: false,
      data: {
        path: '/x',
        parent: '/',
        entries: [
          { name: '.cache', path: '/x/.cache', isDirectory: true },
          { name: 'movies', path: '/x/movies', isDirectory: true },
        ],
      },
    });
    renderPicker({ initialPath: '/x' });
    expect(screen.queryByRole('button', { name: /\.cache/ })).toBeNull();
    expect(screen.getByRole('button', { name: /movies/ })).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText(/show hidden/i));
    expect(screen.getByRole('button', { name: /\.cache/ })).toBeInTheDocument();
  });

  it('calls onSelect with the current path when "Select this folder" is clicked', async () => {
    mockState = () => ({
      isLoading: false,
      isError: false,
      data: { path: '/x', parent: '/', entries: [] },
    });
    const onSelect = vi.fn();
    renderPicker({ initialPath: '/x', onSelect });

    await userEvent.click(screen.getByRole('button', { name: /select this folder/i }));
    expect(onSelect).toHaveBeenCalledWith('/x');
  });

  it('shows error state when query errors', () => {
    mockState = () => ({
      isLoading: false,
      isError: true,
      error: new Error('Permission denied'),
    });
    renderPicker();
    expect(screen.getByText(/permission denied/i)).toBeInTheDocument();
  });

  it('shows loading state', () => {
    mockState = () => ({ isLoading: true, isError: false });
    renderPicker();
    expect(screen.getByTestId('dp-loading')).toBeInTheDocument();
  });
});
