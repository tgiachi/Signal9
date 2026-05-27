import { type ReactNode } from 'react';
import { useLogStream } from './use-log-stream';
import { LogStreamCtx } from './log-stream-ctx';

export function LogStreamProvider({ children }: { children: ReactNode }) {
  const value = useLogStream();
  return <LogStreamCtx.Provider value={value}>{children}</LogStreamCtx.Provider>;
}
