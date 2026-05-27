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
    <section className="flex min-h-[22rem] flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2 lg:min-h-0">
      <header className="flex flex-wrap items-center gap-2 bg-bg-4 px-3 py-2">
        <div className="flex size-7 items-center justify-center rounded-[4px] bg-accent-live text-bg-5">
          <SignalHigh className="size-4" />
        </div>
        <div className="min-w-0">
          <h1 className="text-sm font-semibold text-fg-0">Channels</h1>
          <p className="font-mono text-[10px] text-fg-3">
            {channels.length} configured · sorted by display order
          </p>
        </div>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={onCreateNew}
            className="inline-flex items-center gap-2 rounded-[6px] bg-accent-live px-2.5 py-1.5 text-[12px] font-semibold text-bg-5 transition hover:bg-accent-live-hover"
          >
            <Plus className="size-3.5" />
            New Channel
          </button>
          <button
            type="button"
            onClick={onRefresh}
            className="inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0"
          >
            <RefreshCw className="size-3.5" />
            Refresh
          </button>
        </div>
      </header>

      <div className="bg-bg-2 p-3">
        <label className="flex items-center gap-2 rounded-[6px] bg-bg-1 px-2.5 py-2 focus-within:[box-shadow:inset_0_0_0_2px_var(--accent-live)]">
          <Search className="size-4 text-fg-3" />
          <input
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
            placeholder="Search name or slug"
            className="min-w-0 flex-1 bg-transparent text-[12px] text-fg-1 outline-none placeholder:text-fg-3"
          />
        </label>
      </div>

      <div className="grid gap-2 bg-bg-0 p-3 lg:hidden">
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

      <div className="hidden min-h-0 flex-1 overflow-auto bg-bg-0 lg:block">
        <div className="grid min-w-[68rem] grid-cols-[4rem_minmax(15rem,1.35fr)_minmax(12rem,1fr)_minmax(18rem,1.6fr)_7rem_minmax(11rem,1fr)_6rem] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
          <span>Order</span>
          <span>Channel</span>
          <span>Slug</span>
          <span>Description</span>
          <span>Status</span>
          <span>Commercials</span>
          <span>Open</span>
        </div>

        {isLoading ? (
          <div className="flex h-56 items-center justify-center text-sm text-fg-3">
            Loading channels.
          </div>
        ) : isError ? (
          <div className="flex h-56 items-center justify-center text-sm text-accent-err">
            Failed to load channels.
          </div>
        ) : channels.length === 0 ? (
          <div className="flex h-56 items-center justify-center text-sm text-fg-3">
            No channels match this view.
          </div>
        ) : (
          channels.map((channel, idx) => (
            <button
              key={channel.id}
              type="button"
              onClick={() => onSelect(channel)}
              className={cn(
                'grid min-w-[68rem] grid-cols-[4rem_minmax(15rem,1.35fr)_minmax(12rem,1fr)_minmax(18rem,1.6fr)_7rem_minmax(11rem,1fr)_6rem] gap-3 px-3 py-3 text-left transition',
                selectedId === channel.id
                  ? 'bg-accent-live text-bg-5'
                  : idx % 2
                    ? 'bg-bg-3 text-fg-1 hover:bg-[#343b41] hover:text-fg-0'
                    : 'bg-bg-2 text-fg-1 hover:bg-[#343b41] hover:text-fg-0',
              )}
            >
              <span className="font-mono text-[12px]">{channel.displayOrder}</span>
              <span className="flex min-w-0 items-center gap-3">
                <ChannelLogo channel={channel} />
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold">{channel.name}</span>
                  <span className="block truncate text-[12px] opacity-70">
                    {channel.logoUrl ?? 'No logo'}
                  </span>
                </span>
              </span>
              <span className="min-w-0 break-words font-mono text-[12px]">{channel.slug}</span>
              <span className="min-w-0 text-[12px] leading-5 opacity-90">
                {channel.description ?? 'No description'}
              </span>
              <StatusPill active={channel.isActive} />
              <span className="font-mono text-[12px]">
                {channel.commercialsEnabled
                  ? `${channel.commercialIntervalMinSeconds}-${channel.commercialIntervalMaxSeconds}s`
                  : 'disabled'}
              </span>
              <span className="inline-flex items-center gap-1 font-mono text-[11px] uppercase tracking-label">
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
  if (isLoading)
    return <div className="py-8 text-center text-sm text-fg-3">Loading channels.</div>;
  if (isError)
    return (
      <div className="py-8 text-center text-sm text-accent-err">Failed to load channels.</div>
    );
  return <div className="py-8 text-center text-sm text-fg-3">No channels match this view.</div>;
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
        'rounded-[6px] p-3 text-left transition',
        selected ? 'bg-accent-live text-bg-5' : 'bg-bg-2 hover:bg-[#343b41]',
      )}
    >
      <div className="flex min-w-0 items-start gap-3">
        <ChannelLogo channel={channel} />
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold">{channel.name}</div>
          <div className="truncate font-mono text-[11px] opacity-70">{channel.slug}</div>
          <div className="mt-1 text-[12px] leading-5 opacity-90">
            {channel.description ?? 'No description'}
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <StatusPill active={channel.isActive} />
            <span className="rounded-[3px] bg-bg-3 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-2">
              order {channel.displayOrder}
            </span>
            <span className="rounded-[3px] bg-bg-3 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-fg-2">
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
      <span className="flex size-9 shrink-0 overflow-hidden rounded-[4px] bg-bg-3">
        <img src={channel.logoUrl} alt="" className="size-full object-cover" />
      </span>
    );
  }

  return (
    <span className="flex size-9 shrink-0 items-center justify-center rounded-[4px] bg-accent-live font-mono text-[12px] font-semibold text-bg-5">
      {channel.name.trim().slice(0, 2).toUpperCase() || 'S9'}
    </span>
  );
}

function StatusPill({ active }: { active: boolean }) {
  return (
    <span
      className={cn(
        'w-fit rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-[0.08em]',
        active ? 'bg-accent-live text-bg-5' : 'bg-accent-warn text-bg-0',
      )}
    >
      {active ? 'active' : 'paused'}
    </span>
  );
}
