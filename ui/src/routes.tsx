import { createBrowserRouter, Navigate } from 'react-router';
import { AppShell } from '@/components/shell/app-shell';
import { LogsPage } from '@/features/logs/logs-page';
import { ConfigPage } from '@/features/config/config-page';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/logs" replace /> },
      { path: 'logs', element: <LogsPage /> },
      { path: 'config', element: <ConfigPage /> },
      { path: '*', element: <div className="p-4 text-fg-1">404 — page not found</div> },
    ],
  },
]);
