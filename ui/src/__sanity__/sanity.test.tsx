import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

describe('sanity', () => {
  it('renders text', () => {
    render(<p>hello</p>);
    expect(screen.getByText('hello')).toBeInTheDocument();
  });
});
