import { type ReactNode } from 'react';
import { LoginScreen } from '@/features/auth/login-screen';
import { useAuth } from './auth-context';

export function AuthGate({ children }: { children: ReactNode }) {
  const auth = useAuth();
  if (!auth.authenticated) return <LoginScreen />;
  return <>{children}</>;
}
