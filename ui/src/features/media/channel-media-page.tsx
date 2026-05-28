import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Film,
  Filter,
  MoreVertical,
  Play,
  RefreshCw,
  Search,
  ShieldAlert,
} from 'lucide-react';
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
  streamUrl,
  type ChannelMediaResponse,
} from './channel-media-types';
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
  DialogCloseIcon,
  DialogHeader,
} from '@/components/ui/dialog';
import { useAuth } from '@/providers/auth-context';
import { useChannelMedia } from './use-channel-media';

export function ChannelMediaPage() {
  const channelMedia = useChannelMedia();
  const [typeFilter, setTypeFilter] = useState<'all' | string>('all');
  const [sourceFilter, setSourceFilter] = useState<'all' | string>('all');
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [query, setQuery] = useState('');
  const [pageSize, setPageSize] = useState<number>(25);
  const [page, setPage] = useState<number>(0);
  const [playing, setPlaying] = useState<ChannelMediaResponse | null>(null);

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return channelMedia.media.filter((media) => {
      if (typeFilter !== 'all' && media.type !== Number(typeFilter)) return false;
      if (sourceFilter !== 'all' && media.sourceType !== Number(sourceFilter)) return false;
      if (activeFilter === 'active' && !media.isActive) return false;
      if (activeFilter === 'inactive' && media.isActive) return false;
      if (needle === '') return true;
      return mediaMatchesQuery(media, needle);
    });
  }, [activeFilter, channelMedia.media, query, sourceFilter, typeFilter]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  // Clamp at render time — when filters shrink the result set below the current page, we
  // display the valid last page without needing a useEffect to mutate state.
  const clampedPage = Math.min(page, pageCount - 1);
  const paged = useMemo(
    () => filtered.slice(clampedPage * pageSize, (clampedPage + 1) * pageSize),
    [filtered, clampedPage, pageSize],
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
          query={query}
          onTypeFilterChange={setTypeFilter}
          onSourceFilterChange={setSourceFilter}
          onActiveFilterChange={setActiveFilter}
          onQueryChange={setQuery}
        />

        <div className="min-h-0 flex-1 overflow-auto bg-bg-0 p-4">
          {channelMedia.isLoading ? (
            <EmptyState text="Loading media catalog." />
          ) : channelMedia.isError ? (
            <EmptyState text={errorMessage(channelMedia.error)} tone="error" />
          ) : filtered.length === 0 ? (
            <EmptyState text="No channel media match this view." />
          ) : (
            <div className="grid grid-cols-[repeat(auto-fill,minmax(15rem,1fr))] gap-4">
              {paged.map((media) => (
                <MediaCard
                  key={media.id}
                  media={media}
                  onRunPipeline={runPipeline}
                  isRunning={channelMedia.isRunningPipeline}
                  onPlay={setPlaying}
                />
              ))}
            </div>
          )}
        </div>
        <PaginationBar
          page={clampedPage}
          pageCount={pageCount}
          pageSize={pageSize}
          total={filtered.length}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPage(0);
          }}
        />
      </section>
      <MediaPlayerDialog media={playing} onOpenChange={(open) => !open && setPlaying(null)} />
    </div>
  );
}

function PaginationBar({
  page,
  pageCount,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
}: {
  page: number;
  pageCount: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}) {
  const from = total === 0 ? 0 : page * pageSize + 1;
  const to = Math.min(total, (page + 1) * pageSize);
  return (
    <div className="flex flex-wrap items-center gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] text-fg-3">
      <span>
        {from}–{to} of {total}
      </span>
      <span className="ml-auto flex items-center gap-2">
        <span className="uppercase tracking-label">Page size</span>
        <select
          aria-label="Rows per page"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
          className="rounded-[6px] bg-bg-2 px-2 py-1 text-[11px] text-fg-1 outline-none focus:[box-shadow:inset_0_0_0_2px_var(--accent-live)]"
        >
          <option value={25}>25</option>
          <option value={50}>50</option>
          <option value={100}>100</option>
        </select>
      </span>
      <span className="flex items-center gap-1">
        <button
          type="button"
          aria-label="Previous page"
          disabled={page === 0}
          onClick={() => onPageChange(page - 1)}
          className="flex size-7 items-center justify-center rounded-[6px] bg-bg-2 text-fg-1 transition hover:bg-[#343b41] disabled:opacity-30"
        >
          <ChevronLeft className="size-3.5" />
        </button>
        <span className="min-w-[5rem] text-center uppercase tracking-label">
          page {page + 1}/{pageCount}
        </span>
        <button
          type="button"
          aria-label="Next page"
          disabled={page >= pageCount - 1}
          onClick={() => onPageChange(page + 1)}
          className="flex size-7 items-center justify-center rounded-[6px] bg-bg-2 text-fg-1 transition hover:bg-[#343b41] disabled:opacity-30"
        >
          <ChevronRight className="size-3.5" />
        </button>
      </span>
    </div>
  );
}

function MediaPlayerDialog({
  media,
  onOpenChange,
}: {
  media: ChannelMediaResponse | null;
  onOpenChange: (open: boolean) => void;
}) {
  const auth = useAuth();
  const open = media !== null;
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl bg-bg-5 p-0">
        <DialogHeader>
          <DialogTitle className="truncate">{media?.title ?? ''}</DialogTitle>
          <DialogCloseIcon />
        </DialogHeader>
        <DialogDescription className="sr-only">
          Inline video player for the selected media item.
        </DialogDescription>
        {open && media ? (
          <video
            key={media.id}
            controls
            autoPlay
            preload="metadata"
            src={streamUrl(media.id, auth.token)}
            className="aspect-video w-full bg-black"
          />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function mediaMatchesQuery(media: ChannelMediaResponse, needle: string): boolean {
  const haystack: Array<string | null | undefined> = [
    media.title,
    media.sourceRef,
    media.movieDirector,
    media.tvSeriesName,
    media.commercialAdvertiser,
    media.commercialCampaign,
    media.informationEdition,
    mediaTypeLabel(media.type),
    sourceTypeLabel(media.sourceType),
    ...media.tags.flatMap((tag) => [tag.name, tag.label]),
  ];
  for (const value of haystack) {
    if (value && value.toLowerCase().includes(needle)) return true;
  }
  return false;
}

function MediaCardMenu({
  media,
  isRunning,
  onRunPipeline,
}: {
  media: ChannelMediaResponse;
  isRunning: boolean;
  onRunPipeline: (media: ChannelMediaResponse) => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return undefined;
    const handler = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('mousedown', handler);
    window.addEventListener('keydown', escape);
    return () => {
      window.removeEventListener('mousedown', handler);
      window.removeEventListener('keydown', escape);
    };
  }, [open]);

  const pipelineDisabled = !media.isActive || isRunning;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-label={`More actions for ${media.title}`}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((prev) => !prev)}
        className="flex size-7 items-center justify-center rounded-[6px] bg-bg-3 text-fg-2 transition hover:bg-[#343b41] hover:text-fg-0"
      >
        <MoreVertical className="size-3.5" />
      </button>
      {open && (
        <div
          role="menu"
          className="absolute bottom-full right-0 z-20 mb-1 min-w-[10rem] overflow-hidden rounded-[6px] bg-bg-4"
        >
          <button
            type="button"
            role="menuitem"
            disabled={pipelineDisabled}
            onClick={() => {
              setOpen(false);
              onRunPipeline(media);
            }}
            className="flex w-full items-center gap-2 px-3 py-2 text-left text-[12px] text-fg-1 transition hover:bg-bg-2 disabled:text-fg-3 disabled:hover:bg-transparent"
          >
            <Play className="size-3.5 text-accent-jobs" />
            Pipeline
          </button>
        </div>
      )}
    </div>
  );
}

function MediaCard({
  media,
  onRunPipeline,
  isRunning,
  onPlay,
}: {
  media: ChannelMediaResponse;
  onRunPipeline: (media: ChannelMediaResponse) => void;
  isRunning: boolean;
  onPlay: (media: ChannelMediaResponse) => void;
}) {
  const visibleTags = media.tags.slice(0, 3);
  const overflowTags = media.tags.length - visibleTags.length;

  return (
    <article className="group flex flex-col overflow-hidden rounded-[6px] bg-bg-2">
      <div className="relative">
        <PreviewCarousel
          mediaId={media.id}
          title={media.title}
          onClick={() => onPlay(media)}
          className="aspect-video h-auto w-full"
        />
        {media.durationSeconds !== null && (
          <span className="pointer-events-none absolute bottom-1.5 right-1.5 rounded-[3px] bg-bg-5/85 px-1.5 py-0.5 font-mono text-[10px] text-fg-1">
            {formatDuration(media.durationSeconds)}
          </span>
        )}
        {!media.isActive && (
          <span className="pointer-events-none absolute bottom-1.5 left-1.5 rounded-[3px] bg-accent-warn px-1.5 py-0.5 font-mono text-[9px] font-bold uppercase tracking-label text-bg-0">
            paused
          </span>
        )}
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5 p-3">
        <h3
          className="line-clamp-2 text-[13px] font-semibold leading-snug text-fg-0"
          title={media.title}
        >
          {media.title}
        </h3>
        <div className="truncate font-mono text-[10px] uppercase tracking-label text-fg-3">
          {mediaTypeLabel(media.type)}
          {typeMetadata(media) && <span className="normal-case"> · {typeMetadata(media)}</span>}
        </div>
        {(visibleTags.length > 0 || overflowTags > 0) && (
          <div className="flex flex-wrap gap-1">
            {visibleTags.map((tag) => (
              <span
                key={tag.id}
                title={tag.label ?? tag.name}
                className="truncate rounded-[3px] bg-accent-cfg px-1.5 py-0.5 font-mono text-[9px] font-semibold text-fg-0"
              >
                {tag.label ?? tag.name}
              </span>
            ))}
            {overflowTags > 0 && (
              <span className="rounded-[3px] bg-bg-3 px-1.5 py-0.5 font-mono text-[9px] font-semibold text-fg-2">
                +{overflowTags}
              </span>
            )}
          </div>
        )}
        <div className="mt-auto flex items-center justify-between gap-2 pt-1">
          <span className="truncate font-mono text-[10px] text-fg-3">
            {sourceTypeLabel(media.sourceType)}
          </span>
          <MediaCardMenu
            media={media}
            isRunning={isRunning}
            onRunPipeline={onRunPipeline}
          />
        </div>
      </div>
    </article>
  );
}

function FilterBar({
  typeFilter,
  sourceFilter,
  activeFilter,
  query,
  onTypeFilterChange,
  onSourceFilterChange,
  onActiveFilterChange,
  onQueryChange,
}: {
  typeFilter: string;
  sourceFilter: string;
  activeFilter: string;
  query: string;
  onTypeFilterChange: (value: string) => void;
  onSourceFilterChange: (value: string) => void;
  onActiveFilterChange: (value: 'all' | 'active' | 'inactive') => void;
  onQueryChange: (value: string) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 bg-bg-0 p-3">
      <label className="flex min-w-[16rem] flex-1 items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 focus-within:[box-shadow:inset_0_0_0_2px_var(--accent-live)]">
        <Search className="size-3.5 text-fg-3" />
        <input
          aria-label="Search media"
          value={query}
          onChange={(event) => onQueryChange(event.target.value)}
          placeholder="Search by title, tag, source, series, director…"
          className="min-w-0 flex-1 bg-transparent text-[12px] text-fg-1 outline-none placeholder:text-fg-3"
        />
        {query && (
          <button
            type="button"
            aria-label="Clear search"
            onClick={() => onQueryChange('')}
            className="font-mono text-[10px] text-fg-3 hover:text-fg-1"
          >
            ✕
          </button>
        )}
      </label>
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

function PreviewCarousel({
  mediaId,
  title,
  onClick,
  className,
}: {
  mediaId: string;
  title: string;
  onClick?: () => void;
  className?: string;
}) {
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
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      aria-label={onClick ? `Play ${title}` : undefined}
      onClick={onClick}
      onKeyDown={(event) => {
        if (!onClick) return;
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onClick();
        }
      }}
      className={
        'group relative overflow-hidden rounded-[4px] bg-bg-1 ' +
        (className ?? 'h-16 w-28') +
        ' ' +
        (onClick
          ? 'cursor-pointer transition hover:ring-2 hover:ring-accent-live focus:outline-none focus-visible:ring-2 focus-visible:ring-accent-live'
          : '')
      }
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
