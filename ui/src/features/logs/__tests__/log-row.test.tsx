import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { LogRow } from '../log-row';
import type { LogEntry } from '../log-entry';

const make = (level: LogEntry['level']): LogEntry => ({
  ts: '2026-05-27T11:32:14.123Z',
  level,
  source: 'FreeSql',
  message: 'connect failed',
});

describe('LogRow', () => {
  it('renders timestamp HH:MM:SS', () => {
    render(<LogRow entry={make('info')} />);
    expect(screen.getByTestId('log-ts').textContent).toMatch(/\d{2}:\d{2}:\d{2}/);
  });
  it('renders level uppercased with color class for error', () => {
    render(<LogRow entry={make('error')} />);
    const lvl = screen.getByTestId('log-lvl');
    expect(lvl).toHaveTextContent('ERROR');
    expect(lvl).toHaveClass('text-error');
  });
  it('renders source and message', () => {
    render(<LogRow entry={make('warn')} />);
    expect(screen.getByText('FreeSql')).toBeInTheDocument();
    expect(screen.getByText('connect failed')).toBeInTheDocument();
  });
});
