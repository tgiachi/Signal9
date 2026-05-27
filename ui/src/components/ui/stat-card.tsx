import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/cn';

const cardVariants = cva('rounded-[6px] p-3.5', {
  variants: {
    variant: {
      default: 'bg-bg-2 text-fg-1',
      live: 'bg-accent-live text-bg-5',
      warn: 'bg-accent-warn text-bg-0',
    },
  },
  defaultVariants: { variant: 'default' },
});

type StatCardProps = React.HTMLAttributes<HTMLDivElement> &
  VariantProps<typeof cardVariants> & {
    label: React.ReactNode;
    value: React.ReactNode;
    delta?: React.ReactNode;
  };

export function StatCard({ label, value, delta, variant, className, ...rest }: StatCardProps) {
  const isAccent = variant === 'live' || variant === 'warn';
  return (
    <div className={cn(cardVariants({ variant }), className)} {...rest}>
      <div
        className={cn(
          'text-[10px] font-bold uppercase tracking-label',
          isAccent ? 'opacity-80' : 'text-fg-3',
        )}
      >
        {label}
      </div>
      <div className="mt-1.5 font-mono text-[30px] font-bold leading-[1.05]">{value}</div>
      {delta ? (
        <div
          className={cn(
            'mt-1 font-mono text-[10px]',
            isAccent ? 'opacity-75' : 'text-accent-live',
          )}
        >
          {delta}
        </div>
      ) : null}
    </div>
  );
}
