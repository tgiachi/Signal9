import { useMemo, useState } from 'react';
import { Cog, Database, Filter, Play, Plus, RefreshCw, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiError } from '@/lib/api';
import { MediaLibraryForm } from './media-library-form';
import {
  CHANNEL_MEDIA_TYPE_OPTIONS,
  EMPTY_MEDIA_LIBRARY_FORM,
  MEDIA_SOURCE_TYPE_OPTIONS,
  channelMediaTypeLabel,
  formToUpdateRequest,
  mediaLibraryToForm,
  mediaSourceTypeLabel,
  type CreateMediaLibraryRequest,
  type MediaLibraryFormValues,
  type MediaLibraryResponse,
  type UpdateMediaLibraryRequest,
} from './media-library-types';
import { useMediaLibraries } from './use-media-libraries';

type EditorState =
  | { mode: 'create'; value?: Partial<MediaLibraryFormValues> }
  | { mode: 'edit'; value: MediaLibraryFormValues }
  | null;

export function MediaLibrariesPage() {
  const mediaLibraries = useMediaLibraries();
  const [editor, setEditor] = useState<EditorState>(null);
  const [typeFilter, setTypeFilter] = useState<'all' | string>('all');
  const [sourceFilter, setSourceFilter] = useState<'all' | string>('all');
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');

  const filteredLibraries = useMemo(
    () =>
      mediaLibraries.libraries.filter((library) => {
        if (typeFilter !== 'all' && library.defaultMediaType !== Number(typeFilter)) return false;
        if (sourceFilter !== 'all' && library.sourceType !== Number(sourceFilter)) return false;
        if (activeFilter === 'active' && !library.isActive) return false;
        if (activeFilter === 'inactive' && library.isActive) return false;
        return true;
      }),
    [activeFilter, mediaLibraries.libraries, sourceFilter, typeFilter],
  );

  const createLibrary = async (input: CreateMediaLibraryRequest) => {
    try {
      await mediaLibraries.createMediaLibrary(input);
      setEditor(null);
      toast.success('Media library created');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const updateLibrary = async (id: string, input: UpdateMediaLibraryRequest) => {
    try {
      await mediaLibraries.updateMediaLibrary({ id, input });
      setEditor(null);
      toast.success('Media library updated');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const toggleActive = async (library: MediaLibraryResponse) => {
    try {
      await mediaLibraries.updateMediaLibrary({
        id: library.id,
        input: {
          ...formToUpdateRequest(mediaLibraryToForm(library)),
          isActive: !library.isActive,
        },
      });
      toast.success('Media library updated');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const deleteLibrary = async (library: MediaLibraryResponse) => {
    if (!window.confirm(`Delete media library "${library.name}"?`)) return;

    try {
      await mediaLibraries.deleteMediaLibrary(library.id);
      toast.success('Media library deleted');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const scanLibrary = async (library: MediaLibraryResponse) => {
    try {
      const job = await mediaLibraries.scanMediaLibrary(library.id);
      toast.success(`Library scan enqueued: ${job.id.slice(0, 8)}`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const processAllLibrary = async (library: MediaLibraryResponse) => {
    if (!window.confirm(`Force re-process all media in "${library.name}"?`)) return;
    try {
      const count = await mediaLibraries.processAllMediaLibrary(library.id);
      toast.success(`Enqueued ${count} pipeline job${count === 1 ? '' : 's'}`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (!mediaLibraries.authenticated) {
    return <AuthRequired />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3 xl:overflow-hidden">
      <div className="grid gap-3 md:grid-cols-3">
        <SummaryMetric label="Libraries" value={String(mediaLibraries.libraries.length)} />
        <SummaryMetric
          label="Active"
          value={String(
            mediaLibraries.libraries.filter((library) => library.isActive).length,
          )}
        />
        <SummaryMetric
          label="Jellyfin"
          value={String(
            mediaLibraries.libraries.filter((library) => library.sourceType === 0).length,
          )}
        />
      </div>

      <section className="flex min-h-[26rem] flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2">
        <header className="flex flex-wrap items-center gap-3 bg-bg-4 px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-[4px] bg-accent-live text-bg-5">
            <Database className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Media Libraries</h1>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-3">
              {filteredLibraries.length} visible
            </p>
          </div>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setEditor({ mode: 'create' })}
              className="inline-flex items-center gap-2 rounded-[6px] bg-accent-live px-2.5 py-1.5 text-[12px] font-semibold text-bg-5 transition hover:bg-accent-live-hover"
            >
              <Plus className="size-3.5" />
              New Library
            </button>
            <button
              type="button"
              onClick={() => {
                void mediaLibraries.refresh();
              }}
              className="inline-flex items-center gap-2 rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0"
            >
              <RefreshCw className="size-3.5" />
              Refresh
            </button>
          </div>
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
          <div className="grid min-w-[64rem] grid-cols-[minmax(14rem,1.1fr)_10rem_minmax(18rem,1.4fr)_8rem_11rem_12rem] gap-3 bg-bg-4 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-3">
            <span>Name</span>
            <span>Media type</span>
            <span>Source</span>
            <span>Status</span>
            <span>Last scanned</span>
            <span>Actions</span>
          </div>
          {mediaLibraries.isLoading ? (
            <div className="flex h-56 items-center justify-center text-sm text-fg-3">
              Loading media libraries.
            </div>
          ) : mediaLibraries.isError ? (
            <div className="flex h-56 items-center justify-center text-sm text-accent-err">
              Failed to load media libraries.
            </div>
          ) : filteredLibraries.length === 0 ? (
            <div className="flex h-56 items-center justify-center text-sm text-fg-3">
              No media libraries match this view.
            </div>
          ) : (
            filteredLibraries.map((library, idx) => (
              <div
                key={library.id}
                className={
                  'grid min-w-[64rem] grid-cols-[minmax(14rem,1.1fr)_10rem_minmax(18rem,1.4fr)_8rem_11rem_12rem] items-center gap-3 px-3 py-3 ' +
                  (idx % 2 ? 'bg-bg-3' : 'bg-bg-2')
                }
              >
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-fg-0">{library.name}</div>
                  <div className="truncate text-[12px] text-fg-3">
                    {library.description ?? 'No description'}
                  </div>
                </div>
                <span className="text-[12px] text-fg-1">
                  {channelMediaTypeLabel(library.defaultMediaType)}
                </span>
                <span className="break-all font-mono text-[12px] text-fg-2">
                  {mediaSourceTypeLabel(library.sourceType)} · {library.sourceRef}
                </span>
                <button
                  type="button"
                  role="switch"
                  aria-checked={library.isActive}
                  aria-label={`Toggle ${library.name}`}
                  onClick={() => {
                    void toggleActive(library);
                  }}
                  className={
                    'w-fit rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-label ' +
                    (library.isActive
                      ? 'bg-accent-live text-bg-5'
                      : 'bg-accent-warn text-bg-0')
                  }
                >
                  {library.isActive ? 'active' : 'paused'}
                </button>
                <span className="font-mono text-[12px] text-fg-2">
                  {formatDate(library.lastScannedAt)}
                </span>
                <span className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    aria-label={`Scan ${library.name}`}
                    disabled={!library.isActive || mediaLibraries.isScanning}
                    onClick={() => {
                      void scanLibrary(library);
                    }}
                    className="inline-flex items-center gap-1 rounded-[6px] bg-accent-jobs px-2.5 py-1.5 text-[12px] font-semibold text-fg-0 transition hover:opacity-90 disabled:bg-bg-2 disabled:text-fg-3 disabled:opacity-40"
                  >
                    <Play className="size-3" />
                    Scan
                  </button>
                  <button
                    type="button"
                    aria-label={`Force process all ${library.name}`}
                    disabled={!library.isActive || mediaLibraries.isProcessingAll}
                    onClick={() => {
                      void processAllLibrary(library);
                    }}
                    className="inline-flex items-center gap-1 rounded-[6px] bg-accent-cfg px-2.5 py-1.5 text-[12px] font-semibold text-fg-0 transition hover:opacity-90 disabled:bg-bg-2 disabled:text-fg-3 disabled:opacity-40"
                  >
                    <Cog className="size-3" />
                    Force process
                  </button>
                  <button
                    type="button"
                    aria-label={`Edit ${library.name}`}
                    onClick={() =>
                      setEditor({ mode: 'edit', value: mediaLibraryToForm(library) })
                    }
                    className="rounded-[6px] bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:bg-[#343b41] hover:text-fg-0"
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    aria-label={`Delete ${library.name}`}
                    onClick={() => {
                      void deleteLibrary(library);
                    }}
                    className="rounded-[6px] bg-accent-err px-2.5 py-1.5 text-[12px] font-semibold text-fg-0 transition hover:opacity-90"
                  >
                    Delete
                  </button>
                </span>
              </div>
            ))
          )}
        </div>
      </section>

      <Dialog open={editor !== null} onOpenChange={(open) => !open && setEditor(null)}>
        <DialogContent className="max-h-[calc(100vh-1.5rem)] max-w-2xl overflow-hidden p-0">
          <DialogTitle className="sr-only">
            {editor?.mode === 'edit' ? 'Edit Media Library' : 'Create Media Library'}
          </DialogTitle>
          <DialogDescription className="sr-only">
            Manage a SignalNine media library source.
          </DialogDescription>
          {editor?.mode === 'edit' ? (
            <MediaLibraryForm
              key={editor.value.id ?? 'edit'}
              mode="edit"
              initialValue={editor.value}
              isSaving={mediaLibraries.isSaving}
              onSubmit={(input) => updateLibrary(editor.value.id ?? '', input)}
            />
          ) : (
            <MediaLibraryForm
              key="create"
              mode="create"
              initialValue={editor?.value ?? EMPTY_MEDIA_LIBRARY_FORM}
              isSaving={mediaLibraries.isSaving}
              onSubmit={createLibrary}
            />
          )}
        </DialogContent>
      </Dialog>
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
        aria-label="Default media type filter"
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
        aria-label="Active filter"
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

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <section className="min-w-0 rounded-[6px] bg-bg-2 p-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-lg font-semibold text-fg-0">{value}</div>
    </section>
  );
}

function AuthRequired() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <div className="max-w-md rounded-[6px] bg-bg-2 p-5 text-center">
        <ShieldAlert className="mx-auto mb-3 size-8 text-accent-warn" />
        <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
        <p className="mt-2 text-sm text-fg-2">
          Media library management requires an authenticated session.
        </p>
      </div>
    </div>
  );
}

function formatDate(value: string | null): string {
  if (!value) return 'Never';
  return new Date(value).toLocaleString();
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return 'Una library con questa origine esiste già';
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'Media library request failed.';
}
