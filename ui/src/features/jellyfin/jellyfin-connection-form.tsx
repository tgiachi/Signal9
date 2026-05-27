import { useRef, useState, type FormEvent } from 'react';
import { Save } from 'lucide-react';
import type { JellyfinConnectionInput, JellyfinConnectionStatus } from './jellyfin-types';

type Props = {
  status: JellyfinConnectionStatus;
  isSaving: boolean;
  onSubmit: (input: JellyfinConnectionInput) => Promise<void> | void;
};

export function JellyfinConnectionForm({ status, isSaving, onSubmit }: Props) {
  const formRef = useRef<HTMLFormElement>(null);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const baseUrl = String(form.get('baseUrl') ?? '').trim();
    const apiKey = String(form.get('apiKey') ?? '').trim();

    if (!baseUrl) {
      setError('Base URL is required.');
      return;
    }

    if (!apiKey) {
      setError('API key is required.');
      return;
    }

    setError(null);
    await onSubmit({ baseUrl, apiKey });
    const apiKeyInput = formRef.current?.elements.namedItem('apiKey');
    if (apiKeyInput instanceof HTMLInputElement) apiKeyInput.value = '';
    setDirty(false);
  };

  return (
    <form
      ref={formRef}
      className="grid gap-3"
      onInput={() => setDirty(true)}
      onSubmit={(event) => {
        void submit(event);
      }}
    >
      {error && (
        <div
          role="alert"
          className="rounded-[6px] bg-accent-err px-3 py-2 text-[12px] text-fg-0"
        >
          {error}
        </div>
      )}

      <div className="grid gap-3 md:grid-cols-2">
        <label className="block">
          <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
            Base URL
          </span>
          <input
            key={status.baseUrl ?? 'empty-url'}
            name="baseUrl"
            defaultValue={status.baseUrl ?? ''}
            placeholder="http://jellyfin.local:8096"
            className="mt-1 w-full rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
          />
        </label>
        <label className="block">
          <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
            API key
          </span>
          <input
            name="apiKey"
            type="password"
            autoComplete="new-password"
            placeholder={status.isConfigured ? 'Paste a new API key to replace it' : 'API key'}
            className="mt-1 w-full rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
          />
        </label>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">
          {dirty ? 'Unsaved changes' : 'No pending changes'}
        </div>
        <button
          type="submit"
          disabled={isSaving || !dirty}
          className="inline-flex items-center justify-center gap-2 rounded-[6px] bg-accent-live px-3 py-2 text-sm font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
        >
          <Save className="size-4" />
          {isSaving ? 'Saving' : 'Save'}
        </button>
      </div>
    </form>
  );
}
