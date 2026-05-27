import { RouterProvider } from 'react-router';
import { Toaster } from 'sonner';
import { router } from './routes';
import { QueryProvider } from './providers/query-provider';
import { ThemeProvider } from './providers/theme-provider';
import { AuthProvider } from './providers/auth-provider';

export default function App() {
  return (
    <ThemeProvider>
      <QueryProvider>
        <AuthProvider>
          <RouterProvider router={router} />
          <Toaster position="bottom-right" theme="dark" richColors />
        </AuthProvider>
      </QueryProvider>
    </ThemeProvider>
  );
}
