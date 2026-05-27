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
    <nav className="flex shrink-0 flex-col bg-bg-1 md:w-[168px] max-md:order-last max-md:h-16 max-md:w-full">
      <div className="hidden h-16 items-center gap-2.5 px-3 md:flex">
        <div className="flex size-9 items-center justify-center rounded-[6px] bg-accent-live text-bg-5 font-bold text-[13px] tracking-[0.05em]">
          S9
        </div>
        <div className="min-w-0 leading-[1.1]">
          <div className="text-[13px] font-bold tracking-brand text-fg-1">SIGNAL</div>
          <div className="text-[13px] font-bold tracking-brand text-accent-live">NINE</div>
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
                  'flex items-center gap-2.5 rounded-[6px] px-2.5 py-2 text-[12px] transition-colors md:justify-start max-md:flex-col max-md:justify-center max-md:gap-1 max-md:px-1 max-md:py-1',
                  isActive
                    ? 'bg-accent-live text-bg-5 font-semibold'
                    : 'text-fg-3 hover:bg-bg-2 hover:text-fg-1',
                )
              }
            >
              <Icon className="size-3.5 shrink-0" />
              <span className="truncate">{t.label}</span>
            </NavLink>
          );
        })}
      </div>
    </nav>
  );
}
