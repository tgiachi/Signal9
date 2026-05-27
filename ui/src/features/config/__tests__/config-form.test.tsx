import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ConfigForm } from '../config-form';

const initial =
  '[logging]\nlevel = "information"\nretention_days = 14\n[jwt]\nissuer = "s9"\nexpires_min = 60\n';

describe('ConfigForm', () => {
  it('renders fields from schema populated from TOML', async () => {
    render(<ConfigForm initialText={initial} onSave={vi.fn()} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Logging' }));
    expect(screen.getByLabelText(/Level/i)).toHaveValue('information');
    expect(screen.getByLabelText(/Retention/i)).toHaveValue(14);
  });

  it('disables Save button until a field changes', async () => {
    render(<ConfigForm initialText={initial} onSave={vi.fn()} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Logging' }));
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
    await userEvent.clear(screen.getByLabelText(/Retention/i));
    await userEvent.type(screen.getByLabelText(/Retention/i), '30');
    expect(screen.getByRole('button', { name: /save/i })).toBeEnabled();
  });

  it('calls onSave with stringified TOML reflecting edits', async () => {
    const onSave = vi.fn();
    render(<ConfigForm initialText={initial} onSave={onSave} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Logging' }));
    await userEvent.clear(screen.getByLabelText(/Retention/i));
    await userEvent.type(screen.getByLabelText(/Retention/i), '30');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0]![0]).toMatch(/retention_days = 30/);
  });
});
