import type { VantaEffect, VantaNetOptions } from 'vanta/dist/vanta.net.min';

export type VantaNetFactory = (options: VantaNetOptions) => VantaEffect;

type VantaDefaultExport = VantaNetFactory | { default?: unknown };

export function resolveVantaNetFactory(module: unknown): VantaNetFactory | null {
  if (typeof module === 'function') return module as VantaNetFactory;
  if (!module || typeof module !== 'object') return null;

  const defaultExport = (module as { default?: VantaDefaultExport }).default;
  if (typeof defaultExport === 'function') return defaultExport;
  if (!defaultExport || typeof defaultExport !== 'object') return null;

  const nestedDefault = defaultExport.default;
  if (typeof nestedDefault === 'function') return nestedDefault as VantaNetFactory;

  return null;
}
