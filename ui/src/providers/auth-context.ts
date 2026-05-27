import { createContext, useContext } from 'react';

export type AuthState = { authenticated: boolean; user: { name: string } | null };

export const AuthContext = createContext<AuthState>({
  authenticated: true,
  user: { name: 'dev' },
});

export function useAuth(): AuthState {
  return useContext(AuthContext);
}
