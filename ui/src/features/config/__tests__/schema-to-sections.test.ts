import { describe, expect, it } from 'vitest';
import { schemaToSections, type ConfigSchemaDocument } from '../schema-to-sections';

describe('schemaToSections', () => {
  it('converts backend JSON schema into form sections', () => {
    const schema: ConfigSchemaDocument = {
      type: 'object',
      properties: {
        Pipeline: {
          type: 'object',
          title: 'Media pipeline',
          'x-signalnine-ui': {
            section: 'pipeline',
            sectionTitle: 'Media pipeline',
            order: 500,
          },
          properties: {
            Tasks: {
              type: 'object',
              properties: {
                Preview: {
                  type: 'object',
                  title: 'Preview',
                  properties: {
                    PreviewCount: {
                      type: 'integer',
                      title: 'Preview count',
                      description: 'Number of generated thumbnails.',
                      default: 5,
                      minimum: 1,
                      maximum: 20,
                      'x-signalnine-ui': {
                        order: 110,
                      },
                    },
                  },
                },
              },
            },
          },
        },
      },
    };

    const sections = schemaToSections(schema);

    expect(sections).toHaveLength(1);
    expect(sections[0]?.key).toBe('pipeline');
    expect(sections[0]?.label).toBe('Media pipeline');
    expect(sections[0]?.fields[0]).toMatchObject({
      path: ['Pipeline', 'Tasks', 'Preview', 'PreviewCount'],
      label: 'Preview count',
      type: 'number',
      help: 'Number of generated thumbnails.',
      defaultValue: 5,
      min: 1,
      max: 20,
    });
  });

  it('converts oneOf values into select options', () => {
    const schema: ConfigSchemaDocument = {
      type: 'object',
      properties: {
        LogLevel: {
          type: 'integer',
          title: 'Log level',
          default: 3,
          oneOf: [
            { const: 2, title: 'Debug' },
            { const: 3, title: 'Information' },
          ],
          'x-signalnine-ui': {
            section: 'runtime',
            sectionTitle: 'Runtime',
            order: 100,
          },
        },
      },
    };

    const sections = schemaToSections(schema);

    expect(sections[0]?.fields[0]).toMatchObject({
      path: ['LogLevel'],
      label: 'Log level',
      type: 'select',
      options: [
        { value: 2, label: 'Debug' },
        { value: 3, label: 'Information' },
      ],
    });
  });
});
