import { describe, expect, it } from 'vitest';
import { formatRelativeTime } from '../format-relative-time';

const BASE = new Date('2026-05-28T12:00:00Z').getTime();

describe('formatRelativeTime', () => {
  it('returns "just now" for delta under 5 seconds', () => {
    const iso = new Date(BASE - 3_000).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('just now');
  });

  it('returns seconds ago for delta between 5s and 59s', () => {
    const iso = new Date(BASE - 30_000).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('30s ago');
  });

  it('returns minutes ago for delta between 60s and 59m', () => {
    const iso = new Date(BASE - 5 * 60_000).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('5m ago');
  });

  it('returns hours ago for delta between 1h and 23h', () => {
    const iso = new Date(BASE - 3 * 60 * 60_000).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('3h ago');
  });

  it('returns days ago for delta >= 24h', () => {
    const iso = new Date(BASE - 2 * 24 * 60 * 60_000).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('2d ago');
  });

  it('returns "just now" exactly at the 5s boundary (4s999ms)', () => {
    const iso = new Date(BASE - 4_999).toISOString();
    expect(formatRelativeTime(iso, BASE)).toBe('just now');
  });
});
