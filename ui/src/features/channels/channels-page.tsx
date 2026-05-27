import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { CircleDot, Clapperboard, Radio, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiError } from '@/lib/api';
import {
  channelToForm,
  EMPTY_CHANNEL_FORM,
  formToCreateInput,
  formToUpdateInput,
  type ChannelFormValues,
  type ChannelResponse,
} from './channel-types';
import { ChannelForm } from './channel-form';
import { ChannelList } from './channel-list';
import { useChannels } from './use-channels';

export function ChannelsPage() {
  const channels = useChannels();
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState<ChannelFormValues>(EMPTY_CHANNEL_FORM);
  const [editorOpen, setEditorOpen] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const filteredChannels = useMemo(() => {
    const term = query.trim().toLowerCase();
    if (!term) return channels.channels;

    return channels.channels.filter(
      (channel) =>
        channel.name.toLowerCase().includes(term) ||
        channel.slug.toLowerCase().includes(term) ||
        (channel.description ?? '').toLowerCase().includes(term),
    );
  }, [channels.channels, query]);

  const activeChannels = channels.channels.filter((channel) => channel.isActive).length;
  const commercialChannels = channels.channels.filter(
    (channel) => channel.commercialsEnabled,
  ).length;

  const selectChannel = (channel: ChannelResponse) => {
    setDraft(channelToForm(channel));
    setValidationError(null);
    setEditorOpen(true);
  };

  const createNew = () => {
    setDraft(EMPTY_CHANNEL_FORM);
    setValidationError(null);
    setEditorOpen(true);
  };

  const closeEditor = (open: boolean) => {
    setEditorOpen(open);
    if (!open) setValidationError(null);
  };

  const save = async () => {
    const validation = validateDraft(draft);
    if (validation) {
      setValidationError(validation);
      return;
    }

    setValidationError(null);

    try {
      if (draft.id) {
        const channel = await channels.updateChannel({
          id: draft.id,
          input: formToUpdateInput(draft),
        });
        setDraft(channelToForm(channel));
        setEditorOpen(false);
        toast.success('Channel updated');
        return;
      }

      const channel = await channels.createChannel(formToCreateInput(draft));
      setDraft(channelToForm(channel));
      setEditorOpen(false);
      toast.success('Channel created');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const remove = async () => {
    if (!draft.id) return;

    const confirmed = window.confirm(`Delete channel "${draft.name}"?`);
    if (!confirmed) return;

    try {
      await channels.deleteChannel(draft.id);
      setDraft(EMPTY_CHANNEL_FORM);
      setEditorOpen(false);
      setValidationError(null);
      toast.success('Channel deleted');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const uploadLogo = async (file: File) => {
    try {
      const response = await channels.uploadLogo(file);
      setDraft((current) => ({ ...current, logoUrl: response.logoUrl }));
      toast.success('Logo uploaded');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (!channels.authenticated) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="max-w-md rounded-lg border border-border bg-panel p-5 text-center">
          <ShieldAlert className="mx-auto mb-3 size-8 text-warn" />
          <h1 className="text-base font-semibold text-fg-0">JWT session required</h1>
          <p className="mt-2 text-sm text-fg-1">
            Channel management requires an authenticated SignalNine operator session.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto p-3 xl:overflow-hidden">
      <div className="grid gap-3 md:grid-cols-3">
        <SummaryMetric
          icon={<Radio className="size-4" />}
          label="Total channels"
          value={String(channels.channels.length)}
          detail="configured"
        />
        <SummaryMetric
          icon={<CircleDot className="size-4" />}
          label="Active"
          value={String(activeChannels)}
          detail="available to runtime"
        />
        <SummaryMetric
          icon={<Clapperboard className="size-4" />}
          label="Commercials"
          value={String(commercialChannels)}
          detail="ad breaks enabled"
        />
      </div>
      <ChannelList
        channels={filteredChannels}
        selectedId={editorOpen ? draft.id : null}
        query={query}
        isLoading={channels.isLoading}
        isError={channels.isError}
        onQueryChange={setQuery}
        onCreateNew={createNew}
        onRefresh={() => {
          void channels.refresh();
        }}
        onSelect={selectChannel}
      />
      <Dialog open={editorOpen} onOpenChange={closeEditor}>
        <DialogContent className="max-h-[calc(100vh-1.5rem)] max-w-3xl overflow-hidden border-border bg-panel p-0 shadow-2xl">
          <DialogTitle className="sr-only">
            {draft.id ? 'Edit Channel' : 'Create Channel'}
          </DialogTitle>
          <DialogDescription className="sr-only">
            Manage channel metadata, logo upload, runtime status, and commercial intervals.
          </DialogDescription>
          <ChannelForm
            value={draft}
            isSaving={channels.isSaving}
            isDeleting={channels.isDeleting}
            isUploadingLogo={channels.isUploadingLogo}
            validationError={validationError}
            onChange={setDraft}
            onCreateNew={createNew}
            onDelete={remove}
            onLogoUpload={(file) => {
              void uploadLogo(file);
            }}
            onSubmit={() => {
              void save();
            }}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}

function SummaryMetric({
  icon,
  label,
  value,
  detail,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <section className="min-w-0 rounded-lg border border-border bg-panel p-3">
      <div className="mb-2 flex size-7 items-center justify-center rounded border border-on-air/40 bg-on-air/10 text-on-air-2">
        {icon}
      </div>
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-2">{label}</div>
      <div className="truncate text-lg font-semibold text-fg-0">{value}</div>
      <div className="truncate text-[12px] text-fg-2">{detail}</div>
    </section>
  );
}

function validateDraft(draft: ChannelFormValues): string | null {
  if (!draft.name.trim() || !draft.slug.trim()) return 'Name and slug are required.';
  if (draft.displayOrder < 0) return 'Display order cannot be negative.';
  if (draft.commercialIntervalMinSeconds < 0 || draft.commercialIntervalMaxSeconds < 0) {
    return 'Commercial intervals cannot be negative.';
  }
  if (draft.commercialIntervalMaxSeconds < draft.commercialIntervalMinSeconds) {
    return 'Commercial max interval must be greater than or equal to min interval.';
  }

  return null;
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (typeof error.body === 'string' && error.body.trim()) return error.body;
    if (error.status === 409) return 'Channel slug is already in use.';
    if (error.status === 401) return 'Login required for channel APIs.';
  }
  if (error instanceof Error) return error.message;
  return 'Channel request failed.';
}
