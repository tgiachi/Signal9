import { parse, stringify } from 'smol-toml';

export type TomlValue =
  | string
  | number
  | boolean
  | Date
  | TomlValue[]
  | { [key: string]: TomlValue };

export function parseToml(text: string): Record<string, TomlValue> {
  return parse(text) as Record<string, TomlValue>;
}

export function stringifyToml(value: Record<string, TomlValue>): string {
  return stringify(value);
}

export type TomlParseError = { message: string; line?: number; column?: number };

export function safeParseToml(
  text: string,
):
  | { ok: true; value: Record<string, TomlValue> }
  | { ok: false; error: TomlParseError } {
  try {
    return { ok: true, value: parseToml(text) };
  } catch (e) {
    const err = e as { message?: string; line?: number; column?: number };
    return {
      ok: false,
      error: { message: err.message ?? 'Parse error', line: err.line, column: err.column },
    };
  }
}
