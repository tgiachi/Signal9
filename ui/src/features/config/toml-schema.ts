import type { TomlValue } from '@/lib/toml';

export type FieldType = 'text' | 'number' | 'boolean' | 'select';

export type FieldOption = {
  value: TomlValue;
  label: string;
};

export type FieldSpec = {
  path: readonly string[];
  label: string;
  type: FieldType;
  help?: string;
  options?: readonly FieldOption[];
};

export type SectionSpec = {
  key: string;
  label: string;
  fields: readonly FieldSpec[];
};

export const SCHEMA: readonly SectionSpec[] = [
  {
    key: 'runtime',
    label: 'Runtime',
    fields: [
      {
        path: ['LogLevel'],
        label: 'Log level',
        type: 'select',
        options: [
          { value: 1, label: 'Trace' },
          { value: 2, label: 'Debug' },
          { value: 3, label: 'Information' },
          { value: 4, label: 'Warning' },
          { value: 5, label: 'Error' },
          { value: 6, label: 'Critical' },
        ],
      },
      { path: ['LogToFile'], label: 'Log to file', type: 'boolean' },
      {
        path: ['DatabaseType'],
        label: 'Database type',
        type: 'select',
        options: [
          { value: 0, label: 'Sqlite' },
          { value: 1, label: 'PostgreSQL' },
        ],
        help: 'Storage backend used by FreeSql.',
      },
      { path: ['DatabaseUrl'], label: 'Database URL', type: 'text' },
    ],
  },
  {
    key: 'jwt',
    label: 'JWT',
    fields: [
      { path: ['Jwt', 'Issuer'], label: 'Issuer', type: 'text' },
      { path: ['Jwt', 'Audience'], label: 'Audience', type: 'text' },
      { path: ['Jwt', 'Secret'], label: 'Secret', type: 'text' },
      { path: ['Jwt', 'ExpirationMinutes'], label: 'Expiration (minutes)', type: 'number' },
    ],
  },
  {
    key: 'jobs',
    label: 'Job system',
    fields: [
      {
        path: ['JobSystem', 'MaxConcurrentJobs'],
        label: 'Max concurrent jobs',
        type: 'number',
        help: 'Controls how many queued jobs may run in parallel.',
      },
      {
        path: ['JobSystem', 'MaxLogEntriesPerJob'],
        label: 'Max log entries per job',
        type: 'number',
      },
    ],
  },
] as const;


export const FALLBACK_SCHEMA: readonly SectionSpec[] = SCHEMA;
