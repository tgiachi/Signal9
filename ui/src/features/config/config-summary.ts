import { parseToml, type TomlValue } from '@/lib/toml';

export type ConfigSummary = {
  valid: boolean;
  databaseType: string;
  databaseUrl: string;
  logLevel: string;
  logToFile: string;
  jwtIssuer: string;
  jwtExpiration: string;
  maxConcurrentJobs: number;
  maxLogEntriesPerJob: number;
};

const UNKNOWN = 'unknown';

export function readConfigSummary(text?: string): ConfigSummary {
  if (!text) return emptySummary(false);

  try {
    const data = parseToml(text);
    return {
      valid: true,
      databaseType: databaseTypeLabel(read(data, ['DatabaseType']) ?? read(data, ['database', 'type'])),
      databaseUrl: stringValue(read(data, ['DatabaseUrl']) ?? read(data, ['database', 'connection'])),
      logLevel: logLevelLabel(read(data, ['LogLevel']) ?? read(data, ['logging', 'level'])),
      logToFile: boolLabel(read(data, ['LogToFile']) ?? read(data, ['logging', 'console'])),
      jwtIssuer: stringValue(read(data, ['Jwt', 'Issuer']) ?? read(data, ['jwt', 'issuer'])),
      jwtExpiration: expirationLabel(
        read(data, ['Jwt', 'ExpirationMinutes']) ?? read(data, ['jwt', 'expires_min']),
      ),
      maxConcurrentJobs: numberValue(read(data, ['JobSystem', 'MaxConcurrentJobs']), 2),
      maxLogEntriesPerJob: numberValue(read(data, ['JobSystem', 'MaxLogEntriesPerJob']), 500),
    };
  } catch {
    return emptySummary(false);
  }
}

function emptySummary(valid: boolean): ConfigSummary {
  return {
    valid,
    databaseType: UNKNOWN,
    databaseUrl: UNKNOWN,
    logLevel: UNKNOWN,
    logToFile: UNKNOWN,
    jwtIssuer: UNKNOWN,
    jwtExpiration: UNKNOWN,
    maxConcurrentJobs: 2,
    maxLogEntriesPerJob: 500,
  };
}

function read(data: Record<string, TomlValue>, path: readonly string[]): TomlValue | undefined {
  let current: TomlValue | undefined = data;
  for (const segment of path) {
    if (!current || typeof current !== 'object' || current instanceof Date || Array.isArray(current)) {
      return undefined;
    }
    current = current[segment];
  }
  return current;
}

function stringValue(value: TomlValue | undefined): string {
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return UNKNOWN;
}

function numberValue(value: TomlValue | undefined, fallback: number): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return fallback;
}

function boolLabel(value: TomlValue | undefined): string {
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  return stringValue(value);
}

function expirationLabel(value: TomlValue | undefined): string {
  const minutes = numberValue(value, Number.NaN);
  if (!Number.isFinite(minutes)) return UNKNOWN;
  if (minutes < 60) return `${minutes}m`;
  if (minutes % 60 === 0) return `${minutes / 60}h`;
  return `${minutes}m`;
}

function databaseTypeLabel(value: TomlValue | undefined): string {
  if (value === 0) return 'sqlite';
  if (value === 1) return 'postgresql';
  return stringValue(value).toLowerCase();
}

function logLevelLabel(value: TomlValue | undefined): string {
  const levels = ['none', 'trace', 'debug', 'information', 'warning', 'error', 'critical'];
  if (typeof value === 'number') return levels[value] ?? UNKNOWN;
  return stringValue(value).toLowerCase();
}
