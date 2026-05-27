import { useEffect, useMemo, useRef, useState } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import type { LogEntry } from './log-entry';
import { createLogsConnection } from '@/lib/signalr';

export type ConnectionState = 'connected' | 'reconnecting' | 'disconnected';

type Options = {
  maxEntries?: number;
  hubFactory?: () => HubConnection;
};

type Result = {
  connection: ConnectionState;
  entries: LogEntry[];
  errorCountLastMinute: number;
  reconnect: () => void;
};

const DEFAULT_MAX = 2000;

export function useLogStream(opts: Options = {}): Result {
  const max = opts.maxEntries ?? DEFAULT_MAX;
  const factoryRef = useRef(opts.hubFactory ?? (() => createLogsConnection()));
  const [connection, setConnection] = useState<ConnectionState>('disconnected');
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const hubRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const hub = factoryRef.current();
    hubRef.current = hub;

    hub.on('log', (entry: LogEntry) => {
      setEntries((prev) => {
        const next = prev.length >= max ? prev.slice(prev.length - max + 1) : prev.slice();
        next.push(entry);
        return next;
      });
    });

    hub.onreconnecting(() => setConnection('reconnecting'));
    hub.onreconnected(() => setConnection('connected'));
    hub.onclose(() => setConnection('disconnected'));

    hub
      .start()
      .then(() => setConnection('connected'))
      .catch(() => setConnection('disconnected'));

    return () => {
      hub.stop().catch(() => undefined);
      hubRef.current = null;
    };
  }, [max]);

  const errorCountLastMinute = useMemo(() => {
    const cutoff = Date.now() - 60_000;
    let n = 0;
    for (let i = entries.length - 1; i >= 0; i--) {
      const e = entries[i];
      if (!e) break;
      if (Date.parse(e.ts) < cutoff) break;
      if (e.level === 'error') n++;
    }
    return n;
  }, [entries]);

  const reconnect = (): void => {
    hubRef.current
      ?.start()
      .then(() => setConnection('connected'))
      .catch(() => undefined);
  };

  return { connection, entries, errorCountLastMinute, reconnect };
}
