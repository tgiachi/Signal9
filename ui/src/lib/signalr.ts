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
  return new HubConnectionBuilder()
    .withUrl(url, createOptions())
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(SignalRLogLevel.None)
    .build();
}

function createOptions(): IHttpConnectionOptions {
  // accessTokenFactory is invoked by SignalR on every handshake / reconnect, so we close over
  // the *provider* (not a snapshot of the token). This way the latest JWT — including one that
  // arrived after the hub was created, or a refreshed one — is always used.
  return { accessTokenFactory: () => accessTokenProvider?.() ?? '' };
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

export function createFfmpegConnection(url = '/hubs/ffmpeg'): HubConnection {
  return createConnection(url);
}
