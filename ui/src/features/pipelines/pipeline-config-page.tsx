import { useMemo, useState } from 'react';
import { RefreshCw, RotateCcw, Save, Workflow } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/lib/api';
import { parseToml, stringifyToml, type TomlValue } from '@/lib/toml';
import { useConfig } from '@/features/config/use-config';
import { useConfigSchema } from '@/features/config/use-config-schema';
import { schemaToSections } from '@/features/config/schema-to-sections';
import { FALLBACK_SCHEMA, type FieldSpec, type SectionSpec } from '@/features/config/toml-schema';

type ConfigDocument = Record<string, TomlValue>;

type TaskGroup = {
  key: string;
  label: string;
  fields: FieldSpec[];
  enabled: boolean;
};

export function PipelineConfigPage() {
  const cfg = useConfig();
  const schema = useConfigSchema();
  const [rawText, setRawText] = useState<string | null>(null);

  const sections = useMemo(
    () => (schema.data ? schemaToSections(schema.data) : FALLBACK_SCHEMA),
    [schema.data],
  );
  const pipelineSection =
    sections.find((section) => section.key === 'pipeline') ??
    FALLBACK_SCHEMA.find((section) => section.key === 'pipeline');

  if (cfg.isLoading || schema.isLoading) {
    return <div className="p-4 text-fg-1">Loading pipeline configuration...</div>;
  }

  if (cfg.isError) {
    return <div className="p-4 text-accent-err">Failed to load pipeline configuration.</div>;
  }

  if (!pipelineSection) {
    return <div className="p-4 text-accent-err">Pipeline schema is not available.</div>;
  }

  const baseText = rawText ?? cfg.text ?? '';

  return (
    <PipelineConfigEditor
      key={baseText}
      initialText={baseText}
      section={pipelineSection}
      isSaving={cfg.isSaving}
      schemaStatus={schema.isError ? 'fallback' : 'schema'}
      onRefresh={() => {
        setRawText(null);
        void cfg.refresh();
        void schema.refetch();
      }}
      onSave={async (text) => {
        try {
          await cfg.save(text);
          setRawText(text);
          toast.success('Pipeline configuration saved');
        } catch (error) {
          toast.error(errorMessage(error));
        }
      }}
    />
  );
}

function PipelineConfigEditor({
  initialText,
  section,
  isSaving,
  schemaStatus,
  onRefresh,
  onSave,
}: {
  initialText: string;
  section: SectionSpec;
  isSaving: boolean;
  schemaStatus: 'schema' | 'fallback';
  onRefresh: () => void;
  onSave: (text: string) => Promise<void>;
}) {
  const initial = useMemo(() => readInitial(initialText), [initialText]);
  const [data, setData] = useState<ConfigDocument>(() => structuredClone(initial));
  const groups = useMemo(() => groupPipelineFields(section.fields, data), [data, section.fields]);
  const dirty = useMemo(
    () => stringifyToml(data) !== stringifyToml(initial),
    [data, initial],
  );
  const enabledCount = groups.filter((group) => group.enabled).length;

  const update = (field: FieldSpec, value: TomlValue) => {
    setData((prev) => setValue(prev, field.path, value));
  };

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3">
      <div className="grid gap-3 md:grid-cols-3">
        <SummaryMetric label="Tasks" value={String(groups.length)} />
        <SummaryMetric label="Enabled" value={String(enabledCount)} />
        <SummaryMetric label="Schema" value={schemaStatus} />
      </div>

      <section className="flex min-h-[30rem] flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex flex-wrap items-center gap-3 bg-bg-4 px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-[4px] bg-accent-jobs text-fg-0">
            <Workflow className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Media pipeline</h1>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              {enabledCount}/{groups.length} enabled
            </p>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={onRefresh}
              disabled={isSaving}
              className="inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0 disabled:opacity-40"
            >
              <RefreshCw className="size-3.5" />
              Refresh
            </button>
            <button
              type="button"
              onClick={() => setData(structuredClone(initial))}
              disabled={!dirty || isSaving}
              className="inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0 disabled:opacity-40"
            >
              <RotateCcw className="size-3.5" />
              Discard
            </button>
            <button
              type="button"
              onClick={() => void onSave(stringifyToml(data))}
              disabled={!dirty || isSaving}
              className="inline-flex items-center gap-2 rounded-[6px] bg-accent-live px-2.5 py-1.5 text-[12px] font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
            >
              <Save className="size-3.5" />
              {isSaving ? 'Saving' : 'Save'}
            </button>
          </div>
        </header>

        <div className="min-h-0 flex-1 overflow-auto bg-bg-0 p-3">
          {groups.length === 0 ? (
            <EmptyState />
          ) : (
            <div className="grid gap-3 xl:grid-cols-2">
              {groups.map((group) => (
                <TaskPanel key={group.key} group={group} data={data} onChange={update} />
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

function TaskPanel({
  group,
  data,
  onChange,
}: {
  group: TaskGroup;
  data: ConfigDocument;
  onChange: (field: FieldSpec, value: TomlValue) => void;
}) {
  return (
    <section className="overflow-hidden rounded-[6px] bg-bg-2">
      <header className="flex items-center justify-between gap-3 bg-bg-4 px-3 py-2">
        <div className="min-w-0">
          <h2 className="text-sm font-semibold text-fg-0">{group.label}</h2>
          <p className="font-mono text-[10px] uppercase tracking-label text-fg-3">
            {group.key}
          </p>
        </div>
        <span
          className={
            'rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-label ' +
            (group.enabled ? 'bg-accent-live text-bg-5' : 'bg-bg-1 text-fg-3')
          }
        >
          {group.enabled ? 'enabled' : 'disabled'}
        </span>
      </header>

      <div className="grid gap-4 p-3 md:grid-cols-2">
        {group.fields.map((field) => (
          <PipelineField
            key={field.path.join('.')}
            field={field}
            value={getValue(data, field.path) ?? emptyForField(field)}
            onChange={(value) => onChange(field, value)}
          />
        ))}
      </div>
    </section>
  );
}

function PipelineField({
  field,
  value,
  onChange,
}: {
  field: FieldSpec;
  value: TomlValue;
  onChange: (value: TomlValue) => void;
}) {
  const id = `pipeline-${field.path.join('-')}`;

  return (
    <label htmlFor={id} className="flex min-w-0 flex-col gap-1">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
        {field.label}
      </span>
      {field.type === 'boolean' && (
        <input
          id={id}
          type="checkbox"
          checked={Boolean(value)}
          onChange={(event) => onChange(event.target.checked)}
          className="h-4 w-4 accent-accent-live"
        />
      )}
      {field.type === 'number' && (
        <input
          id={id}
          type="number"
          value={Number(value)}
          min={field.min}
          max={field.max}
          onChange={(event) => onChange(Number(event.target.value))}
          className="w-36 rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        />
      )}
      {field.type === 'text' && (
        <input
          id={id}
          type={field.secret ? 'password' : 'text'}
          value={String(value)}
          onChange={(event) => onChange(event.target.value)}
          className="w-full rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        />
      )}
      {field.type === 'select' && field.options && (
        <select
          id={id}
          value={String(value)}
          onChange={(event) => {
            const option = field.options?.find((item) => String(item.value) === event.target.value);
            onChange(option?.value ?? event.target.value);
          }}
          className="w-full rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        >
          {field.options.map((option) => (
            <option key={String(option.value)} value={String(option.value)}>
              {option.label}
            </option>
          ))}
        </select>
      )}
      {field.help && <span className="font-mono text-[10px] text-fg-3">{field.help}</span>}
    </label>
  );
}

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <section className="min-w-0 rounded-[6px] bg-bg-2 p-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-lg font-semibold text-fg-0">{value}</div>
    </section>
  );
}

function EmptyState() {
  return <div className="flex h-56 items-center justify-center text-sm text-fg-3">No tasks.</div>;
}

function readInitial(text: string): ConfigDocument {
  try {
    return parseToml(text);
  } catch {
    return {};
  }
}

function emptyForField(field: FieldSpec): TomlValue {
  if (field.defaultValue !== undefined) return field.defaultValue;

  switch (field.type) {
    case 'number':
      return 0;
    case 'boolean':
      return false;
    default:
      return '';
  }
}

function groupPipelineFields(fields: readonly FieldSpec[], data: ConfigDocument): TaskGroup[] {
  const groups = new Map<string, FieldSpec[]>();

  for (const field of fields) {
    const taskName = field.path[0] === 'Pipeline' && field.path[1] === 'Tasks'
      ? field.path[2] ?? 'Pipeline'
      : 'Pipeline';
    groups.set(taskName, [...(groups.get(taskName) ?? []), field]);
  }

  return Array.from(groups.entries()).map(([key, groupFields]) => {
    const enabledField = groupFields.find((field) => field.path[field.path.length - 1] === 'Enabled');
    const enabled = enabledField
      ? Boolean(getValue(data, enabledField.path) ?? emptyForField(enabledField))
      : true;

    return {
      key,
      label: labelFromKey(key),
      fields: groupFields,
      enabled,
    };
  });
}

function labelFromKey(key: string): string {
  return key.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function getValue(data: ConfigDocument, path: readonly string[]): TomlValue | undefined {
  let current: TomlValue | undefined = data;
  for (const segment of path) {
    if (
      !current ||
      typeof current !== 'object' ||
      current instanceof Date ||
      Array.isArray(current)
    ) {
      return undefined;
    }
    current = current[segment];
  }
  return current;
}

function setValue(
  data: ConfigDocument,
  path: readonly string[],
  value: TomlValue,
): ConfigDocument {
  const [head, ...rest] = path;
  if (!head) return data;

  if (rest.length === 0) return { ...data, [head]: value };

  const current = data[head];
  const branch =
    current && typeof current === 'object' && !(current instanceof Date) && !Array.isArray(current)
      ? (current as ConfigDocument)
      : {};

  return {
    ...data,
    [head]: setValue(branch, rest, value),
  };
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError && typeof error.body === 'string' && error.body.trim()) {
    return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'Pipeline configuration save failed.';
}
