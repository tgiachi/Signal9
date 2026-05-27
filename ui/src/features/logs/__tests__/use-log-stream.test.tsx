import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { useLogStream } from '../use-log-stream';
import { MockHub } from './mock-hub';
import type { LogEntry } from '../log-entry';

const entry = (overrides: Partial<LogEntry> = {}): LogEntry => ({
  ts: new Date().toISOString(),
  level: 'info',
  source: 'Test',
  message: 'hello',
  ...overrides,
});

describe('useLogStream', () => {
  it('starts disconnected, becomes connected after start()', async () => {
    const hub = new MockHub();
    const { result } = renderHook(() => useLogStream({ hubFactory: () => hub as never }));
    await waitFor(() => expect(result.current.connection).toBe('connected'));
    expect(result.current.entries).toEqual([]);
  });

  it('appends pushed entries', async () => {
    const hub = new MockHub();
    const { result } = renderHook(() => useLogStream({ hubFactory: () => hub as never }));
    await waitFor(() => expect(result.current.connection).toBe('connected'));
    act(() => hub.emit(entry({ message: 'one' })));
    act(() => hub.emit(entry({ message: 'two' })));
    expect(result.current.entries.map((e) => e.message)).toEqual(['one', 'two']);
  });

  it('caps the ring buffer at the configured max', async () => {
    const hub = new MockHub();
    const { result } = renderHook(() =>
      useLogStream({ hubFactory: () => hub as never, maxEntries: 3 }),
    );
    await waitFor(() => expect(result.current.connection).toBe('connected'));
    act(() => {
      hub.emit(entry({ message: 'a' }));
      hub.emit(entry({ message: 'b' }));
      hub.emit(entry({ message: 'c' }));
      hub.emit(entry({ message: 'd' }));
    });
    expect(result.current.entries.map((e) => e.message)).toEqual(['b', 'c', 'd']);
  });

  it('transitions connection on reconnecting → reconnected', async () => {
    const hub = new MockHub();
    const { result } = renderHook(() => useLogStream({ hubFactory: () => hub as never }));
    await waitFor(() => expect(result.current.connection).toBe('connected'));
    act(() => hub.simulateReconnecting());
    await waitFor(() => expect(result.current.connection).toBe('reconnecting'));
    act(() => hub.simulateReconnected());
    await waitFor(() => expect(result.current.connection).toBe('connected'));
  });

  it('reports error count in the last minute', async () => {
    const hub = new MockHub();
    const { result } = renderHook(() => useLogStream({ hubFactory: () => hub as never }));
    await waitFor(() => expect(result.current.connection).toBe('connected'));
    const now = new Date().toISOString();
    act(() => {
      hub.emit(entry({ level: 'error', ts: now }));
      hub.emit(entry({ level: 'error', ts: now }));
      hub.emit(entry({ level: 'warn', ts: now }));
    });
    expect(result.current.errorCountLastMinute).toBe(2);
  });
});
