import {
  HubConnectionBuilder,
  HubConnectionState,
  type IHttpConnectionOptions,
  LogLevel as SignalRLogLevel,
  type HubConnection,
} from '@microsoft/signalr';

export { HubConnectionState };
export type SignalRConnection = HubConnection;

let factoryOverride: (() => HubConnection) | null = null;
let accessTokenProvider: (() => string | null) | null = null;

export function setLogsConnectionFactory(factory: (() => HubConnection) | null): void {
  factoryOverride = factory;
}

export function setSignalRAccessTokenProvider(provider: (() => string | null) | null): void {
  accessTokenProvider = provider;
}

function createConnection(url: string): HubConnection {
  const options = createOptions();
  const builder = new HubConnectionBuilder();
  const withUrl = options ? builder.withUrl(url, options) : builder.withUrl(url);

  return withUrl
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(SignalRLogLevel.None)
    .build();
}

function createOptions(): IHttpConnectionOptions | undefined {
  const token = accessTokenProvider?.();
  if (!token) return undefined;

  return { accessTokenFactory: () => token };
}

export function createLogsConnection(url = '/hubs/logs'): HubConnection {
  if (factoryOverride) return factoryOverride();
  return createConnection(url);
}

export function createJobStatusConnection(url = '/hubs/jobs/status'): HubConnection {
  return createConnection(url);
}

export function createJobLogsConnection(url = '/hubs/jobs/logs'): HubConnection {
  return createConnection(url);
}
