import { useEffect, useMemo, useState } from 'react';
import { Film, Filter, Play, RefreshCw, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/lib/api';
import {
  CHANNEL_MEDIA_TYPE_OPTIONS,
  MEDIA_SOURCE_TYPE_OPTIONS,
} from '@/features/media-libraries/media-library-types';
import {
  formatDuration,
  mediaTypeLabel,
  previewUrl,
  sourceTypeLabel,
  type ChannelMediaResponse,
} from './channel-media-types';
import { useChannelMedia } from './use-channel-media';

export function ChannelMediaPage() {
  const channelMedia = useChannelMedia();
  const [typeFilter, setTypeFilter] = useState<'all' | string>('all');
  const [sourceFilter, setSourceFilter] = useState<'all' | string>('all');
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');

  const filtered = useMemo(
    () =>
      channelMedia.media.filter((media) => {
        if (typeFilter !== 'all' && media.type !== Number(typeFilter)) return false;
        if (sourceFilter !== 'all' && media.sourceType !== Number(sourceFilter)) return false;
        if (activeFilter === 'active' && !media.isActive) return false;
        if (activeFilter === 'inactive' && media.isActive) return false;
        return true;
      }),
    [activeFilter, channelMedia.media, sourceFilter, typeFilter],
  );

  const runPipeline = async (media: ChannelMediaResponse) => {
    try {
      const job = await channelMedia.runPipeline(media.id);
      toast.success(`Media pipeline enqueued: ${job.id.slice(0, 8)}`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (!channelMedia.authenticated) {
    return <AuthRequired />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3 xl:overflow-hidden">
      <div className="grid gap-3 md:grid-cols-3">
        <SummaryMetric label="Media items" value={String(channelMedia.media.length)} />
        <SummaryMetric
          label="Active"
          value={String(channelMedia.media.filter((media) => media.isActive).length)}
        />
        <SummaryMetric
          label="With duration"
          value={String(channelMedia.media.filter((media) => media.durationSeconds !== null).length)}
        />
      </div>

      <section className="flex min-h-[30rem] flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex flex-wrap items-center gap-3 bg-bg-4 px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-[4px] bg-accent-jobs text-fg-0">
            <Film className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Media Catalog</h1>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              {filtered.length} visible
            </p>
          </div>
          <button
            type="button"
            onClick={() => {
              void channelMedia.refresh();
            }}
            className="ml-auto inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0"
          >
            <RefreshCw className="size-3.5" />
            Refresh
          </button>
        </header>

        <FilterBar
          typeFilter={typeFilter}
          sourceFilter={sourceFilter}
          activeFilter={activeFilter}
          onTypeFilterChange={setTypeFilter}
          onSourceFilterChange={setSourceFilter}
          onActiveFilterChange={setActiveFilter}
        />

        <div className="min-h-0 flex-1 overflow-auto bg-bg-0">
          <div className="grid min-w-[74rem] grid-cols-[6rem_minmax(18rem,1.4fr)_10rem_7rem_minmax(16rem,1.1fr)_8rem_10rem] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
            <span>Preview</span>
            <span>Title</span>
            <span>Media type</span>
            <span>Duration</span>
            <span>Source</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {channelMedia.isLoading ? (
            <EmptyState text="Loading media catalog." />
          ) : channelMedia.isError ? (
            <EmptyState text={errorMessage(channelMedia.error)} tone="error" />
          ) : filtered.length === 0 ? (
            <EmptyState text="No channel media match this view." />
          ) : (
            filtered.map((media, idx) => (
              <MediaRow
                key={media.id}
                media={media}
                index={idx}
                onRunPipeline={runPipeline}
                isRunning={channelMedia.isRunningPipeline}
              />
            ))
          )}
        </div>
      </section>
    </div>
  );
}

function MediaRow({
  media,
  index,
  onRunPipeline,
  isRunning,
}: {
  media: ChannelMediaResponse;
  index: number;
  onRunPipeline: (media: ChannelMediaResponse) => void;
  isRunning: boolean;
}) {
  return (
    <div
      className={
        'grid min-w-[74rem] grid-cols-[6rem_minmax(18rem,1.4fr)_10rem_7rem_minmax(16rem,1.1fr)_8rem_10rem] items-center gap-3 px-3 py-3 ' +
        (index % 2 ? 'bg-bg-3' : 'bg-bg-2')
      }
    >
      <PreviewCarousel mediaId={media.id} title={media.title} />
      <div className="min-w-0">
        <div className="truncate text-sm font-semibold text-fg-0">{media.title}</div>
        <div className="truncate text-[12px] text-fg-3">{typeMetadata(media)}</div>
      </div>
      <span className="text-[12px] text-fg-1">{mediaTypeLabel(media.type)}</span>
      <span className="font-mono text-[12px] text-fg-2">
        {formatDuration(media.durationSeconds)}
      </span>
      <span className="break-all font-mono text-[12px] text-fg-2">
        {sourceTypeLabel(media.sourceType)} · {media.sourceRef ?? 'none'}
      </span>
      <span
        className={
          'w-fit rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-label ' +
          (media.isActive ? 'bg-accent-live text-bg-5' : 'bg-accent-warn text-bg-0')
        }
      >
        {media.isActive ? 'active' : 'paused'}
      </span>
      <button
        type="button"
        aria-label={`Run pipeline for ${media.title}`}
        disabled={!media.isActive || isRunning}
        onClick={() => onRunPipeline(media)}
        className="inline-flex w-fit items-center gap-1 rounded-[6px] bg-accent-jobs px-2.5 py-1.5 text-[12px] font-semibold text-fg-0 transition hover:opacity-90 disabled:bg-bg-1 disabled:text-fg-3 disabled:opacity-40"
      >
        <Play className="size-3" />
        Pipeline
      </button>
    </div>
  );
}

function FilterBar({
  typeFilter,
  sourceFilter,
  activeFilter,
  onTypeFilterChange,
  onSourceFilterChange,
  onActiveFilterChange,
}: {
  typeFilter: string;
  sourceFilter: string;
  activeFilter: string;
  onTypeFilterChange: (value: string) => void;
  onSourceFilterChange: (value: string) => void;
  onActiveFilterChange: (value: 'all' | 'active' | 'inactive') => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 bg-bg-0 p-3">
      <div className="flex items-center gap-2 text-fg-3">
        <Filter className="size-4" />
        <span className="font-mono text-[10px] uppercase tracking-label">Filters</span>
      </div>
      <select
        aria-label="Media type filter"
        value={typeFilter}
        onChange={(event) => onTypeFilterChange(event.target.value)}
        className="rounded-[6px] bg-bg-2 px-2 py-1.5 text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
      >
        <option value="all">All media types</option>
        {CHANNEL_MEDIA_TYPE_OPTIONS.map((item) => (
          <option key={item.value} value={String(item.value)}>
            {item.label}
          </option>
        ))}
      </select>
      <select
        aria-label="Source type filter"
        value={sourceFilter}
        onChange={(event) => onSourceFilterChange(event.target.value)}
        className="rounded-[6px] bg-bg-2 px-2 py-1.5 text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
      >
        <option value="all">All sources</option>
        {MEDIA_SOURCE_TYPE_OPTIONS.map((item) => (
          <option key={item.value} value={String(item.value)}>
            {item.label}
          </option>
        ))}
      </select>
      <select
        aria-label="Active media filter"
        value={activeFilter}
        onChange={(event) =>
          onActiveFilterChange(event.target.value as 'all' | 'active' | 'inactive')
        }
        className="rounded-[6px] bg-bg-2 px-2 py-1.5 text-[12px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
      >
        <option value="all">All states</option>
        <option value="active">Active</option>
        <option value="inactive">Inactive</option>
      </select>
    </div>
  );
}

const MAX_PREVIEW_COUNT = 5;
const PREVIEW_ROTATE_MS = 300;

function PreviewCarousel({ mediaId, title }: { mediaId: string; title: string }) {
  const [index, setIndex] = useState(1);
  const [hovering, setHovering] = useState(false);
  const [invalid, setInvalid] = useState<Set<number>>(() => new Set());

  useEffect(() => {
    if (!hovering) return undefined;
    const id = window.setInterval(() => {
      setIndex((prev) => {
        let candidate = prev;
        for (let i = 0; i < MAX_PREVIEW_COUNT; i++) {
          candidate = (candidate % MAX_PREVIEW_COUNT) + 1;
          if (!invalid.has(candidate)) return candidate;
        }
        return prev;
      });
    }, PREVIEW_ROTATE_MS);
    return () => window.clearInterval(id);
  }, [hovering, invalid]);

  const allInvalid = invalid.size >= MAX_PREVIEW_COUNT;

  return (
    <div
      data-testid="preview-carousel"
      className="group relative h-12 w-20 overflow-hidden rounded-[4px] bg-bg-1"
      onMouseEnter={() => setHovering(true)}
      onMouseLeave={() => {
        setHovering(false);
        setIndex(1);
      }}
    >
      {allInvalid ? (
        <div className="flex size-full items-center justify-center font-mono text-[9px] text-fg-3">
          no preview
        </div>
      ) : (
        <img
          src={previewUrl(mediaId, index)}
          alt={`${title} preview ${index}`}
          loading="lazy"
          decoding="async"
          className="size-full object-cover"
          onError={() => {
            setInvalid((prev) => {
              if (prev.has(index)) return prev;
              const next = new Set(prev);
              next.add(index);
              return next;
            });
          }}
        />
      )}
      {hovering && !allInvalid && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 flex justify-center gap-0.5 bg-gradient-to-t from-bg-5/80 to-transparent px-1 py-0.5">
          {Array.from({ length: MAX_PREVIEW_COUNT }, (_, i) => i + 1).map((n) => (
            <span
              key={n}
              className={
                'h-0.5 w-2 rounded-full ' +
                (n === index
                  ? 'bg-accent-live'
                  : invalid.has(n)
                    ? 'bg-bg-2 opacity-30'
                    : 'bg-fg-3 opacity-50')
              }
            />
          ))}
        </div>
      )}
    </div>
  );
}

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <section className="min-w-0 rounded-[6px] bg-bg-2 p-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-lg font-semibold text-fg-0">{value}</div>
    </section>
  );
}

function EmptyState({ text, tone = 'muted' }: { text: string; tone?: 'muted' | 'error' }) {
  return (
    <div
      className={
        'flex h-56 items-center justify-center text-sm ' +
        (tone === 'error' ? 'text-accent-err' : 'text-fg-3')
      }
    >
      {text}
    </div>
  );
}

function AuthRequired() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <div className="max-w-md rounded-[6px] bg-bg-2 p-5 text-center">
        <ShieldAlert className="mx-auto mb-3 size-8 text-accent-warn" />
        <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
        <p className="mt-2 text-sm text-fg-2">
          Media catalog endpoints require an authenticated session.
        </p>
      </div>
    </div>
  );
}

function typeMetadata(media: ChannelMediaResponse): string {
  if (media.type === 3) {
    return [media.movieReleaseYear, media.movieDirector].filter(Boolean).join(' · ') || 'Movie';
  }
  if (media.type === 1) {
    const episode =
      media.tvSeason !== null && media.tvEpisode !== null
        ? `S${media.tvSeason}E${media.tvEpisode}`
        : null;
    return [media.tvSeriesName, episode].filter(Boolean).join(' · ') || 'TV episode';
  }
  if (media.type === 0) {
    return [media.commercialAdvertiser, media.commercialCampaign].filter(Boolean).join(' · ') ||
      'Commercial';
  }
  if (media.type === 4) {
    return media.informationEdition ?? 'Information';
  }
  return 'Bumper';
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409 && typeof error.body === 'string') return error.body;
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'Media request failed.';
}
