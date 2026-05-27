import { Toaster as Sonner, type ToasterProps } from 'sonner';

export function Toaster(props: ToasterProps) {
  return (
    <Sonner
      theme="dark"
      position="bottom-right"
      toastOptions={{
        classNames: {
          toast: 'rounded-[6px] bg-bg-4 text-fg-1',
          title: 'text-[13px] font-semibold',
          description: 'text-[12px] text-fg-2',
          success: 'bg-accent-live text-bg-5',
          error: 'bg-accent-err text-fg-0',
          warning: 'bg-accent-warn text-bg-0',
          info: 'bg-accent-jobs text-fg-0',
          actionButton: 'bg-bg-0 text-fg-1',
          cancelButton: 'bg-bg-0 text-fg-2',
        },
      }}
      {...props}
    />
  );
}
