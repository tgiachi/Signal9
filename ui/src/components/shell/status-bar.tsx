import { cn } from '@/lib/cn';

export type ConnectionState = 'connected' | 'reconnecting' | 'disconnected';

type Props = {
  connection: ConnectionState;
  configOk: boolean;
  errorCount: number;
};

const LIVE_PILL: Record<ConnectionState, { label: string; cls: string }> = {
  connected: { label: '● LIVE', cls: 'bg-on-air text-black' },
  reconnecting: { label: '○ RECONNECT', cls: 'bg-warn text-black animate-pulse' },
  disconnected: { label: '✕ OFFLINE', cls: 'bg-error-bg text-error' },
};

export function StatusBar({ connection, configOk, errorCount }: Props) {
  const livePill = LIVE_PILL[connection];

  return (
    <header className="flex h-10 items-center gap-2 border-b border-border bg-bg-2 px-3 text-xs">
      <span className="mr-3 font-semibold tracking-[0.15em] text-on-air-2">SIGNAL9</span>
      <span
        data-testid="pill-live"
        className={cn(
          'rounded-sm px-2 py-0.5 font-mono text-[10px] font-semibold',
          livePill.cls,
        )}
      >
        {livePill.label}
      </span>
      <span
        data-testid="pill-cfg"
        className={cn(
          'rounded-sm px-2 py-0.5 font-mono text-[10px] font-semibold',
          configOk ? 'bg-bg-3 text-fg-1' : 'bg-warn text-black',
        )}
      >
        {configOk ? 'CFG OK' : 'CFG DIRTY'}
      </span>
      {errorCount > 0 && (
        <span
          data-testid="pill-err"
          className="rounded-sm bg-error-bg px-2 py-0.5 font-mono text-[10px] font-semibold text-error"
        >
          {errorCount} ERR
        </span>
      )}
      <span className="ml-auto font-mono text-[10px] text-fg-2" data-testid="clock" />
    </header>
  );
}
