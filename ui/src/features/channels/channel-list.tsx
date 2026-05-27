import { Eye, Plus, RefreshCw, Search, SignalHigh } from 'lucide-react';
import type { ChannelResponse } from './channel-types';
import { cn } from '@/lib/cn';

type Props = {
  channels: ChannelResponse[];
  selectedId: string | null;
  query: string;
  isLoading: boolean;
  isError: boolean;
  onQueryChange: (value: string) => void;
  onCreateNew: () => void;
  onRefresh: () => void;
  onSelect: (channel: ChannelResponse) => void;
};

export function ChannelList({
  channels,
  selectedId,
  query,
  isLoading,
  isError,
  onQueryChange,
  onCreateNew,
  onRefresh,
  onSelect,
}: Props) {
  return (
    <section className="flex min-h-[22rem] flex-1 flex-col overflow-hidden rounded-lg border border-border bg-panel lg:min-h-0">
      <header className="flex flex-wrap items-center gap-2 border-b border-border-subtle bg-panel-strong px-3 py-2">
        <div className="flex size-7 items-center justify-center rounded-md border border-on-air/30 bg-on-air/10 text-on-air-2">
          <SignalHigh className="size-4" />
        </div>
        <div className="min-w-0">
          <h1 className="text-sm font-semibold text-fg-0">Channels</h1>
          <p className="font-mono text-[10px] text-fg-2">
            {channels.length} configured · sorted by display order
          </p>
        </div>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={onCreateNew}
            className="inline-flex items-center gap-2 rounded-md border border-on-air/40 bg-on-air/10 px-2.5 py-1.5 text-[12px] font-semibold text-on-air-2 transition hover:bg-on-air/15"
          >
            <Plus className="size-3.5" />
            New Channel
          </button>
          <button
            type="button"
            onClick={onRefresh}
            className="inline-flex items-center gap-2 rounded-md border border-border bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:border-on-air/40 hover:text-fg-0"
          >
            <RefreshCw className="size-3.5" />
            Refresh
          </button>
        </div>
      </header>

      <div className="border-b border-border-subtle p-3">
        <label className="flex items-center gap-2 rounded-md border border-border bg-bg-1 px-2.5 py-2 focus-within:border-on-air">
          <Search className="size-4 text-fg-2" />
          <input
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
            placeholder="Search name or slug"
            className="min-w-0 flex-1 bg-transparent text-[12px] text-fg-0 outline-none placeholder:text-fg-2"
          />
        </label>
      </div>

      <div className="grid gap-2 bg-bg-1 p-3 lg:hidden">
        {isLoading || isError || channels.length === 0 ? (
          <MobileState isLoading={isLoading} isError={isError} />
        ) : (
          channels.map((channel) => (
            <ChannelCard
              key={channel.id}
              channel={channel}
              selected={selectedId === channel.id}
              onSelect={onSelect}
            />
          ))
        )}
      </div>

      <div className="hidden min-h-0 flex-1 overflow-auto bg-bg-1 lg:block">
        <div className="grid min-w-[68rem] grid-cols-[4rem_minmax(15rem,1.35fr)_minmax(12rem,1fr)_minmax(18rem,1.6fr)_7rem_minmax(11rem,1fr)_6rem] gap-3 border-b border-border-subtle bg-bg-2 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-2">
          <span>Order</span>
          <span>Channel</span>
          <span>Slug</span>
          <span>Description</span>
          <span>Status</span>
          <span>Commercials</span>
          <span>Open</span>
        </div>

        {isLoading ? (
          <div className="flex h-56 items-center justify-center text-sm text-fg-2">
            Loading channels.
          </div>
        ) : isError ? (
          <div className="flex h-56 items-center justify-center text-sm text-error">
            Failed to load channels.
          </div>
        ) : channels.length === 0 ? (
          <div className="flex h-56 items-center justify-center text-sm text-fg-2">
            No channels match this view.
          </div>
        ) : (
          channels.map((channel) => (
            <button
              key={channel.id}
              type="button"
              onClick={() => onSelect(channel)}
              className={cn(
                'grid min-w-[68rem] grid-cols-[4rem_minmax(15rem,1.35fr)_minmax(12rem,1fr)_minmax(18rem,1.6fr)_7rem_minmax(11rem,1fr)_6rem] gap-3 border-b border-border-subtle px-3 py-3 text-left transition',
                selectedId === channel.id
                  ? 'bg-on-air/10 text-fg-0'
                  : 'bg-bg-1 text-fg-1 hover:bg-bg-2 hover:text-fg-0',
              )}
            >
              <span className="font-mono text-[12px] text-fg-1">{channel.displayOrder}</span>
              <span className="flex min-w-0 items-center gap-3">
                <ChannelLogo channel={channel} />
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold text-fg-0">
                    {channel.name}
                  </span>
                  <span className="block truncate text-[12px] text-fg-1">
                    {channel.logoUrl ?? 'No logo'}
                  </span>
                </span>
              </span>
              <span className="min-w-0 break-words font-mono text-[12px] text-fg-0">
                {channel.slug}
              </span>
              <span className="min-w-0 text-[12px] leading-5 text-fg-1">
                {channel.description ?? 'No description'}
              </span>
              <StatusPill active={channel.isActive} />
              <span className="font-mono text-[12px] text-fg-0">
                {channel.commercialsEnabled
                  ? `${channel.commercialIntervalMinSeconds}-${channel.commercialIntervalMaxSeconds}s`
                  : 'disabled'}
              </span>
              <span className="inline-flex items-center gap-1 font-mono text-[11px] uppercase tracking-label text-on-air-2">
                <Eye className="size-3.5" />
                Edit
              </span>
            </button>
          ))
        )}
      </div>
    </section>
  );
}

function MobileState({ isLoading, isError }: { isLoading: boolean; isError: boolean }) {
  if (isLoading) return <div className="py-8 text-center text-sm text-fg-2">Loading channels.</div>;
  if (isError)
    return <div className="py-8 text-center text-sm text-error">Failed to load channels.</div>;
  return <div className="py-8 text-center text-sm text-fg-2">No channels match this view.</div>;
}

function ChannelCard({
  channel,
  selected,
  onSelect,
}: {
  channel: ChannelResponse;
  selected: boolean;
  onSelect: (channel: ChannelResponse) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onSelect(channel)}
      className={cn(
        'rounded-md border p-3 text-left transition',
        selected
          ? 'border-on-air/40 bg-on-air/10'
          : 'border-border-subtle bg-bg-1 hover:border-border hover:bg-bg-2',
      )}
    >
      <div className="flex min-w-0 items-start gap-3">
        <ChannelLogo channel={channel} />
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold text-fg-0">{channel.name}</div>
          <div className="truncate font-mono text-[11px] text-fg-2">{channel.slug}</div>
          <div className="mt-1 text-[12px] leading-5 text-fg-1">
            {channel.description ?? 'No description'}
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <StatusPill active={channel.isActive} />
            <span className="rounded border border-border bg-bg-2 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-1">
              order {channel.displayOrder}
            </span>
            <span className="rounded border border-border bg-bg-2 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-1">
              {channel.commercialsEnabled
                ? `${channel.commercialIntervalMinSeconds}-${channel.commercialIntervalMaxSeconds}s ads`
                : 'ads off'}
            </span>
          </div>
        </div>
      </div>
    </button>
  );
}

function ChannelLogo({ channel }: { channel: ChannelResponse }) {
  if (channel.logoUrl) {
    return (
      <span className="flex size-9 shrink-0 overflow-hidden rounded-md border border-border bg-bg-2">
        <img src={channel.logoUrl} alt="" className="size-full object-cover" />
      </span>
    );
  }

  return (
    <span className="flex size-9 shrink-0 items-center justify-center rounded-md border border-on-air/30 bg-on-air/10 font-mono text-[12px] font-semibold text-on-air-2">
      {channel.name.trim().slice(0, 2).toUpperCase() || 'S9'}
    </span>
  );
}

function StatusPill({ active }: { active: boolean }) {
  return (
    <span
      className={cn(
        'w-fit rounded border px-2 py-1 font-mono text-[10px] uppercase tracking-label',
        active
          ? 'border-on-air/40 bg-on-air/10 text-on-air-2'
          : 'border-warn/40 bg-warn/10 text-warn',
      )}
    >
      {active ? 'active' : 'paused'}
    </span>
  );
}
