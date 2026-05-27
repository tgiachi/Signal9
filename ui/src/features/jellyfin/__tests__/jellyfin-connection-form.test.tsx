import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { JellyfinConnectionForm } from '../jellyfin-connection-form';
import type { JellyfinConnectionStatus } from '../jellyfin-types';

const CONFIGURED_STATUS: JellyfinConnectionStatus = {
  isConfigured: true,
  baseUrl: 'http://jellyfin.local:8096',
  lastVerifiedAt: '2026-05-27T10:00:00Z',
};

describe('JellyfinConnectionForm', () => {
  it('tracks dirty edits, validates the API key, and submits connection values', async () => {
    const submit = vi.fn(async () => undefined);
    render(
      <JellyfinConnectionForm
        status={{ isConfigured: false, baseUrl: null, lastVerifiedAt: null }}
        isSaving={false}
        onSubmit={submit}
      />,
    );

    await userEvent.type(screen.getByLabelText('Base URL'), 'http://jellyfin.local:8096');
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('API key is required');

    await userEvent.type(screen.getByLabelText('API key'), 'secret-key');
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() =>
      expect(submit).toHaveBeenCalledWith({
        baseUrl: 'http://jellyfin.local:8096',
        apiKey: 'secret-key',
      }),
    );
  });

  it('never echoes an existing API key in the password input', () => {
    render(
      <JellyfinConnectionForm
        status={CONFIGURED_STATUS}
        isSaving={false}
        onSubmit={async () => undefined}
      />,
    );

    expect(screen.getByLabelText('Base URL')).toHaveValue('http://jellyfin.local:8096');
    expect(screen.getByLabelText('API key')).toHaveValue('');
  });
});
