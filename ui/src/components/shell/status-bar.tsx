import { useState } from 'react';
import { KeyRound, LogOut, RadioTower } from 'lucide-react';
import { toast } from 'sonner';
import type { EndpointStatus } from '@/lib/health';
import { useAuth } from '@/providers/auth-context';
import { Pill, type PillVariant } from '@/components/ui/pill';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

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

  const liveLabel =
    connection === 'connected'
      ? 'ON AIR'
      : connection === 'reconnecting'
        ? 'RECONNECT'
        : 'OFFLINE';
  const liveVariant: PillVariant =
    connection === 'connected' ? 'live' : connection === 'reconnecting' ? 'warn' : 'err';

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
    <header className="flex min-h-10 flex-wrap items-center gap-1.5 bg-bg-1 px-4 py-2">
      <div className="mr-2 flex items-center gap-2 text-[12px] font-bold tracking-brand text-fg-1 md:hidden">
        <RadioTower className="size-4 text-accent-live" />
        SIGNALNINE
      </div>
      <Pill data-testid="pill-live" variant={liveVariant} dot>
        {liveLabel}
      </Pill>
      <Pill variant={endpointVariant(health)}>/health {health}</Pill>
      <Pill variant={endpointVariant(live)}>/live {live}</Pill>
      <Pill variant={auth.authenticated ? endpointVariant(jobsConnection) : 'warn'}>
        {auth.authenticated ? `jobs ${runningJobs}/${maxConcurrentJobs}` : 'jobs locked'}
      </Pill>
      <Pill data-testid="pill-cfg" variant={configOk ? 'cfg' : 'warn'}>
        {configOk ? 'config sync' : 'config dirty'}
      </Pill>
      {errorCount > 0 && (
        <Pill data-testid="pill-err" variant="err">
          {errorCount} err
        </Pill>
      )}
      <div className="ml-auto flex min-w-0 items-center gap-2">
        {auth.authenticated ? (
          <>
            <span className="max-w-36 truncate rounded-[4px] bg-bg-2 px-2 py-1 font-mono text-[10px] text-fg-2">
              JWT · {auth.user?.username ?? 'session'}
            </span>
            <Button
              type="button"
              onClick={auth.logout}
              variant="icon"
              size="icon"
              aria-label="Log out"
            >
              <LogOut />
            </Button>
          </>
        ) : (
          <form
            className="flex items-center gap-1 max-sm:w-full"
            onSubmit={(event) => {
              event.preventDefault();
              void login();
            }}
          >
            <Input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              className="h-7 w-24 px-2 font-mono text-[11px] max-sm:flex-1"
              placeholder="user"
            />
            <Input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              className="h-7 w-28 px-2 font-mono text-[11px] max-sm:flex-1"
              placeholder="password"
            />
            <Button
              type="submit"
              variant="primary"
              size="sm"
              disabled={isLoggingIn || !username.trim() || !password}
              className="h-7"
            >
              <KeyRound />
              Login
            </Button>
          </form>
        )}
      </div>
    </header>
  );
}

function endpointVariant(status: EndpointStatus | ConnectionState): PillVariant {
  if (status === 'ok' || status === 'connected') return 'live';
  if (status === 'reconnecting' || status === 'unknown') return 'warn';
  return 'err';
}
