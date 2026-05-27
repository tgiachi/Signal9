import { useMemo, useState } from 'react';
import { LogStream } from './log-stream';
import { LogFilterBar, type LevelFilter } from './log-filter-bar';
import { useLogStreamContext } from './log-stream-ctx';

export function LogsPage() {
  const { entries, connection, reconnect } = useLogStreamContext();
  const [level, setLevel] = useState<LevelFilter>('all');
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    const lower = search.toLowerCase();
    return entries.filter((e) => {
      if (level !== 'all' && e.level !== level) return false;
      if (
        lower &&
        !e.message.toLowerCase().includes(lower) &&
        !e.source.toLowerCase().includes(lower)
      )
        return false;
      return true;
    });
  }, [entries, level, search]);

  return (
    <div className="flex h-full min-h-0 flex-col p-3">
      <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-lg border border-border bg-panel">
        <header className="flex items-center gap-3 border-b border-border-subtle bg-panel-strong px-3 py-2">
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Live Log Stream</h1>
            <p className="font-mono text-[10px] text-fg-2">
              {filtered.length} visible / {entries.length} retained
            </p>
          </div>
          <span className="ml-auto rounded border border-border bg-bg-1 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-1">
            {connection}
          </span>
        </header>
        <LogFilterBar
          level={level}
          search={search}
          onLevelChange={setLevel}
          onSearchChange={setSearch}
        />
        {connection === 'reconnecting' && (
          <div className="border-b border-warn/40 bg-warn/10 px-3 py-1 text-[11px] text-warn">
            Reconnecting…
          </div>
        )}
        {connection === 'disconnected' && (
          <div className="flex items-center gap-3 border-b border-error/40 bg-error-bg/30 px-3 py-1 text-[11px] text-error">
            <span>Disconnected from log hub.</span>
            <button
              type="button"
              onClick={reconnect}
              className="rounded border border-error/60 px-2 py-0.5 text-error hover:bg-error-bg"
            >
              Retry now
            </button>
          </div>
        )}
        <div className="min-h-0 flex-1">
          <LogStream entries={filtered} />
        </div>
        <footer className="flex items-center justify-between border-t border-border-subtle bg-panel-strong px-3 py-1 font-mono text-[10px] text-fg-2">
          <span>{entries.length >= 2000 ? '2000+' : entries.length} entries</span>
          <span>SignalR logs hub</span>
        </footer>
      </section>
    </div>
  );
}
