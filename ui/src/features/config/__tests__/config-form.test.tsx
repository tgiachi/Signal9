import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ConfigForm } from '../config-form';

const initial =
  'LogLevel = 3\nLogToFile = true\nDatabaseType = 0\nDatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db"\n[Jwt]\nIssuer = "SignalNine"\nAudience = "SignalNine"\nSecret = "signalnine-development-secret-change-before-production"\nExpirationMinutes = 60\n[JobSystem]\nMaxConcurrentJobs = 2\nMaxLogEntriesPerJob = 500\n';

describe('ConfigForm', () => {
  it('renders fields from schema populated from TOML', async () => {
    render(<ConfigForm initialText={initial} onSave={vi.fn()} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Runtime' }));
    expect(screen.getByLabelText(/Log level/i)).toHaveValue('3');
    expect(screen.getByLabelText(/Database URL/i)).toHaveValue(
      'sqlite://{ROOT_DIRECTORY}/db/signalnine.db',
    );
  });

  it('disables Save button until a field changes', async () => {
    render(<ConfigForm initialText={initial} onSave={vi.fn()} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Job system' }));
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
    await userEvent.clear(screen.getByLabelText(/Max concurrent/i));
    await userEvent.type(screen.getByLabelText(/Max concurrent/i), '4');
    expect(screen.getByRole('button', { name: /save/i })).toBeEnabled();
  });

  it('calls onSave with stringified TOML reflecting edits', async () => {
    const onSave = vi.fn();
    render(<ConfigForm initialText={initial} onSave={onSave} isSaving={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Job system' }));
    await userEvent.clear(screen.getByLabelText(/Max concurrent/i));
    await userEvent.type(screen.getByLabelText(/Max concurrent/i), '4');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0]![0]).toMatch(/MaxConcurrentJobs = 4/);
  });
});
