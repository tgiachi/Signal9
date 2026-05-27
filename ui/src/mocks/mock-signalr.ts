import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { generateEntry } from './fixtures/log-generator';

export function createMockLogsConnection(): HubConnection {
  let onLog: ((e: unknown) => void) | null = null;
  let onClose: ((e?: Error) => void) | null = null;
  let timer: ReturnType<typeof setInterval> | null = null;
  let state: HubConnectionState = HubConnectionState.Disconnected;

  const start = async () => {
    state = HubConnectionState.Connected;
    timer = setInterval(() => onLog?.(generateEntry()), 200);
  };
  const stop = async () => {
    if (timer) clearInterval(timer);
    timer = null;
    state = HubConnectionState.Disconnected;
    onClose?.();
  };

  const hub: Partial<HubConnection> = {
    start,
    stop,
    on: (event: string, handler: (...args: unknown[]) => void) => {
      if (event === 'log') onLog = handler as (e: unknown) => void;
    },
    off: (event: string) => {
      if (event === 'log') onLog = null;
    },
    onclose: (cb: (e?: Error) => void) => {
      onClose = cb;
    },
    onreconnecting: () => undefined,
    onreconnected: () => undefined,
    get state() {
      return state;
    },
  };
  return hub as HubConnection;
}
