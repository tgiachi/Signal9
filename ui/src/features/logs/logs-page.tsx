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
      <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex items-center gap-3 bg-bg-4 px-3 py-2">
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Live Log Stream</h1>
            <p className="font-mono text-[10px] text-fg-3">
              {filtered.length} visible / {entries.length} retained
            </p>
          </div>
          <span className="ml-auto rounded-[3px] bg-bg-2 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-2">
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
          <div className="bg-accent-warn px-3 py-1 text-[11px] text-bg-0">
            Reconnecting…
          </div>
        )}
        {connection === 'disconnected' && (
          <div className="flex items-center gap-3 bg-accent-err px-3 py-1 text-[11px] text-fg-0">
            <span>Disconnected from log hub.</span>
            <button
              type="button"
              onClick={reconnect}
              className="rounded-[3px] bg-bg-5 px-2 py-0.5 text-fg-0 hover:opacity-90"
            >
              Retry now
            </button>
          </div>
        )}
        <div className="min-h-0 flex-1">
          <LogStream entries={filtered} />
        </div>
        <footer className="flex items-center justify-between bg-bg-4 px-3 py-1 font-mono text-[10px] text-fg-3">
          <span>{entries.length >= 2000 ? '2000+' : entries.length} entries</span>
          <span>SignalR logs hub</span>
        </footer>
      </section>
    </div>
  );
}
