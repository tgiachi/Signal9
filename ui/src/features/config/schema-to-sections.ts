import { FALLBACK_SCHEMA, type FieldOption, type FieldSpec, type SectionSpec } from './toml-schema';
import type { TomlValue } from '@/lib/toml';

export type ConfigSchemaUiMetadata = {
  section?: string;
  sectionTitle?: string;
  group?: string;
  order?: number;
  widget?: string;
  secret?: boolean;
};

export type ConfigSchemaEnumOption = {
  const?: TomlValue;
  title?: string;
};

export type ConfigSchemaNode = {
  type?: string;
  title?: string;
  description?: string;
  default?: TomlValue;
  minimum?: number;
  maximum?: number;
  enum?: TomlValue[];
  oneOf?: ConfigSchemaEnumOption[];
  properties?: Record<string, ConfigSchemaNode>;
  'x-signalnine-ui'?: ConfigSchemaUiMetadata;
};

export type ConfigSchemaDocument = ConfigSchemaNode & {
  $schema?: string;
  $id?: string;
};

type SectionBuilder = {
  key: string;
  label: string;
  order: number;
  fields: FieldBuilder[];
};

type FieldBuilder = FieldSpec & {
  order: number;
};

type SectionContext = {
  key: string;
  label: string;
  order: number;
};

const FALLBACK_SECTION_LABELS = new Map(FALLBACK_SCHEMA.map((section) => [section.key, section.label]));

export function schemaToSections(schema: ConfigSchemaDocument): readonly SectionSpec[] {
  const sections = new Map<string, SectionBuilder>();

  for (const [key, node] of Object.entries(schema.properties ?? {})) {
    visitNode(sections, node, [key], undefined);
  }

  const result = Array.from(sections.values())
    .map((section) => ({
      key: section.key,
      label: section.label,
      order: section.order,
      fields: section.fields
        .sort((left, right) => left.order - right.order || left.label.localeCompare(right.label))
        .map((field) => ({
          path: field.path,
          label: field.label,
          type: field.type,
          help: field.help,
          options: field.options,
          defaultValue: field.defaultValue,
          group: field.group,
          secret: field.secret,
          min: field.min,
          max: field.max,
          order: field.order,
        })),
    }))
    .sort((left, right) => left.order - right.order || left.label.localeCompare(right.label));

  return result.length > 0 ? result : FALLBACK_SCHEMA;
}

function visitNode(
  sections: Map<string, SectionBuilder>,
  node: ConfigSchemaNode,
  path: readonly string[],
  parentSection: SectionContext | undefined,
): void {
  const ui = node['x-signalnine-ui'];
  const section = resolveSection(node, ui, parentSection);

  if (node.properties) {
    for (const [key, child] of Object.entries(node.properties)) {
      visitNode(sections, child, [...path, key], section);
    }
    return;
  }

  if (!section) return;

  const field = toField(node, path, ui, section);
  if (!field) return;

  const builder = ensureSection(sections, section);
  builder.fields.push(field);
}

function resolveSection(
  node: ConfigSchemaNode,
  ui: ConfigSchemaUiMetadata | undefined,
  parentSection: SectionContext | undefined,
): SectionContext | undefined {
  if (ui?.section) {
    return {
      key: ui.section,
      label: ui.sectionTitle ?? FALLBACK_SECTION_LABELS.get(ui.section) ?? node.title ?? ui.section,
      order: ui.order ?? parentSection?.order ?? Number.MAX_SAFE_INTEGER,
    };
  }

  return parentSection;
}

function ensureSection(
  sections: Map<string, SectionBuilder>,
  section: SectionContext,
): SectionBuilder {
  const existing = sections.get(section.key);
  if (existing) {
    existing.order = Math.min(existing.order, section.order);
    if (!existing.label && section.label) existing.label = section.label;
    return existing;
  }

  const created: SectionBuilder = {
    key: section.key,
    label: section.label,
    order: section.order,
    fields: [],
  };
  sections.set(section.key, created);
  return created;
}

function toField(
  node: ConfigSchemaNode,
  path: readonly string[],
  ui: ConfigSchemaUiMetadata | undefined,
  section: SectionContext,
): FieldBuilder | null {
  const options = readOptions(node);
  const type = readFieldType(node, options);
  if (!type) return null;

  return {
    path,
    label: node.title ?? path[path.length - 1] ?? '',
    type,
    help: node.description,
    options,
    defaultValue: node.default,
    group: ui?.group,
    secret: ui?.secret === true || ui?.widget === 'password',
    min: node.minimum,
    max: node.maximum,
    order: ui?.order ?? section.order,
  };
}

function readFieldType(
  node: ConfigSchemaNode,
  options: readonly FieldOption[] | undefined,
): FieldSpec['type'] | null {
  if (options && options.length > 0) return 'select';

  switch (node.type) {
    case 'string':
      return 'text';
    case 'integer':
    case 'number':
      return 'number';
    case 'boolean':
      return 'boolean';
    default:
      return null;
  }
}

function readOptions(node: ConfigSchemaNode): readonly FieldOption[] | undefined {
  if (node.oneOf && node.oneOf.length > 0) {
    return node.oneOf
      .filter(
        (option): option is ConfigSchemaEnumOption & { const: TomlValue } =>
          option.const !== undefined,
      )
      .map((option) => ({
        value: option.const,
        label: option.title ?? String(option.const),
      }));
  }

  if (node.enum && node.enum.length > 0) {
    return node.enum.map((value) => ({
      value,
      label: String(value),
    }));
  }

  return undefined;
}
