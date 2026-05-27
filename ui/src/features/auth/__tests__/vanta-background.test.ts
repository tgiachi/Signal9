import { describe, expect, it } from 'vitest';
import { resolveVantaNetFactory } from '../vanta-loader';

describe('resolveVantaNetFactory', () => {
  it('resolves the nested CommonJS default export used by Vanta in Vite dev', () => {
    const factory = () => ({ destroy: () => undefined });

    expect(resolveVantaNetFactory({ default: { default: factory } })).toBe(factory);
  });
});
