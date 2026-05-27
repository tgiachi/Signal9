export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

export type LogEntry = {
  ts: string;
  level: LogLevel;
  source: string;
  message: string;
  props?: Record<string, unknown>;
};
