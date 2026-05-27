import { describe, it, expect } from 'vitest';
import { parseToml, stringifyToml } from '../toml';

describe('toml wrapper', () => {
  it('parses simple TOML', () => {
    const out = parseToml('[a]\nx = 1\n');
    expect(out).toEqual({ a: { x: 1 } });
  });

  it('stringifies + reparses (roundtrip preserves values)', () => {
    const src = {
      logging: { level: 'information', retention_days: 14 },
      jwt: { issuer: 'signal9' },
    };
    const text = stringifyToml(src);
    expect(parseToml(text)).toEqual(src);
  });

  it('throws with line info on invalid TOML', () => {
    expect(() => parseToml('[a\nx = 1')).toThrow();
  });
});
