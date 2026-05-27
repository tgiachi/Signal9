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
    <main className="relative isolate flex min-h-full items-center justify-center overflow-hidden bg-bg-0 p-4 text-fg-0">
      <VantaBackground />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-[linear-gradient(90deg,rgba(7,9,13,0.72),rgba(7,9,13,0.56)),radial-gradient(circle_at_78%_24%,rgba(54,209,95,0.12),transparent_30%)]"
      />
      <section className="relative z-10 grid w-full max-w-5xl overflow-hidden rounded-lg border border-border/80 bg-panel/95 shadow-2xl shadow-black/35 backdrop-blur-xl md:grid-cols-[1fr_26rem]">
        <div className="flex min-h-[28rem] flex-col justify-between bg-panel-strong/90 p-6">
          <div>
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-md border border-on-air/40 bg-on-air/10 text-on-air-2">
                <RadioTower className="size-5" />
              </div>
              <div>
                <div className="text-lg font-semibold tracking-[0.14em] text-fg-0">
                  SIGNAL<span className="text-on-air-2">NINE</span>
                </div>
                <div className="font-mono text-[10px] uppercase tracking-label text-fg-2">
                  Broadcast control room
                </div>
              </div>
            </div>
            <h1 className="mt-10 max-w-lg text-2xl font-semibold tracking-normal text-fg-0">
              Control room access
            </h1>
            <p className="mt-3 max-w-lg text-sm leading-6 text-fg-1">
              Resume broadcast operations with a verified SignalNine operator account.
            </p>
          </div>
        </div>
        <form className="flex flex-col justify-center gap-4 p-6" onSubmit={submit}>
          <div className="flex size-10 items-center justify-center rounded-md border border-cyan/40 bg-cyan/10 text-cyan">
            <ShieldCheck className="size-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold">JWT session</h2>
            <p className="mt-1 text-sm text-fg-1">Use a SignalNine user account.</p>
          </div>
          <label className="block">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">
              Username
            </span>
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              className="mt-1 h-10 w-full rounded-md border border-border bg-bg-1 px-3 text-sm outline-none focus:border-on-air"
              autoComplete="username"
            />
          </label>
          <label className="block">
            <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">
              Password
            </span>
            <input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              className="mt-1 h-10 w-full rounded-md border border-border bg-bg-1 px-3 text-sm outline-none focus:border-on-air"
              autoComplete="current-password"
            />
          </label>
          {error && (
            <div
              role="alert"
              className="rounded border border-error/40 bg-error-bg/50 px-3 py-2 text-sm text-error"
            >
              {error}
            </div>
          )}
          <button
            type="submit"
            disabled={isSubmitting || !username.trim() || !password}
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-on-air/50 bg-on-air/15 px-3 text-sm font-semibold text-on-air-2 transition hover:bg-on-air/20 disabled:opacity-40"
          >
            <KeyRound className="size-4" />
            {isSubmitting ? 'Signing in' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
