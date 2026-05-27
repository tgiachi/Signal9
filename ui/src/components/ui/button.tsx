import * as React from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/cn';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-[6px] text-[12px] font-semibold transition-colors focus-visible:outline-none focus-visible:[box-shadow:inset_0_0_0_2px_var(--accent-live)] disabled:pointer-events-none disabled:opacity-40 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        primary: 'bg-accent-live text-bg-5 hover:bg-accent-live-hover',
        ghost: 'bg-bg-2 text-fg-1 hover:bg-[#343b41]',
        danger: 'bg-accent-err text-fg-0 hover:opacity-90',
        icon: 'bg-bg-2 text-fg-2 hover:bg-[#343b41] hover:text-fg-1',
      },
      size: {
        default: 'h-9 px-4',
        sm: 'h-8 px-3 text-[11px]',
        lg: 'h-10 px-6',
        icon: 'h-7 w-7 [&_svg]:size-3.5',
      },
    },
    defaultVariants: { variant: 'primary', size: 'default' },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return (
      <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
    );
  },
);
Button.displayName = 'Button';

export { Button, buttonVariants };
