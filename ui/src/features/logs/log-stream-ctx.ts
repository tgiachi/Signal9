import { createContext, useContext } from 'react';
import type { useLogStream } from './use-log-stream';

type Value = ReturnType<typeof useLogStream>;

export const LogStreamCtx = createContext<Value | null>(null);

export function useLogStreamContext(): Value {
  const v = useContext(LogStreamCtx);
  if (!v) throw new Error('useLogStreamContext must be inside LogStreamProvider');
  return v;
}
