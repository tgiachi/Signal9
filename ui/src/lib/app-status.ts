import { useQueryClient } from '@tanstack/react-query';
import { useLogStreamContext } from '@/features/logs/log-stream-ctx';

export type AppStatus = {
  connection: 'connected' | 'reconnecting' | 'disconnected';
  configOk: boolean;
  errorCount: number;
};

export function useAppStatus(): AppStatus {
  const { connection, errorCountLastMinute } = useLogStreamContext();
  const qc = useQueryClient();
  const cached = qc.getQueryState(['config']);
  const configOk = !cached || cached.status !== 'error';
  return { connection, configOk, errorCount: errorCountLastMinute };
}
