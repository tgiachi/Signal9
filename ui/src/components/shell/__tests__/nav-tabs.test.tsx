import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, it, expect } from 'vitest';
import { NavTabs } from '../nav-tabs';

describe('NavTabs', () => {
  it('renders logs and config tabs', () => {
    render(
      <MemoryRouter initialEntries={['/logs']}>
        <NavTabs />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Logs' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Config' })).toBeInTheDocument();
  });

  it('marks the active tab', () => {
    render(
      <MemoryRouter initialEntries={['/config']}>
        <NavTabs />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Config' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Logs' })).not.toHaveAttribute('aria-current');
  });
});
