export type FieldType = 'text' | 'number' | 'boolean' | 'select';

export type FieldSpec = {
  key: string;
  label: string;
  type: FieldType;
  help?: string;
  options?: readonly string[];
};

export type SectionSpec = {
  key: string;
  label: string;
  fields: readonly FieldSpec[];
};

export const SCHEMA: readonly SectionSpec[] = [
  {
    key: 'database',
    label: 'Database',
    fields: [
      {
        key: 'type',
        label: 'Type',
        type: 'select',
        options: ['sqlite', 'postgres'],
        help: 'Storage backend.',
      },
      { key: 'connection', label: 'Connection string', type: 'text' },
    ],
  },
  {
    key: 'logging',
    label: 'Logging',
    fields: [
      {
        key: 'level',
        label: 'Level',
        type: 'select',
        options: ['debug', 'information', 'warning', 'error'],
      },
      {
        key: 'retention_days',
        label: 'Retention (days)',
        type: 'number',
        help: 'Older logs are pruned.',
      },
      { key: 'console', label: 'Write to console', type: 'boolean' },
    ],
  },
  {
    key: 'jwt',
    label: 'JWT',
    fields: [
      { key: 'issuer', label: 'Issuer', type: 'text' },
      { key: 'expires_min', label: 'Expires (minutes)', type: 'number' },
    ],
  },
] as const;
