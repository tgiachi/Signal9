import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StatCard } from '../stat-card';

describe('StatCard', () => {
  it('renders label and value', () => {
    render(<StatCard label="Channels live" value="12" />);
    expect(screen.getByText('Channels live')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('renders optional delta', () => {
    render(<StatCard label="Uptime" value="98.7%" delta="▲ 0.4%" />);
    expect(screen.getByText('▲ 0.4%')).toBeInTheDocument();
  });

  it('applies the live variant class', () => {
    render(<StatCard label="Live" value="12" variant="live" data-testid="sc" />);
    expect(screen.getByTestId('sc')).toHaveClass('bg-accent-live');
  });

  it('applies the warn variant class', () => {
    render(<StatCard label="Warn" value="1" variant="warn" data-testid="sc" />);
    expect(screen.getByTestId('sc')).toHaveClass('bg-accent-warn');
  });
});
