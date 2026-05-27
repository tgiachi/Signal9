import { useState } from 'react';
import { Database, PlugZap, RefreshCw, Server, ShieldAlert, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiError } from '@/lib/api';
import { MediaLibraryForm } from '@/features/media-libraries/media-library-form';
import {
  type CreateMediaLibraryRequest,
  type MediaLibraryFormValues,
} from '@/features/media-libraries/media-library-types';
import { useMediaLibraries } from '@/features/media-libraries/use-media-libraries';
import { JellyfinConnectionForm } from './jellyfin-connection-form';
import type {
  JellyfinConnectionInput,
  JellyfinLibrarySummary,
  JellyfinServerInfo,
} from './jellyfin-types';
import { useJellyfin } from './use-jellyfin';

export function JellyfinPage() {
  const jellyfin = useJellyfin();
  const mediaLibraries = useMediaLibraries();
  const [serverInfo, setServerInfo] = useState<JellyfinServerInfo | null>(null);
  const [selectedLibrary, setSelectedLibrary] = useState<JellyfinLibrarySummary | null>(null);
  const [registeredConflicts, setRegisteredConflicts] = useState<Set<string>>(() => new Set());
  const connection = jellyfin.connection ?? {
    isConfigured: false,
    baseUrl: null,
    lastVerifiedAt: null,
  };

  const saveConnection = async (input: JellyfinConnectionInput) => {
    try {
      await jellyfin.saveConnection(input);
      setServerInfo(null);
      toast.success('Jellyfin connection saved');
    } catch (error) {
      toast.error(jellyfinErrorMessage(error));
    }
  };

  const testConnection = async () => {
    try {
      const info = await jellyfin.testConnection();
      setServerInfo(info);
      toast.success('Jellyfin connection verified');
    } catch (error) {
      setServerInfo(null);
      toast.error(jellyfinErrorMessage(error));
    }
  };

  const disconnect = async () => {
    if (!window.confirm('Disconnect Jellyfin?')) return;

    try {
      await jellyfin.disconnect();
      setServerInfo(null);
      toast.success('Jellyfin disconnected');
    } catch (error) {
      toast.error(jellyfinErrorMessage(error));
    }
  };

  const registerLibrary = async (input: CreateMediaLibraryRequest) => {
    if (!selectedLibrary) return;

    try {
      await mediaLibraries.createMediaLibrary(input);
      setSelectedLibrary(null);
      toast.success('Media library registered');
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setRegisteredConflicts((current) => new Set(current).add(selectedLibrary.id));
        setSelectedLibrary(null);
        toast.error('Una library con questa origine esiste già');
        return;
      }

      toast.error(mediaLibraryErrorMessage(error));
    }
  };

  if (!jellyfin.authenticated) {
    return <AuthRequired />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3">
      <section className="rounded-lg border border-border bg-panel">
        <header className="flex flex-wrap items-center gap-3 border-b border-border-subtle bg-panel-strong px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-md border border-cyan/40 bg-cyan/10 text-cyan">
            <Server className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-fg-0">Jellyfin Connection</h1>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-2">
              {connection.isConfigured ? 'Configured' : 'Not configured'}
            </p>
          </div>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => {
                void testConnection();
              }}
              disabled={!connection.isConfigured || jellyfin.isTesting}
              className="inline-flex items-center gap-2 rounded-md border border-on-air/40 bg-on-air/10 px-2.5 py-1.5 text-[12px] font-semibold text-on-air-2 transition hover:bg-on-air/15 disabled:opacity-40"
            >
              <PlugZap className="size-3.5" />
              Test connection
            </button>
            <button
              type="button"
              onClick={() => {
                void disconnect();
              }}
              disabled={!connection.isConfigured || jellyfin.isDisconnecting}
              className="inline-flex items-center gap-2 rounded-md border border-error/40 bg-error-bg/40 px-2.5 py-1.5 text-[12px] font-semibold text-error transition hover:bg-error-bg disabled:opacity-40"
            >
              <Trash2 className="size-3.5" />
              Disconnect
            </button>
          </div>
        </header>
        <div className="grid gap-4 p-3 lg:grid-cols-[minmax(0,1fr)_22rem]">
          <JellyfinConnectionForm
            status={connection}
            isSaving={jellyfin.isSavingConnection}
            onSubmit={saveConnection}
          />
          <StatusPanel
            baseUrl={connection.baseUrl}
            lastVerifiedAt={connection.lastVerifiedAt}
            serverInfo={serverInfo}
            isLoading={jellyfin.isConnectionLoading}
          />
        </div>
      </section>

      <section className="flex min-h-[22rem] flex-col overflow-hidden rounded-lg border border-border bg-panel">
        <header className="flex flex-wrap items-center gap-3 border-b border-border-subtle bg-panel-strong px-3 py-2">
          <div className="flex size-8 items-center justify-center rounded-md border border-on-air/40 bg-on-air/10 text-on-air-2">
            <Database className="size-4" />
          </div>
          <div className="min-w-0">
            <h2 className="text-sm font-semibold text-fg-0">Libraries on this server</h2>
            <p className="font-mono text-[10px] uppercase tracking-label text-fg-2">
              Jellyfin source libraries
            </p>
          </div>
          <button
            type="button"
            onClick={() => {
              void jellyfin.refreshLibraries();
            }}
            disabled={!connection.isConfigured}
            className="ml-auto inline-flex items-center gap-2 rounded-md border border-border bg-bg-2 px-2.5 py-1.5 text-[12px] text-fg-1 transition hover:border-on-air/40 hover:text-fg-0 disabled:opacity-40"
          >
            <RefreshCw className="size-3.5" />
            Refresh
          </button>
        </header>

        {!connection.isConfigured ? (
          <div className="flex flex-1 items-center justify-center bg-bg-1 p-6 text-sm text-fg-2">
            Configure connection first
          </div>
        ) : jellyfin.isLibrariesLoading ? (
          <div className="flex flex-1 items-center justify-center bg-bg-1 p-6 text-sm text-fg-2">
            Loading Jellyfin libraries.
          </div>
        ) : jellyfin.isLibrariesError ? (
          <div className="flex flex-1 items-center justify-center bg-bg-1 p-6 text-sm text-error">
            {jellyfinErrorMessage(jellyfin.librariesError)}
          </div>
        ) : jellyfin.libraries.length === 0 ? (
          <div className="flex flex-1 items-center justify-center bg-bg-1 p-6 text-sm text-fg-2">
            No Jellyfin libraries returned by the server.
          </div>
        ) : (
          <div className="min-h-0 flex-1 overflow-auto bg-bg-1">
            <div className="grid min-w-[48rem] grid-cols-[minmax(14rem,1fr)_minmax(16rem,1.3fr)_10rem_12rem] gap-3 border-b border-border-subtle bg-bg-2 px-3 py-2 font-mono text-[10px] uppercase tracking-label text-fg-2">
              <span>Name</span>
              <span>ID</span>
              <span>Collection</span>
              <span>Action</span>
            </div>
            {jellyfin.libraries.map((library) => (
              <div
                key={library.id}
                className="grid min-w-[48rem] grid-cols-[minmax(14rem,1fr)_minmax(16rem,1.3fr)_10rem_12rem] items-center gap-3 border-b border-border-subtle px-3 py-3"
              >
                <span className="text-sm font-semibold text-fg-0">{library.name}</span>
                <span className="break-all font-mono text-[12px] text-fg-1">{library.id}</span>
                <span className="font-mono text-[12px] text-fg-1">
                  {library.collectionType ?? 'unknown'}
                </span>
                {registeredConflicts.has(library.id) ? (
                  <span className="w-fit rounded border border-warn/40 bg-warn/10 px-2 py-1 font-mono text-[10px] uppercase tracking-label text-warn">
                    already registered
                  </span>
                ) : (
                  <button
                    type="button"
                    onClick={() => setSelectedLibrary(library)}
                    className="inline-flex items-center justify-center rounded-md border border-on-air/40 bg-on-air/10 px-2.5 py-1.5 text-[12px] font-semibold text-on-air-2 transition hover:bg-on-air/15"
                  >
                    Register as MediaLibrary
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </section>

      <Dialog open={selectedLibrary !== null} onOpenChange={(open) => !open && setSelectedLibrary(null)}>
        <DialogContent className="max-h-[calc(100vh-1.5rem)] max-w-2xl overflow-hidden border-border bg-panel p-0 shadow-2xl">
          <DialogTitle className="sr-only">Register Jellyfin library</DialogTitle>
          <DialogDescription className="sr-only">
            Register a Jellyfin source as a SignalNine media library.
          </DialogDescription>
          {selectedLibrary && (
            <MediaLibraryForm
              key={selectedLibrary.id}
              mode="create"
              initialValue={jellyfinLibraryToForm(selectedLibrary)}
              isSaving={mediaLibraries.isSaving}
              onSubmit={registerLibrary}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function StatusPanel({
  baseUrl,
  lastVerifiedAt,
  serverInfo,
  isLoading,
}: {
  baseUrl: string | null;
  lastVerifiedAt: string | null;
  serverInfo: JellyfinServerInfo | null;
  isLoading: boolean;
}) {
  return (
    <div className="rounded-md border border-border-subtle bg-bg-1 p-3">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-2">
        Current status
      </div>
      <div className="mt-2 grid gap-2 text-[12px]">
        <InfoRow label="Base URL" value={isLoading ? 'Loading' : baseUrl ?? 'Not configured'} />
        <InfoRow label="Last verified" value={formatDate(lastVerifiedAt)} />
        {serverInfo && (
          <>
            <InfoRow label="Server" value={serverInfo.serverName} />
            <InfoRow label="Version" value={serverInfo.version} />
            <InfoRow label="Server ID" value={serverInfo.id} />
          </>
        )}
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-2">
      <span className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</span>
      <span className="min-w-0 break-words text-fg-0">{value}</span>
    </div>
  );
}

function AuthRequired() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <div className="max-w-md rounded-lg border border-border bg-panel p-5 text-center">
        <ShieldAlert className="mx-auto mb-3 size-8 text-warn" />
        <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
        <p className="mt-2 text-sm text-fg-1">Jellyfin settings require an authenticated session.</p>
      </div>
    </div>
  );
}

function jellyfinLibraryToForm(library: JellyfinLibrarySummary): Partial<MediaLibraryFormValues> {
  return {
    name: library.name,
    defaultMediaType: inferMediaType(library.collectionType),
    sourceType: 0,
    sourceRef: library.id,
    isActive: true,
  };
}

function inferMediaType(collectionType: string | null): MediaLibraryFormValues['defaultMediaType'] {
  if (collectionType === 'tvshows') return 1;
  if (collectionType === 'movies') return 3;
  return 3;
}

function formatDate(value: string | null): string {
  if (!value) return 'Never';
  return new Date(value).toLocaleString();
}

function jellyfinErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return 'Jellyfin connection is not configured';
    if (error.status === 401) return 'Jellyfin API key was rejected';
    if (error.status === 502) return 'Jellyfin server is unreachable';
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'Jellyfin request failed.';
}

function mediaLibraryErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return 'Una library con questa origine esiste già';
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
  }
  if (error instanceof Error) return error.message;
  return 'Media library request failed.';
}
