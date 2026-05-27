import { RouterProvider } from 'react-router';
import { Toaster } from 'sonner';
import { router } from './routes';
import { QueryProvider } from './providers/query-provider';
import { ThemeProvider } from './providers/theme-provider';
import { AuthProvider } from './providers/auth-provider';
import { LogStreamProvider } from './features/logs/log-stream-context';
import { JobsProvider } from './features/jobs/jobs-context';

export default function App() {
  return (
    <ThemeProvider>
      <QueryProvider>
        <AuthProvider>
          <LogStreamProvider>
            <JobsProvider>
              <RouterProvider router={router} />
            </JobsProvider>
          </LogStreamProvider>
          <Toaster position="bottom-right" theme="dark" richColors />
        </AuthProvider>
      </QueryProvider>
    </ThemeProvider>
  );
}
