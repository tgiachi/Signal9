import {
  Ban,
  BadgeCheck,
  Image as ImageIcon,
  Plus,
  RadioTower,
  Save,
  Trash2,
  Upload,
} from 'lucide-react';
import type { ChannelFormValues } from './channel-types';
import { cn } from '@/lib/cn';

type Props = {
  value: ChannelFormValues;
  isSaving: boolean;
  isDeleting: boolean;
  isUploadingLogo: boolean;
  validationError: string | null;
  onChange: (value: ChannelFormValues) => void;
  onCreateNew: () => void;
  onDelete: () => void;
  onLogoUpload: (file: File) => void;
  onSubmit: () => void;
};

export function ChannelForm({
  value,
  isSaving,
  isDeleting,
  isUploadingLogo,
  validationError,
  onChange,
  onCreateNew,
  onDelete,
  onLogoUpload,
  onSubmit,
}: Props) {
  const mode = value.id ? 'edit' : 'create';

  return (
    <div className="flex max-h-[calc(100vh-2rem)] min-h-0 flex-col overflow-hidden bg-panel">
      <header className="flex items-center gap-2 border-b border-border-subtle bg-panel-strong py-2 pl-3 pr-12">
        <RadioTower className="size-4 text-on-air-2" />
        <div className="min-w-0">
          <div className="text-sm font-semibold text-fg-0">
            {mode === 'edit' ? 'Edit Channel' : 'Create Channel'}
          </div>
          <p className="font-mono text-[10px] text-fg-2">
            {mode === 'edit' ? value.slug || value.id : 'new channel'}
          </p>
        </div>
        <button
          type="button"
          onClick={onCreateNew}
          className="ml-auto inline-flex items-center gap-2 rounded-md border border-border bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:border-on-air/40 hover:text-fg-0"
        >
          <Plus className="size-3.5" />
          New
        </button>
      </header>

      <div className="min-h-0 flex-1 overflow-auto p-3">
        {validationError && (
          <div className="mb-3 rounded-md border border-error/40 bg-error-bg/50 px-3 py-2 text-[12px] text-error">
            {validationError}
          </div>
        )}

        <div className="grid gap-3">
          <TextField
            label="Name"
            value={value.name}
            onChange={(name) => onChange({ ...value, name })}
            placeholder="Signal Nine One"
          />
          <TextField
            label="Slug"
            value={value.slug}
            onChange={(slug) => onChange({ ...value, slug })}
            placeholder="signal-nine-one"
          />
          <TextArea
            label="Description"
            value={value.description}
            onChange={(description) => onChange({ ...value, description })}
            placeholder="Schedule identity and playout notes"
          />
          <TextField
            label="Logo URL"
            value={value.logoUrl}
            onChange={(logoUrl) => onChange({ ...value, logoUrl })}
            placeholder="https://…"
          />
          <LogoUploadRow
            logoUrl={value.logoUrl}
            isUploading={isUploadingLogo}
            onUpload={onLogoUpload}
          />
          <div className="grid grid-cols-3 gap-2">
            <NumberField
              label="Order"
              value={value.displayOrder}
              min={0}
              onChange={(displayOrder) => onChange({ ...value, displayOrder })}
            />
            <NumberField
              label="Ad min"
              value={value.commercialIntervalMinSeconds}
              min={0}
              onChange={(commercialIntervalMinSeconds) =>
                onChange({ ...value, commercialIntervalMinSeconds })
              }
            />
            <NumberField
              label="Ad max"
              value={value.commercialIntervalMaxSeconds}
              min={0}
              onChange={(commercialIntervalMaxSeconds) =>
                onChange({ ...value, commercialIntervalMaxSeconds })
              }
            />
          </div>
          <ToggleRow
            label="Channel active"
            description="Visible to runtime scheduling."
            checked={value.isActive}
            disabled={mode === 'create'}
            onChange={(isActive) => onChange({ ...value, isActive })}
          />
          <ToggleRow
            label="Commercials"
            description="Allow ad breaks on this channel."
            checked={value.commercialsEnabled}
            onChange={(commercialsEnabled) => onChange({ ...value, commercialsEnabled })}
          />
        </div>
      </div>

      <footer className="flex flex-wrap items-center gap-2 border-t border-border-subtle bg-panel-strong p-3">
        <button
          type="button"
          onClick={onSubmit}
          disabled={isSaving}
          className="inline-flex flex-1 items-center justify-center gap-2 rounded-md border border-on-air/50 bg-on-air/15 px-3 py-2 text-sm font-semibold text-on-air-2 transition hover:bg-on-air/20 disabled:opacity-40"
        >
          <Save className="size-4" />
          {isSaving ? 'Saving' : mode === 'edit' ? 'Save' : 'Create'}
        </button>
        <button
          type="button"
          onClick={onDelete}
          disabled={!value.id || isDeleting}
          className="inline-flex items-center justify-center gap-2 rounded-md border border-error/40 bg-error-bg/40 px-3 py-2 text-sm font-semibold text-error transition hover:bg-error-bg disabled:opacity-40"
        >
          <Trash2 className="size-4" />
          Delete
        </button>
      </footer>
    </div>
  );
}

function LogoUploadRow({
  logoUrl,
  isUploading,
  onUpload,
}: {
  logoUrl: string;
  isUploading: boolean;
  onUpload: (file: File) => void;
}) {
  return (
    <div className="rounded-md border border-border-subtle bg-bg-1 p-3">
      <div className="flex items-center gap-3">
        <div className="flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-md border border-border bg-bg-2 text-fg-2">
          {logoUrl ? (
            <img src={logoUrl} alt="" className="size-full object-cover" />
          ) : (
            <ImageIcon className="size-5" />
          )}
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-mono text-[10px] uppercase tracking-label text-fg-2">
            Logo upload
          </div>
          <div className="truncate text-[12px] text-fg-1">
            {logoUrl || 'PNG, JPEG, or WebP up to 2 MB'}
          </div>
        </div>
        <label className="inline-flex shrink-0 items-center justify-center gap-2 rounded-md border border-on-air/40 bg-on-air/10 px-2.5 py-1.5 text-[12px] font-semibold text-on-air-2 transition hover:bg-on-air/15">
          <Upload className="size-3.5" />
          {isUploading ? 'Uploading' : 'Upload'}
          <input
            aria-label="Logo file"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            disabled={isUploading}
            className="sr-only"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) onUpload(file);
              event.currentTarget.value = '';
            }}
          />
        </label>
      </div>
    </div>
  );
}

function TextField({
  label,
  value,
  placeholder,
  onChange,
}: {
  label: string;
  value: string;
  placeholder: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="mt-1 w-full rounded-md border border-border bg-bg-1 px-2.5 py-2 text-[12px] text-fg-0 outline-none transition placeholder:text-fg-2 focus:border-on-air"
      />
    </label>
  );
}

function TextArea({
  label,
  value,
  placeholder,
  onChange,
}: {
  label: string;
  value: string;
  placeholder: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <textarea
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        rows={4}
        className="mt-1 w-full resize-none rounded-md border border-border bg-bg-1 px-2.5 py-2 text-[12px] text-fg-0 outline-none transition placeholder:text-fg-2 focus:border-on-air"
      />
    </label>
  );
}

function NumberField({
  label,
  value,
  min,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  onChange: (value: number) => void;
}) {
  return (
    <label className="block min-w-0">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <input
        type="number"
        min={min}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="mt-1 w-full rounded-md border border-border bg-bg-1 px-2 py-2 font-mono text-[12px] text-fg-0 outline-none transition focus:border-on-air"
      />
    </label>
  );
}

function ToggleRow({
  label,
  description,
  checked,
  disabled = false,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div
      className={cn(
        'flex items-center justify-between gap-3 rounded-md border border-border-subtle bg-bg-1 px-3 py-2',
        disabled && 'opacity-60',
      )}
    >
      <span className="min-w-0">
        <span className="block text-[12px] font-semibold text-fg-0">{label}</span>
        <span className="block text-[11px] text-fg-2">{description}</span>
      </span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cn(
          'inline-flex h-7 w-14 shrink-0 items-center rounded-full border px-1 transition',
          checked
            ? 'border-on-air/40 bg-on-air/15 text-on-air-2'
            : 'border-border bg-bg-2 text-fg-2',
        )}
      >
        <span
          className={cn(
            'flex size-5 items-center justify-center rounded-full bg-bg-0 transition',
            checked && 'translate-x-7',
          )}
        >
          {checked ? <BadgeCheck className="size-3" /> : <Ban className="size-3" />}
        </span>
      </button>
    </div>
  );
}
