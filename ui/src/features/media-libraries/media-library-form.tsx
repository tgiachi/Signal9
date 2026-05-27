import { useMemo, useState } from 'react';
import { Save } from 'lucide-react';
import {
  CHANNEL_MEDIA_TYPE_OPTIONS,
  MEDIA_SOURCE_TYPE_OPTIONS,
  formToCreateRequest,
  formToUpdateRequest,
  mergeMediaLibraryForm,
  type CreateMediaLibraryRequest,
  type MediaLibraryFormValues,
  type UpdateMediaLibraryRequest,
} from './media-library-types';

type Props =
  | {
      mode: 'create';
      initialValue?: Partial<MediaLibraryFormValues>;
      isSaving: boolean;
      onSubmit: (value: CreateMediaLibraryRequest) => Promise<void> | void;
    }
  | {
      mode: 'edit';
      initialValue: MediaLibraryFormValues;
      isSaving: boolean;
      onSubmit: (value: UpdateMediaLibraryRequest) => Promise<void> | void;
    };

export function MediaLibraryForm(props: Props) {
  const initial = useMemo(
    () => mergeMediaLibraryForm(props.initialValue),
    [props.initialValue],
  );
  const [form, setForm] = useState<MediaLibraryFormValues>(initial);
  const [error, setError] = useState<string | null>(null);
  const isDirty = !sameForm(initial, form);

  const submit = async () => {
    const validation = validateForm(form);
    if (validation) {
      setError(validation);
      return;
    }

    setError(null);
    if (props.mode === 'edit') {
      await props.onSubmit(formToUpdateRequest(form));
      return;
    }

    await props.onSubmit(formToCreateRequest(form));
  };

  return (
    <div className="flex max-h-[calc(100vh-2rem)] min-h-0 flex-col overflow-hidden bg-panel">
      <div className="border-b border-border-subtle bg-panel-strong py-2 pl-3 pr-12">
        <div className="text-sm font-semibold text-fg-0">
          {props.mode === 'edit' ? 'Edit Media Library' : 'Create Media Library'}
        </div>
        <div className="font-mono text-[10px] uppercase tracking-label text-fg-2">
          {isDirty ? 'Unsaved changes' : 'No pending changes'}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-auto p-3">
        {error && (
          <div
            role="alert"
            className="mb-3 rounded-md border border-error/40 bg-error-bg/50 px-3 py-2 text-[12px] text-error"
          >
            {error}
          </div>
        )}

        <div className="grid gap-3 md:grid-cols-2">
          <TextField
            label="Name"
            value={form.name}
            onChange={(name) => setForm((current) => ({ ...current, name }))}
          />
          <SelectField
            label="Default media type"
            value={String(form.defaultMediaType)}
            options={CHANNEL_MEDIA_TYPE_OPTIONS.map((item) => ({
              value: String(item.value),
              label: item.label,
            }))}
            onChange={(defaultMediaType) =>
              setForm((current) => ({
                ...current,
                defaultMediaType: Number(defaultMediaType) as MediaLibraryFormValues['defaultMediaType'],
              }))
            }
          />
          <SelectField
            label="Source type"
            value={String(form.sourceType)}
            options={MEDIA_SOURCE_TYPE_OPTIONS.map((item) => ({
              value: String(item.value),
              label: item.label,
            }))}
            onChange={(sourceType) =>
              setForm((current) => ({
                ...current,
                sourceType: Number(sourceType) as MediaLibraryFormValues['sourceType'],
              }))
            }
          />
          <TextField
            label="Source reference"
            value={form.sourceRef}
            onChange={(sourceRef) => setForm((current) => ({ ...current, sourceRef }))}
          />
          <label className="block md:col-span-2">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">
              Description
            </span>
            <textarea
              value={form.description}
              onChange={(event) =>
                setForm((current) => ({ ...current, description: event.target.value }))
              }
              rows={3}
              className="mt-1 w-full resize-none rounded-md border border-border bg-bg-1 px-2.5 py-2 text-[12px] text-fg-0 outline-none transition placeholder:text-fg-2 focus:border-on-air"
            />
          </label>
          {props.mode === 'edit' && (
            <label className="flex items-center gap-2 rounded-md border border-border-subtle bg-bg-1 px-3 py-2 text-[12px] text-fg-0 md:col-span-2">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) =>
                  setForm((current) => ({ ...current, isActive: event.target.checked }))
                }
                className="size-4"
              />
              Active library
            </label>
          )}
        </div>
      </div>

      <footer className="flex justify-end border-t border-border-subtle bg-panel-strong p-3">
        <button
          type="button"
          onClick={() => {
            void submit();
          }}
          disabled={props.isSaving || (props.mode === 'edit' && !isDirty)}
          className="inline-flex items-center justify-center gap-2 rounded-md border border-on-air/50 bg-on-air/15 px-3 py-2 text-sm font-semibold text-on-air-2 transition hover:bg-on-air/20 disabled:opacity-40"
        >
          <Save className="size-4" />
          {props.isSaving ? 'Saving' : props.mode === 'edit' ? 'Save' : 'Create'}
        </button>
      </footer>
    </div>
  );
}

function TextField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-md border border-border bg-bg-1 px-2.5 py-2 text-[12px] text-fg-0 outline-none transition placeholder:text-fg-2 focus:border-on-air"
      />
    </label>
  );
}

function SelectField({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: Array<{ value: string; label: string }>;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-md border border-border bg-bg-1 px-2.5 py-2 text-[12px] text-fg-0 outline-none transition focus:border-on-air"
      >
        {options.map((item) => (
          <option key={item.value} value={item.value}>
            {item.label}
          </option>
        ))}
      </select>
    </label>
  );
}

function validateForm(form: MediaLibraryFormValues): string | null {
  if (!form.name.trim()) return 'Name is required.';
  if (!form.sourceRef.trim()) return 'Source reference is required.';
  return null;
}

function sameForm(left: MediaLibraryFormValues, right: MediaLibraryFormValues): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}
