import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StatusBar } from '../status-bar';

describe('StatusBar', () => {
  it('renders the live pill in green when connected', () => {
    render(<StatusBar connection="connected" configOk errorCount={0} />);
    const pill = screen.getByTestId('pill-live');
    expect(pill).toHaveTextContent(/ON AIR/);
    expect(pill).toHaveClass('bg-accent-live');
  });

  it('renders the error pill with count when errors > 0', () => {
    render(<StatusBar connection="connected" configOk errorCount={3} />);
    expect(screen.getByTestId('pill-err')).toHaveTextContent('3 err');
  });

  it('renders reconnecting state', () => {
    render(<StatusBar connection="reconnecting" configOk errorCount={0} />);
    expect(screen.getByTestId('pill-live')).toHaveTextContent(/RECONNECT/);
  });

  it('renders disconnected state', () => {
    render(<StatusBar connection="disconnected" configOk errorCount={0} />);
    expect(screen.getByTestId('pill-live')).toHaveTextContent(/OFFLINE/);
  });
});
