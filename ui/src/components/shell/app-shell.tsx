import { Outlet } from 'react-router';
import { StatusBar, type ConnectionState } from './status-bar';
import { NavTabs } from './nav-tabs';
import { readConfigSummary } from '@/features/config/config-summary';
import { useConfig } from '@/features/config/use-config';
import { useJobsContext } from '@/features/jobs/jobs-context-value';
import { useHealthState } from '@/lib/health';
import { useAppStatus } from '@/lib/app-status';

export function AppShell() {
  const status = useAppStatus();
  const jobs = useJobsContext();
  const health = useHealthState();
  const config = useConfig();
  const configSummary = readConfigSummary(config.text);

  return (
    <div className="flex h-full min-h-0 bg-bg-0 text-fg-1 max-md:flex-col">
      <NavTabs />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <StatusBar
          connection={status.connection as ConnectionState}
          jobsConnection={jobs.connection}
          health={health.health.status}
          live={health.live.status}
          configOk={status.configOk}
          errorCount={status.errorCount}
          runningJobs={jobs.counts.running}
          maxConcurrentJobs={configSummary.maxConcurrentJobs}
        />
        <main className="min-h-0 flex-1 overflow-hidden">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
