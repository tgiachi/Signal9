import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ConfigRawEditor } from '../config-raw-editor';

describe('ConfigRawEditor', () => {
  it('shows the initial text', () => {
    render(<ConfigRawEditor initialText={'[a]\nx = 1'} onSave={vi.fn()} isSaving={false} />);
    expect(screen.getByTestId('monaco-editor')).toHaveValue('[a]\nx = 1');
  });

  it('disables Save when TOML is invalid and shows parse error', () => {
    render(<ConfigRawEditor initialText={'[a]\nx = 1'} onSave={vi.fn()} isSaving={false} />);
    const ta = screen.getByTestId('monaco-editor');
    fireEvent.change(ta, { target: { value: '[a' } });
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
    expect(screen.getByText(/Parse error/i)).toBeInTheDocument();
  });

  it('enables Save when TOML is valid + dirty and submits text', async () => {
    const onSave = vi.fn();
    render(<ConfigRawEditor initialText={'[a]\nx = 1'} onSave={onSave} isSaving={false} />);
    const ta = screen.getByTestId('monaco-editor');
    fireEvent.change(ta, { target: { value: '[b]\ny = 2' } });
    expect(screen.getByRole('button', { name: /save/i })).toBeEnabled();
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(onSave).toHaveBeenCalledWith('[b]\ny = 2');
  });
});
