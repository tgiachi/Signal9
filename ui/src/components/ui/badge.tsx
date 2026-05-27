import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/cn';

const badgeVariants = cva(
  'inline-flex items-center rounded-[3px] px-2 py-0.5 font-mono text-[9px] font-bold tracking-[0.08em] uppercase',
  {
    variants: {
      variant: {
        live: 'bg-accent-live text-bg-5',
        off: 'bg-bg-2 text-fg-3',
        err: 'bg-accent-err text-fg-0',
        warn: 'bg-accent-warn text-bg-0',
        queue: 'bg-accent-cfg text-fg-0',
        run: 'bg-accent-jobs text-fg-0',
        ok: 'bg-accent-live text-bg-5',
      },
    },
    defaultVariants: { variant: 'off' },
  },
);

export type BadgeVariant = NonNullable<VariantProps<typeof badgeVariants>['variant']>;

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { badgeVariants };
