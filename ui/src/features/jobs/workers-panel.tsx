import { Panel } from '@/components/ui/panel';
import { formatRelativeTime } from './format-relative-time';
import { useWorkers } from './use-workers';
import type { WorkerSnapshot } from './worker-types';

export function WorkersPanel() {
  const { data: workers = [], isLoading } = useWorkers();
  const online = workers.filter((w) => w.online).length;
  const counter = isLoading ? '…' : `${online}/${workers.length}`;

  return (
    <Panel title={`Workers · ${online} online`} counter={counter}>
      {workers.length === 0 ? (
        <div className="px-4 py-6 text-center text-[12px] text-fg-3">
          {isLoading ? 'Loading workers…' : 'No workers have registered yet.'}
        </div>
      ) : (
        workers.map((w, idx) => (
          <WorkerRow key={w.workerId} worker={w} index={idx} />
        ))
      )}
    </Panel>
  );
}

function WorkerRow({ worker, index }: { worker: WorkerSnapshot; index: number }) {
  return (
    <div className={`flex items-center gap-3 px-3.5 py-2.5 ${index % 2 ? 'bg-bg-3' : 'bg-bg-2'}`}>
      <span
        className={`size-2 rounded-full ${worker.online ? 'bg-accent-live' : 'bg-fg-3'}`}
        aria-label={worker.online ? 'online' : 'offline'}
      />
      <div className="min-w-0 flex-1">
        <div className="truncate text-[13px] font-medium text-fg-1">{worker.name}</div>
        <div className="truncate font-mono text-[10px] text-fg-3">
          v{worker.version} · last seen {formatRelativeTime(worker.lastSeenAt)}
        </div>
      </div>
      <span className="rounded-[3px] bg-bg-1 px-2 py-1 font-mono text-[10px] text-fg-2">
        {worker.runningJobs}/{worker.maxConcurrentJobs} jobs
      </span>
    </div>
  );
}
