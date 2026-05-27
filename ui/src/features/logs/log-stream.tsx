import { Virtuoso } from 'react-virtuoso';
import { LogRow } from './log-row';
import type { LogEntry } from './log-entry';

export function LogStream({ entries }: { entries: LogEntry[] }) {
  return (
    <Virtuoso
      data={entries}
      followOutput="smooth"
      atBottomThreshold={50}
      itemContent={(_, entry) => <LogRow entry={entry} />}
      className="h-full bg-bg-1"
    />
  );
}
