import { createContext, useContext } from 'react';

export type AuthUser = {
  id?: string;
  username: string;
  email?: string;
  role?: string;
};

export type AuthState = {
  authenticated: boolean;
  token: string | null;
  expiresAt: string | null;
  user: AuthUser | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
};

export const AuthContext = createContext<AuthState>({
  authenticated: false,
  token: null,
  expiresAt: null,
  user: null,
  login: async () => undefined,
  logout: () => undefined,
});

export function useAuth(): AuthState {
  return useContext(AuthContext);
}
