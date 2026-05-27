import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Pill } from '../pill';

describe('Pill', () => {
  it('renders the label text', () => {
    render(<Pill variant="live">ON AIR</Pill>);
    expect(screen.getByText('ON AIR')).toBeInTheDocument();
  });

  it('applies the live variant class', () => {
    render(<Pill variant="live">ON AIR</Pill>);
    expect(screen.getByText('ON AIR')).toHaveClass('bg-accent-live');
  });

  it('applies the jobs variant class', () => {
    render(<Pill variant="jobs">JOBS</Pill>);
    expect(screen.getByText('JOBS')).toHaveClass('bg-accent-jobs');
  });

  it('renders a leading dot when dot=true', () => {
    render(
      <Pill variant="live" dot>
        ON AIR
      </Pill>,
    );
    expect(screen.getByTestId('pill-dot')).toBeInTheDocument();
  });

  it('forwards data-testid', () => {
    render(
      <Pill variant="health" data-testid="pill-health">
        /health OK
      </Pill>,
    );
    expect(screen.getByTestId('pill-health')).toBeInTheDocument();
  });
});
