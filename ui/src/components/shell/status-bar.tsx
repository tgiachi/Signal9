import { type ReactNode, useState } from 'react';
import { KeyRound, LogOut, RadioTower } from 'lucide-react';
import { toast } from 'sonner';
import type { EndpointStatus } from '@/lib/health';
import { useAuth } from '@/providers/auth-context';
import { cn } from '@/lib/cn';

export type ConnectionState = 'connected' | 'reconnecting' | 'disconnected';

type Props = {
  connection: ConnectionState;
  jobsConnection?: ConnectionState;
  health?: EndpointStatus;
  live?: EndpointStatus;
  configOk: boolean;
  errorCount: number;
  runningJobs?: number;
  maxConcurrentJobs?: number;
};

const LIVE_PILL: Record<ConnectionState, { label: string; cls: string }> = {
  connected: { label: 'ON AIR', cls: 'border-on-air/50 bg-on-air/15 text-on-air-2' },
  reconnecting: { label: 'RECONNECT', cls: 'border-warn/50 bg-warn/10 text-warn animate-pulse' },
  disconnected: { label: 'OFFLINE', cls: 'border-error/50 bg-error-bg/50 text-error' },
};

export function StatusBar({
  connection,
  jobsConnection = 'disconnected',
  health = 'unknown',
  live = 'unknown',
  configOk,
  errorCount,
  runningJobs = 0,
  maxConcurrentJobs = 1,
}: Props) {
  const auth = useAuth();
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('');
  const [isLoggingIn, setIsLoggingIn] = useState(false);
  const livePill = LIVE_PILL[connection];

  const login = async () => {
    setIsLoggingIn(true);
    try {
      await auth.login(username, password);
      setPassword('');
      toast.success('Logged in');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Login failed');
    } finally {
      setIsLoggingIn(false);
    }
  };

  return (
    <header className="flex min-h-12 flex-wrap items-center gap-2 border-b border-border bg-bg-2 px-3 py-2 text-xs">
      <div className="mr-2 flex items-center gap-2 font-semibold tracking-[0.12em] text-fg-0 md:hidden">
        <RadioTower className="size-4 text-on-air-2" />
        SIGNALNINE
      </div>
      <Pill testId="pill-live" className={livePill.cls}>
        {livePill.label}
      </Pill>
      <Pill className={statusClass(health)}>/health {health}</Pill>
      <Pill className={statusClass(live)}>/live {live}</Pill>
      <Pill
        className={
          auth.authenticated
            ? statusClass(jobsConnection)
            : 'border-warn/40 bg-warn/10 text-warn'
        }
      >
        {auth.authenticated ? `jobs ${runningJobs}/${maxConcurrentJobs}` : 'jobs locked'}
      </Pill>
      <Pill
        testId="pill-cfg"
        className={
          configOk
            ? 'border-border bg-bg-3 text-fg-1'
            : 'border-warn/50 bg-warn/10 text-warn'
        }
      >
        {configOk ? 'config synced' : 'config dirty'}
      </Pill>
      {errorCount > 0 && (
        <Pill testId="pill-err" className="border-error/50 bg-error-bg/60 text-error">
          {errorCount} err
        </Pill>
      )}
      <div className="ml-auto flex min-w-0 items-center gap-2">
        {auth.authenticated ? (
          <>
            <span className="max-w-36 truncate rounded border border-border bg-bg-1 px-2 py-1 font-mono text-[10px] text-fg-1">
              JWT {auth.user?.username ?? 'session'}
            </span>
            <button
              type="button"
              onClick={auth.logout}
              className="flex size-7 items-center justify-center rounded border border-border bg-bg-1 text-fg-1 hover:text-fg-0"
              title="Log out"
            >
              <LogOut className="size-3.5" />
            </button>
          </>
        ) : (
          <form
            className="flex items-center gap-1 max-sm:w-full"
            onSubmit={(event) => {
              event.preventDefault();
              void login();
            }}
          >
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              className="h-7 w-24 rounded border border-border bg-bg-1 px-2 font-mono text-[11px] outline-none focus:border-on-air max-sm:flex-1"
              placeholder="user"
            />
            <input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              className="h-7 w-28 rounded border border-border bg-bg-1 px-2 font-mono text-[11px] outline-none focus:border-on-air max-sm:flex-1"
              placeholder="password"
            />
            <button
              type="submit"
              disabled={isLoggingIn || !username.trim() || !password}
              className="flex h-7 items-center gap-1 rounded border border-on-air/40 bg-on-air/10 px-2 font-mono text-[10px] uppercase tracking-label text-on-air-2 hover:bg-on-air/20 disabled:opacity-40"
            >
              <KeyRound className="size-3" />
              Login
            </button>
          </form>
        )}
      </div>
    </header>
  );
}

function Pill({
  children,
  className,
  testId,
}: {
  children: ReactNode;
  className: string;
  testId?: string;
}) {
  return (
    <span
      data-testid={testId}
      className={cn('rounded border px-2 py-1 font-mono text-[10px] uppercase tracking-label', className)}
    >
      {children}
    </span>
  );
}

function statusClass(status: EndpointStatus | ConnectionState): string {
  if (status === 'ok' || status === 'connected') {
    return 'border-on-air/40 bg-on-air/10 text-on-air-2';
  }
  if (status === 'reconnecting' || status === 'unknown') {
    return 'border-warn/40 bg-warn/10 text-warn';
  }
  return 'border-error/50 bg-error-bg/60 text-error';
}
