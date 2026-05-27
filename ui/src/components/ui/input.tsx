import * as React from 'react';
import { cn } from '@/lib/cn';

export type InputProps = React.InputHTMLAttributes<HTMLInputElement> & {
  invalid?: boolean;
};

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, invalid, ...props }, ref) => (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        'flex h-9 w-full rounded-[6px] bg-bg-1 px-3 py-2 text-[13px] text-fg-1 outline-none placeholder:text-fg-3 disabled:cursor-not-allowed disabled:opacity-40',
        'focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]',
        invalid && 'aria-invalid:[box-shadow:inset_0_0_0_2px_var(--accent-err)]',
        className,
      )}
      {...props}
    />
  ),
);
Input.displayName = 'Input';

export { Input };
