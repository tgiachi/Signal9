import { Outlet } from 'react-router';
import { StatusBar, type ConnectionState } from './status-bar';
import { NavTabs } from './nav-tabs';
import { useAppStatus } from '@/lib/app-status';

export function AppShell() {
  const status = useAppStatus();
  return (
    <div className="flex h-full flex-col bg-bg-0 text-fg-0">
      <StatusBar
        connection={status.connection as ConnectionState}
        configOk={status.configOk}
        errorCount={status.errorCount}
      />
      <NavTabs />
      <main className="flex-1 overflow-hidden">
        <Outlet />
      </main>
    </div>
  );
}
