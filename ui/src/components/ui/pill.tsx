import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/cn';

const pillVariants = cva(
  'inline-flex items-center gap-1.5 rounded-[4px] px-2.5 py-1 font-mono text-[10px] font-semibold tracking-label uppercase',
  {
    variants: {
      variant: {
        live: 'bg-accent-live text-bg-5',
        jobs: 'bg-accent-jobs text-fg-0',
        cfg: 'bg-accent-cfg text-fg-0',
        warn: 'bg-accent-warn text-bg-0',
        err: 'bg-accent-err text-fg-0',
        health: 'bg-bg-2 text-fg-2',
      },
    },
    defaultVariants: { variant: 'health' },
  },
);

export type PillVariant = NonNullable<VariantProps<typeof pillVariants>['variant']>;

type PillProps = React.HTMLAttributes<HTMLSpanElement> &
  VariantProps<typeof pillVariants> & {
    dot?: boolean;
  };

export function Pill({ className, variant, dot, children, ...rest }: PillProps) {
  return (
    <span className={cn(pillVariants({ variant }), className)} {...rest}>
      {dot ? (
        <span data-testid="pill-dot" className="size-1.5 rounded-full bg-current" />
      ) : null}
      {children}
    </span>
  );
}
