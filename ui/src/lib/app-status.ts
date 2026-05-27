export type AppStatus = {
  connection: 'connected' | 'reconnecting' | 'disconnected';
  configOk: boolean;
  errorCount: number;
};

export function useAppStatus(): AppStatus {
  return { connection: 'disconnected', configOk: true, errorCount: 0 };
}
