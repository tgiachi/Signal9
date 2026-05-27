import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MediaLibraryForm } from '../media-library-form';
import type { MediaLibraryFormValues } from '../media-library-types';

const EDIT_LIBRARY: MediaLibraryFormValues = {
  id: 'library-1',
  name: 'Movies',
  description: 'Feature films',
  defaultMediaType: 3,
  sourceType: 0,
  sourceRef: 'jellyfin-movies',
  isActive: true,
};

describe('MediaLibraryForm', () => {
  it('validates required fields, tracks dirty state, and submits create values', async () => {
    const submit = vi.fn(async () => undefined);
    render(<MediaLibraryForm mode="create" isSaving={false} onSubmit={submit} />);

    await userEvent.type(screen.getByLabelText('Name'), 'Movies');
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /^create$/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Source reference is required');

    await userEvent.type(screen.getByLabelText('Source reference'), 'jellyfin-movies');
    await userEvent.selectOptions(screen.getByLabelText('Default media type'), '3');
    await userEvent.click(screen.getByRole('button', { name: /^create$/i }));

    await waitFor(() =>
      expect(submit).toHaveBeenCalledWith({
        name: 'Movies',
        description: null,
        defaultMediaType: 3,
        sourceType: 0,
        sourceRef: 'jellyfin-movies',
      }),
    );
  });

  it('submits edit values including the active flag', async () => {
    const submit = vi.fn(async () => undefined);
    render(
      <MediaLibraryForm
        mode="edit"
        initialValue={EDIT_LIBRARY}
        isSaving={false}
        onSubmit={submit}
      />,
    );

    await userEvent.click(screen.getByLabelText('Active library'));
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() =>
      expect(submit).toHaveBeenCalledWith({
        name: 'Movies',
        description: 'Feature films',
        defaultMediaType: 3,
        sourceType: 0,
        sourceRef: 'jellyfin-movies',
        isActive: false,
      }),
    );
  });
});
