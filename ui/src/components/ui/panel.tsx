import * as React from 'react';
import { cn } from '@/lib/cn';

type PanelProps = React.HTMLAttributes<HTMLDivElement> & {
  title: React.ReactNode;
  counter?: number | string;
  action?: React.ReactNode;
};

export function Panel({ title, counter, action, className, children, ...rest }: PanelProps) {
  return (
    <section className={cn('overflow-hidden rounded-[6px] bg-bg-2', className)} {...rest}>
      <header className="flex items-center gap-2 bg-bg-4 px-3.5 py-2.5">
        <span className="text-[11px] font-bold uppercase tracking-label text-fg-2">{title}</span>
        {counter !== undefined ? (
          <span className="ml-auto rounded-[4px] bg-bg-0 px-2 py-0.5 font-mono text-[10px] text-fg-3">
            {counter}
          </span>
        ) : null}
        {action ? <div className="ml-auto">{action}</div> : null}
      </header>
      <div>{children}</div>
    </section>
  );
}
