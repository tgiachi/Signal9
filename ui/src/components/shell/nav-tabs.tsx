import { NavLink } from 'react-router';
import {
  Activity,
  Database,
  ListChecks,
  RadioTower,
  ScrollText,
  Server,
  Settings,
} from 'lucide-react';
import { cn } from '@/lib/cn';

const TABS = [
  { to: '/monitor', label: 'Monitor', icon: Activity },
  { to: '/channels', label: 'Channels', icon: RadioTower },
  { to: '/media-libraries', label: 'Libraries', icon: Database },
  { to: '/jobs', label: 'Jobs', icon: ListChecks },
  { to: '/logs', label: 'Logs', icon: ScrollText },
  { to: '/settings/jellyfin', label: 'Jellyfin', icon: Server },
  { to: '/config', label: 'Config', icon: Settings },
] as const;

export function NavTabs() {
  return (
    <nav className="flex shrink-0 flex-col border-r border-border bg-bg-1 md:w-36 max-md:order-last max-md:h-16 max-md:w-full max-md:border-r-0 max-md:border-t">
      <div className="hidden h-16 items-center gap-2 border-b border-border-subtle px-3 md:flex">
        <div className="flex size-8 items-center justify-center rounded-md border border-on-air/40 bg-on-air/10 text-on-air-2">
          S9
        </div>
        <div className="min-w-0">
          <div className="text-sm font-semibold tracking-[0.12em] text-fg-0">SIGNAL</div>
          <div className="text-sm font-semibold tracking-[0.12em] text-on-air-2">NINE</div>
        </div>
      </div>
      <div className="flex min-h-0 flex-1 gap-1 p-2 md:flex-col max-md:grid max-md:grid-cols-7">
        {TABS.map((t) => {
          const Icon = t.icon;
          return (
            <NavLink
              key={t.to}
              to={t.to}
              end
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md border px-2 py-2 text-[12px] transition-colors md:justify-start max-md:flex-col max-md:justify-center max-md:gap-1 max-md:px-1 max-md:py-1',
                  isActive
                    ? 'border-on-air/40 bg-on-air/10 text-on-air-2'
                    : 'border-transparent text-fg-1 hover:bg-bg-2 hover:text-fg-0',
                )
              }
            >
              <Icon className="size-4 shrink-0" />
              <span className="truncate">{t.label}</span>
            </NavLink>
          );
        })}
      </div>
    </nav>
  );
}
