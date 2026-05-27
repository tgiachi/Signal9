import { HubConnectionState } from '@microsoft/signalr';
import type { LogEntry } from '../log-entry';

type Handler = (entry: LogEntry) => void;

export class MockHub {
  state: HubConnectionState = HubConnectionState.Disconnected;
  private handlers: Handler[] = [];
  private closeCb?: (e?: Error) => void;
  private reconnectingCb?: (e?: Error) => void;
  private reconnectedCb?: (id?: string) => void;

  on(event: string, cb: Handler): void {
    if (event === 'log') this.handlers.push(cb);
  }
  off(event: string, cb: Handler): void {
    if (event === 'log') this.handlers = this.handlers.filter((h) => h !== cb);
  }
  onclose(cb: (e?: Error) => void): void {
    this.closeCb = cb;
  }
  onreconnecting(cb: (e?: Error) => void): void {
    this.reconnectingCb = cb;
  }
  onreconnected(cb: (id?: string) => void): void {
    this.reconnectedCb = cb;
  }
  async start(): Promise<void> {
    this.state = HubConnectionState.Connected;
  }
  async stop(): Promise<void> {
    this.state = HubConnectionState.Disconnected;
    this.closeCb?.();
  }

  emit(entry: LogEntry): void {
    this.handlers.forEach((h) => h(entry));
  }
  simulateReconnecting(): void {
    this.state = HubConnectionState.Reconnecting;
    this.reconnectingCb?.();
  }
  simulateReconnected(): void {
    this.state = HubConnectionState.Connected;
    this.reconnectedCb?.('id');
  }
}
