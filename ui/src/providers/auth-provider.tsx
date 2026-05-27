import { type ReactNode, useEffect, useMemo, useState } from 'react';
import { apiJson, setApiAccessTokenProvider } from '@/lib/api';
import { setSignalRAccessTokenProvider } from '@/lib/signalr';
import { AuthContext, type AuthUser } from './auth-context';

type StoredAuth = {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
};

type LoginResponse = {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
};

const STORAGE_KEY = 'signalnine.auth';

function readStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredAuth;
    if (!parsed.accessToken || !parsed.expiresAt || !parsed.user) return null;
    if (Date.parse(parsed.expiresAt) <= Date.now()) return null;
    return parsed;
  } catch {
    return null;
  }
}

function installTokenProviders(auth: StoredAuth | null): void {
  const provider = () => auth?.accessToken ?? null;
  setApiAccessTokenProvider(provider);
  setSignalRAccessTokenProvider(provider);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<StoredAuth | null>(() => {
    const stored = readStoredAuth();
    installTokenProviders(stored);
    return stored;
  });

  useEffect(() => {
    installTokenProviders(auth);
  }, [auth]);

  const value = useMemo(
    () => ({
      authenticated: Boolean(auth),
      token: auth?.accessToken ?? null,
      expiresAt: auth?.expiresAt ?? null,
      user: auth?.user ?? null,
      login: async (username: string, password: string) => {
        const response = await apiJson<LoginResponse>('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ username, password }),
        });
        const next = {
          accessToken: response.accessToken,
          expiresAt: response.expiresAt,
          user: response.user,
        };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
        installTokenProviders(next);
        setAuth(next);
      },
      logout: () => {
        localStorage.removeItem(STORAGE_KEY);
        installTokenProviders(null);
        setAuth(null);
      },
    }),
    [auth],
  );

  return (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}
