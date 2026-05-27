import type { LogEntry } from '@/features/logs/log-entry';

const SOURCES = ['ConfigService', 'FreeSql', 'Startup', 'Kestrel', 'Auth', 'SerilogService'];
const INFO_MSGS = [
  'Configuration loaded',
  'Connection established',
  'Request handled in 12ms',
  'Listening on :5001',
  'Token issued for dev',
];
const WARN_MSGS = ['JWT signing key not set', 'Slow request 1.4s', 'Retrying connection'];
const ERROR_MSGS = [
  'Database connect failed: timeout',
  'Unhandled exception in pipeline',
  'TOML parse error at line 4',
];

function pick<T>(xs: readonly T[]): T {
  const i = Math.floor(Math.random() * xs.length);
  return xs[i] as T;
}

export function generateEntry(): LogEntry {
  const roll = Math.random();
  const level: LogEntry['level'] =
    roll > 0.97 ? 'error' : roll > 0.88 ? 'warn' : roll > 0.05 ? 'info' : 'debug';
  const msg =
    level === 'error' ? pick(ERROR_MSGS) : level === 'warn' ? pick(WARN_MSGS) : pick(INFO_MSGS);
  return {
    ts: new Date().toISOString(),
    level,
    source: pick(SOURCES),
    message: msg,
  };
}
