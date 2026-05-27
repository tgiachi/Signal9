import { type ReactNode } from 'react';
import { AuthContext } from './auth-context';

export function AuthProvider({ children }: { children: ReactNode }) {
  return (
    <AuthContext.Provider value={{ authenticated: true, user: { name: 'dev' } }}>
      {children}
    </AuthContext.Provider>
  );
}
