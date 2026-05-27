import { useMemo, useState } from 'react';
import Editor from '@monaco-editor/react';
import { safeParseToml } from '@/lib/toml';

type Props = {
  initialText: string;
  onSave: (text: string) => void;
  isSaving: boolean;
};

export function ConfigRawEditor({ initialText, onSave, isSaving }: Props) {
  const [text, setText] = useState(initialText);
  const parsed = useMemo(() => safeParseToml(text), [text]);
  const dirty = text !== initialText;

  return (
    <div className="flex h-full flex-col">
      <div className="min-h-0 flex-1">
        <Editor
          height="100%"
          defaultLanguage="ini"
          value={text}
          theme="vs-dark"
          options={{
            fontFamily: 'JetBrains Mono, ui-monospace, monospace',
            fontSize: 13,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            tabSize: 2,
          }}
          onChange={(v) => setText(v ?? '')}
        />
      </div>
      <div className="bg-bg-1 px-3 py-1.5 font-mono text-[10px]">
        {parsed.ok ? (
          <span className="text-fg-3">{dirty ? 'modified · valid TOML' : 'unchanged'}</span>
        ) : (
          <span className="text-accent-err">
            Parse error{parsed.error.line !== undefined ? ` (line ${parsed.error.line})` : ''}:{' '}
            {parsed.error.message}
          </span>
        )}
      </div>
      <footer className="flex items-center justify-end gap-2 bg-bg-4 px-3 py-2">
        <button
          type="button"
          onClick={() => setText(initialText)}
          disabled={!dirty || isSaving}
          className="rounded-[6px] bg-bg-2 px-3 py-1.5 text-[11px] text-fg-1 transition hover:bg-[#343b41] disabled:opacity-40"
        >
          Discard
        </button>
        <button
          type="button"
          onClick={() => onSave(text)}
          disabled={!dirty || isSaving || !parsed.ok}
          className="rounded-[6px] bg-accent-live px-3 py-1.5 text-[11px] font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
        >
          {isSaving ? 'Saving…' : 'Save & Reload'}
        </button>
      </footer>
    </div>
  );
}
