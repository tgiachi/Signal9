import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel as SignalRLogLevel,
  type HubConnection,
} from '@microsoft/signalr';

export { HubConnectionState };
export type SignalRConnection = HubConnection;

let factoryOverride: (() => HubConnection) | null = null;

export function setLogsConnectionFactory(factory: (() => HubConnection) | null): void {
  factoryOverride = factory;
}

export function createLogsConnection(url = '/hub/logs'): HubConnection {
  if (factoryOverride) return factoryOverride();
  return new HubConnectionBuilder()
    .withUrl(url)
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(SignalRLogLevel.Warning)
    .build();
}
