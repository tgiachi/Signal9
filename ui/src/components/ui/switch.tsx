import * as React from 'react';
import * as SwitchPrimitive from '@radix-ui/react-switch';
import { cn } from '@/lib/cn';

const Switch = React.forwardRef<
  React.ElementRef<typeof SwitchPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof SwitchPrimitive.Root>
>(({ className, ...props }, ref) => (
  <SwitchPrimitive.Root
    ref={ref}
    className={cn(
      'peer inline-flex h-[18px] w-8 shrink-0 cursor-pointer items-center rounded-full transition-colors focus-visible:outline-none disabled:opacity-40',
      'data-[state=checked]:bg-accent-live data-[state=unchecked]:bg-bg-2',
      className,
    )}
    {...props}
  >
    <SwitchPrimitive.Thumb
      className={cn(
        'pointer-events-none block size-3.5 rounded-full bg-bg-5 transition-transform',
        'data-[state=checked]:translate-x-[14px] data-[state=unchecked]:translate-x-0.5',
      )}
    />
  </SwitchPrimitive.Root>
));
Switch.displayName = SwitchPrimitive.Root.displayName;

export { Switch };
