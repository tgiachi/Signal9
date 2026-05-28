// ui/src/components/ui/directory-picker.tsx
import { useEffect, useState } from 'react';
import { ChevronUp, File as FileIcon, Folder } from 'lucide-react';
import {
  Dialog,
  DialogBody,
  DialogCloseIcon,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from './dialog';
import { Button } from './button';
import { Switch } from './switch';
import { useFsBrowse } from '@/features/filesystem/use-fs-browse';
import { cn } from '@/lib/cn';

type Props = {
  open: boolean;
  initialPath?: string;
  onOpenChange: (open: boolean) => void;
  onSelect: (path: string) => void;
};

export function DirectoryPicker({ open, initialPath = '/', onOpenChange, onSelect }: Props) {
  const [path, setPath] = useState(initialPath);
  const [showHidden, setShowHidden] = useState(false);

  useEffect(() => {
    if (open) {
      setPath(initialPath || '/');
      setShowHidden(false);
    }
  }, [open, initialPath]);

  const query = useFsBrowse(open ? path : null);

  const visibleEntries = (query.data?.entries ?? []).filter(
    (e) => showHidden || !e.name.startsWith('.'),
  );

  const segments = splitPath(query.data?.path ?? path);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Select directory</DialogTitle>
          <DialogCloseIcon />
        </DialogHeader>
        <DialogBody>
          <div className="flex flex-wrap items-center gap-1 rounded-[6px] bg-bg-1 px-2 py-2 font-mono text-[11px] text-fg-2">
            {segments.map((seg) => (
              <button
                key={seg.path}
                type="button"
                onClick={() => setPath(seg.path)}
                className="rounded-[3px] px-1.5 py-0.5 transition hover:bg-bg-2 hover:text-fg-1"
              >
                {seg.label}
              </button>
            ))}
            {segments.length > 1 && <span className="text-fg-3">/</span>}
          </div>

          <div className="flex items-center gap-3">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => query.data?.parent && setPath(query.data.parent)}
              disabled={!query.data?.parent}
            >
              <ChevronUp />
              Parent
            </Button>
            <label className="ml-auto flex items-center gap-2 text-[12px] text-fg-2">
              <span>Show hidden</span>
              <Switch
                checked={showHidden}
                onCheckedChange={setShowHidden}
                aria-label="Show hidden"
              />
            </label>
          </div>

          <div className="max-h-[420px] min-h-[200px] overflow-auto rounded-[6px] bg-bg-1">
            {query.isLoading ? (
              <div data-testid="dp-loading" className="space-y-1 p-2">
                <SkeletonRow />
                <SkeletonRow />
                <SkeletonRow />
              </div>
            ) : query.isError ? (
              <div className="px-3 py-6 text-center text-[12px] text-accent-err">
                {errorMessage(query.error)}
              </div>
            ) : visibleEntries.length === 0 ? (
              <div className="px-3 py-6 text-center text-[12px] text-fg-3">
                This folder is empty
              </div>
            ) : (
              visibleEntries.map((entry, idx) =>
                entry.isDirectory ? (
                  <button
                    key={entry.path}
                    type="button"
                    onClick={() => setPath(entry.path)}
                    className={cn(
                      'flex w-full items-center gap-2 px-3 py-1.5 text-left text-[12px] text-fg-1 transition hover:bg-bg-2',
                      idx % 2 ? 'bg-transparent' : 'bg-transparent',
                    )}
                  >
                    <Folder className="size-3.5 shrink-0 text-accent-live" />
                    <span className="truncate">{entry.name}</span>
                  </button>
                ) : (
                  <div
                    key={entry.path}
                    className="flex items-center gap-2 px-3 py-1.5 text-[12px] text-fg-3"
                  >
                    <FileIcon className="size-3.5 shrink-0" />
                    <span className="truncate">{entry.name}</span>
                  </div>
                ),
              )
            )}
          </div>
        </DialogBody>
        <DialogFooter>
          <div className="ml-auto flex gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => onOpenChange(false)}
            >
              Cancel
            </Button>
            <Button
              type="button"
              variant="primary"
              size="sm"
              onClick={() => {
                onSelect(query.data?.path ?? path);
                onOpenChange(false);
              }}
              disabled={query.isLoading || query.isError}
            >
              Select this folder
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function SkeletonRow() {
  return <div className="h-5 animate-pulse rounded-[3px] bg-bg-2" />;
}

function splitPath(p: string): Array<{ label: string; path: string }> {
  const trimmed = p.replace(/\/+$/g, '') || '/';
  if (trimmed === '/') return [{ label: '/', path: '/' }];
  const parts = trimmed.split('/').filter(Boolean);
  const segments: Array<{ label: string; path: string }> = [{ label: '/', path: '/' }];
  let acc = '';
  for (const part of parts) {
    acc += '/' + part;
    segments.push({ label: part, path: acc });
  }
  return segments;
}

function errorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  return 'Failed to read directory';
}
