import { NavLink } from 'react-router';
import { cn } from '@/lib/cn';

const TABS = [
  { to: '/logs', label: 'Logs' },
  { to: '/config', label: 'Config' },
] as const;

export function NavTabs() {
  return (
    <nav className="flex h-9 items-end gap-4 border-b border-border-subtle bg-bg-1 px-3">
      {TABS.map((t) => (
        <NavLink
          key={t.to}
          to={t.to}
          end
          className={({ isActive }) =>
            cn(
              'pb-1.5 text-[11px] font-medium uppercase tracking-label transition-colors',
              isActive
                ? 'border-b-2 border-on-air-2 text-on-air-2'
                : 'text-fg-1 hover:text-fg-0',
            )
          }
        >
          {t.label}
        </NavLink>
      ))}
    </nav>
  );
}
