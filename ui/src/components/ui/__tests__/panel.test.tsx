import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Panel } from '../panel';

describe('Panel', () => {
  it('renders the title and children', () => {
    render(
      <Panel title="Active channels">
        <div>row</div>
      </Panel>,
    );
    expect(screen.getByText('Active channels')).toBeInTheDocument();
    expect(screen.getByText('row')).toBeInTheDocument();
  });

  it('renders the optional counter', () => {
    render(
      <Panel title="Recent jobs" counter={4}>
        <div />
      </Panel>,
    );
    expect(screen.getByText('4')).toBeInTheDocument();
  });

  it('renders a custom action node in the header', () => {
    render(
      <Panel title="x" action={<button>act</button>}>
        <div />
      </Panel>,
    );
    expect(screen.getByRole('button', { name: 'act' })).toBeInTheDocument();
  });
});
