import { useMemo, useState } from 'react';
import { SCHEMA, type FieldSpec } from './toml-schema';
import { parseToml, stringifyToml, type TomlValue } from '@/lib/toml';
import { ConfigSectionNav } from './config-section-nav';

type Props = {
  initialText: string;
  onSave: (text: string) => void;
  isSaving: boolean;
};

type ConfigDocument = Record<string, TomlValue>;

function readInitial(text: string): ConfigDocument {
  try {
    return parseToml(text);
  } catch {
    return {};
  }
}

function emptyForField(f: FieldSpec): TomlValue {
  switch (f.type) {
    case 'number':
      return 0;
    case 'boolean':
      return false;
    default:
      return '';
  }
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

export function ConfigForm({ initialText, onSave, isSaving }: Props) {
  const initial = useMemo(() => readInitial(initialText), [initialText]);
  const [data, setData] = useState<ConfigDocument>(() => structuredClone(initial));
  const firstSection = SCHEMA[0];
  if (!firstSection) throw new Error('SCHEMA must contain at least one section');
  const [activeKey, setActiveKey] = useState<string>(firstSection.key);
  const dirty = useMemo(
    () => stringifyToml(data) !== stringifyToml(initial),
    [data, initial],
  );

  const section = SCHEMA.find((s) => s.key === activeKey) ?? firstSection;

  const update = (path: readonly string[], value: TomlValue) => {
    setData((prev) => setValue(prev, path, value));
  };

  return (
    <div className="flex h-full flex-col">
      <div className="flex min-h-0 flex-1">
        <ConfigSectionNav activeKey={activeKey} onChange={setActiveKey} />
        <div className="flex-1 overflow-auto p-5">
          <h2 className="mb-4 font-mono text-[10px] uppercase tracking-label text-fg-3">
            {section.label}
          </h2>
          <div className="flex flex-col gap-4">
            {section.fields.map((f) => (
              <FieldRow
                key={f.path.join('.')}
                spec={f}
                value={getValue(data, f.path) ?? emptyForField(f)}
                onChange={(v) => update(f.path, v)}
              />
            ))}
          </div>
        </div>
      </div>
      <footer className="flex items-center justify-end gap-2 bg-bg-4 px-3 py-2">
        <button
          type="button"
          onClick={() => setData(structuredClone(initial))}
          disabled={!dirty || isSaving}
          className="rounded-[6px] bg-bg-2 px-3 py-1.5 text-[11px] text-fg-1 transition hover:bg-[#343b41] disabled:opacity-40"
        >
          Discard
        </button>
        <button
          type="button"
          onClick={() => onSave(stringifyToml(data))}
          disabled={!dirty || isSaving}
          className="rounded-[6px] bg-accent-live px-3 py-1.5 text-[11px] font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
        >
          {isSaving ? 'Saving…' : 'Save & Reload'}
        </button>
      </footer>
    </div>
  );
}

function FieldRow({
  spec,
  value,
  onChange,
}: {
  spec: FieldSpec;
  value: TomlValue;
  onChange: (next: TomlValue) => void;
}) {
  const id = `cfg-${spec.path.join('-')}`;
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="font-mono text-[10px] uppercase tracking-label text-fg-3">
        {spec.label}
      </label>
      {spec.type === 'select' && spec.options && (
        <select
          id={id}
          value={String(value)}
          onChange={(e) => {
            const option = spec.options?.find((item) => String(item.value) === e.target.value);
            onChange(option?.value ?? e.target.value);
          }}
          className="w-full max-w-[28rem] rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        >
          {spec.options.map((o) => (
            <option key={String(o.value)} value={String(o.value)}>
              {o.label}
            </option>
          ))}
        </select>
      )}
      {spec.type === 'text' && (
        <input
          id={id}
          value={String(value)}
          onChange={(e) => onChange(e.target.value)}
          className="w-full max-w-[28rem] rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        />
      )}
      {spec.type === 'number' && (
        <input
          id={id}
          type="number"
          value={Number(value)}
          onChange={(e) => onChange(Number(e.target.value))}
          className="w-36 rounded-[6px] bg-bg-1 px-2 py-1.5 font-mono text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        />
      )}
      {spec.type === 'boolean' && (
        <input
          id={id}
          type="checkbox"
          checked={Boolean(value)}
          onChange={(e) => onChange(e.target.checked)}
          className="h-4 w-4 accent-accent-live"
        />
      )}
      {spec.help && <p className="font-mono text-[10px] text-fg-3">{spec.help}</p>}
    </div>
  );
}
