import { useMemo, useState } from 'react';
import { FolderOpen, Save } from 'lucide-react';
import { DirectoryPicker } from '@/components/ui/directory-picker';
import { Button } from '@/components/ui/button';
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
  const [pickerOpen, setPickerOpen] = useState(false);
  const isLocalFile = form.sourceType === 1;
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
    <div className="flex max-h-[calc(100vh-2rem)] min-h-0 flex-col overflow-hidden bg-bg-2">
      <div className="bg-bg-0 py-2 pl-3 pr-12">
        <div className="text-sm font-semibold text-fg-0">
          {props.mode === 'edit' ? 'Edit Media Library' : 'Create Media Library'}
        </div>
        <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">
          {isDirty ? 'Unsaved changes' : 'No pending changes'}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-auto p-3">
        {error && (
          <div
            role="alert"
            className="mb-3 rounded-[6px] bg-accent-err px-3 py-2 text-[12px] text-fg-0"
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
                defaultMediaType: Number(
                  defaultMediaType,
                ) as MediaLibraryFormValues['defaultMediaType'],
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
          <div className="block">
            <label
              htmlFor="media-library-source-ref"
              className="font-mono text-[10px] uppercase tracking-label text-fg-3"
            >
              Source reference
            </label>
            <div className="mt-1 flex gap-2">
              <input
                id="media-library-source-ref"
                value={form.sourceRef}
                onChange={(event) =>
                  setForm((current) => ({ ...current, sourceRef: event.target.value }))
                }
                className="w-full rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
              />
              {isLocalFile && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label="Browse server filesystem"
                  onClick={() => setPickerOpen(true)}
                >
                  <FolderOpen />
                  Browse…
                </Button>
              )}
            </div>
          </div>
          <label className="block md:col-span-2">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              Description
            </span>
            <textarea
              value={form.description}
              onChange={(event) =>
                setForm((current) => ({ ...current, description: event.target.value }))
              }
              rows={3}
              className="mt-1 w-full resize-none rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
            />
          </label>
          {props.mode === 'edit' && (
            <label className="flex items-center gap-2 rounded-[6px] bg-bg-1 px-3 py-2 text-[12px] text-fg-1 md:col-span-2">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) =>
                  setForm((current) => ({ ...current, isActive: event.target.checked }))
                }
                className="size-4 accent-accent-live"
              />
              Active library
            </label>
          )}
        </div>
      </div>

      <footer className="flex justify-end bg-bg-0 p-3">
        <button
          type="button"
          onClick={() => {
            void submit();
          }}
          disabled={props.isSaving || (props.mode === 'edit' && !isDirty)}
          className="inline-flex items-center justify-center gap-2 rounded-[6px] bg-accent-live px-3 py-2 text-sm font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
        >
          <Save className="size-4" />
          {props.isSaving ? 'Saving' : props.mode === 'edit' ? 'Save' : 'Create'}
        </button>
      </footer>
      <DirectoryPicker
        open={pickerOpen}
        initialPath={form.sourceRef || '/'}
        onOpenChange={setPickerOpen}
        onSelect={(picked) => setForm((current) => ({ ...current, sourceRef: picked }))}
      />
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
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
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
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-[6px] bg-bg-1 px-2.5 py-2 text-[12px] text-fg-1 outline-none transition focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
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
