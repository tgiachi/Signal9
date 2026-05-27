import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { LogFilterBar } from '../log-filter-bar';

function Harness({ onSearchChange }: { onSearchChange?: (v: string) => void }) {
  const [search, setSearch] = useState('');
  return (
    <LogFilterBar
      level="all"
      search={search}
      onLevelChange={vi.fn()}
      onSearchChange={(v) => {
        setSearch(v);
        onSearchChange?.(v);
      }}
    />
  );
}

describe('LogFilterBar', () => {
  it('renders all chips and search input', () => {
    render(
      <LogFilterBar level="all" search="" onLevelChange={vi.fn()} onSearchChange={vi.fn()} />,
    );
    ['all', 'info', 'warn', 'error'].forEach((l) =>
      expect(screen.getByRole('button', { name: l })).toBeInTheDocument(),
    );
    expect(screen.getByPlaceholderText('search…')).toBeInTheDocument();
  });

  it('calls onLevelChange when a chip is clicked', async () => {
    const onLevelChange = vi.fn();
    render(
      <LogFilterBar
        level="all"
        search=""
        onLevelChange={onLevelChange}
        onSearchChange={vi.fn()}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'warn' }));
    expect(onLevelChange).toHaveBeenCalledWith('warn');
  });

  it('calls onSearchChange as user types', async () => {
    const onSearchChange = vi.fn();
    render(<Harness onSearchChange={onSearchChange} />);
    await userEvent.type(screen.getByPlaceholderText('search…'), 'jwt');
    expect(onSearchChange).toHaveBeenLastCalledWith('jwt');
  });
});
