import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiEmpty, apiJson } from '@/lib/api';
import { useAuth } from '@/providers/auth-context';
import type {
  ChannelLogoUploadResponse,
  ChannelResponse,
  CreateChannelInput,
  UpdateChannelInput,
} from './channel-types';

export const CHANNELS_QUERY_KEY = ['channels'] as const;

type UpdateChannelCommand = {
  id: string;
  input: UpdateChannelInput;
};

export function useChannels() {
  const auth = useAuth();
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: CHANNELS_QUERY_KEY,
    queryFn: () => apiJson<ChannelResponse[]>('/api/channels'),
    enabled: auth.authenticated,
    staleTime: 5_000,
    retry: 1,
  });

  const create = useMutation({
    mutationFn: (input: CreateChannelInput) =>
      apiJson<ChannelResponse>('/api/channels', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: (channel) => {
      qc.setQueryData<ChannelResponse[]>(CHANNELS_QUERY_KEY, (current) =>
        sortChannels([channel, ...(current ?? []).filter((item) => item.id !== channel.id)]),
      );
    },
  });

  const update = useMutation({
    mutationFn: ({ id, input }: UpdateChannelCommand) =>
      apiJson<ChannelResponse>(`/api/channels/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: (channel) => {
      qc.setQueryData<ChannelResponse[]>(CHANNELS_QUERY_KEY, (current) =>
        sortChannels((current ?? []).map((item) => (item.id === channel.id ? channel : item))),
      );
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => apiEmpty(`/api/channels/${id}`, { method: 'DELETE' }),
    onSuccess: (_, id) => {
      qc.setQueryData<ChannelResponse[]>(CHANNELS_QUERY_KEY, (current) =>
        (current ?? []).filter((item) => item.id !== id),
      );
    },
  });

  const uploadLogo = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.set('file', file);

      return apiJson<ChannelLogoUploadResponse>('/api/channels/logo', {
        method: 'POST',
        body: form,
      });
    },
  });

  const channels = useMemo(() => sortChannels(query.data ?? []), [query.data]);

  return {
    channels,
    authenticated: auth.authenticated,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
    createChannel: create.mutateAsync,
    updateChannel: update.mutateAsync,
    deleteChannel: remove.mutateAsync,
    uploadLogo: uploadLogo.mutateAsync,
    isSaving: create.isPending || update.isPending,
    isDeleting: remove.isPending,
    isUploadingLogo: uploadLogo.isPending,
  };
}

function sortChannels(channels: ChannelResponse[]): ChannelResponse[] {
  return channels
    .slice()
    .sort(
      (left, right) =>
        left.displayOrder - right.displayOrder || left.name.localeCompare(right.name),
    );
}
