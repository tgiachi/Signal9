import { useState, type FormEvent } from 'react';
import { KeyRound, RadioTower, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { useAuth } from '@/providers/auth-context';
import { VantaBackground } from './vanta-background';

export function LoginScreen() {
  const auth = useAuth();
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      await auth.login(username, password);
      setPassword('');
      toast.success('Logged in');
    } catch (ex) {
      const message = ex instanceof Error ? ex.message : 'Login failed';
      setError(message);
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="relative isolate flex min-h-full items-center justify-center overflow-hidden bg-bg-0 p-4 text-fg-1">
      <VantaBackground />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-[linear-gradient(90deg,rgba(14,20,24,0.78),rgba(14,20,24,0.58))]"
      />
      <section className="relative z-10 grid w-full max-w-5xl overflow-hidden rounded-[10px] bg-bg-2 md:grid-cols-[1fr_26rem]">
        <div className="flex min-h-[28rem] flex-col justify-between bg-bg-4 p-6">
          <div>
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-[6px] bg-accent-live text-bg-5">
                <RadioTower className="size-5" />
              </div>
              <div>
                <div className="text-lg font-semibold tracking-brand text-fg-0">
                  SIGNAL<span className="text-accent-live">NINE</span>
                </div>
                <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">
                  Broadcast control room
                </div>
              </div>
            </div>
            <h1 className="mt-10 max-w-lg text-2xl font-semibold tracking-normal text-fg-0">
              Control room access
            </h1>
            <p className="mt-3 max-w-lg text-sm leading-6 text-fg-2">
              Resume broadcast operations with a verified SignalNine operator account.
            </p>
          </div>
        </div>
        <form className="flex flex-col justify-center gap-4 p-6" onSubmit={submit}>
          <div className="flex size-10 items-center justify-center rounded-[6px] bg-accent-jobs text-fg-0">
            <ShieldCheck className="size-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-fg-0">JWT session</h2>
            <p className="mt-1 text-sm text-fg-2">Use a SignalNine user account.</p>
          </div>
          <label className="block">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              Username
            </span>
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              className="mt-1 h-10 w-full rounded-[6px] bg-bg-1 px-3 text-sm text-fg-1 outline-none placeholder:text-fg-3 focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
              autoComplete="username"
            />
          </label>
          <label className="block">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              Password
            </span>
            <input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              className="mt-1 h-10 w-full rounded-[6px] bg-bg-1 px-3 text-sm text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
              autoComplete="current-password"
            />
          </label>
          {error && (
            <div
              role="alert"
              className="rounded-[6px] bg-accent-err px-3 py-2 text-sm text-fg-0"
            >
              {error}
            </div>
          )}
          <button
            type="submit"
            disabled={isSubmitting || !username.trim() || !password}
            className="inline-flex h-10 items-center justify-center gap-2 rounded-[6px] bg-accent-live px-3 text-sm font-semibold text-bg-5 transition hover:bg-accent-live-hover disabled:opacity-40"
          >
            <KeyRound className="size-4" />
            {isSubmitting ? 'Signing in' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
