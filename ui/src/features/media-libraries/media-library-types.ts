export type ChannelMediaType = 0 | 1 | 2 | 3 | 4;
export type MediaSourceType = 0 | 1 | 2;

export type MediaLibraryResponse = {
  id: string;
  name: string;
  description: string | null;
  defaultMediaType: ChannelMediaType;
  sourceType: MediaSourceType;
  sourceRef: string;
  isActive: boolean;
  lastScannedAt: string | null;
  createdAt: string;
  updatedAt: string;
};

export type CreateMediaLibraryRequest = {
  name: string;
  description: string | null;
  defaultMediaType: ChannelMediaType;
  sourceType: MediaSourceType;
  sourceRef: string;
};

export type UpdateMediaLibraryRequest = CreateMediaLibraryRequest & {
  isActive: boolean;
};

export type MediaLibraryFormValues = {
  id: string | null;
  name: string;
  description: string;
  defaultMediaType: ChannelMediaType;
  sourceType: MediaSourceType;
  sourceRef: string;
  isActive: boolean;
};

export const CHANNEL_MEDIA_TYPE_OPTIONS: ReadonlyArray<{
  value: ChannelMediaType;
  label: string;
}> = [
  { value: 0, label: 'Commercial' },
  { value: 1, label: 'TV Show' },
  { value: 2, label: 'Bumper' },
  { value: 3, label: 'Movies' },
  { value: 4, label: 'Information' },
];

export const MEDIA_SOURCE_TYPE_OPTIONS: ReadonlyArray<{
  value: MediaSourceType;
  label: string;
}> = [
  { value: 0, label: 'Jellyfin' },
  { value: 1, label: 'Local File' },
  { value: 2, label: 'URL' },
];

export const EMPTY_MEDIA_LIBRARY_FORM: MediaLibraryFormValues = {
  id: null,
  name: '',
  description: '',
  defaultMediaType: 3,
  sourceType: 0,
  sourceRef: '',
  isActive: true,
};

export function channelMediaTypeLabel(type: ChannelMediaType): string {
  const label = CHANNEL_MEDIA_TYPE_OPTIONS.find((item) => item.value === type)?.label;
  return label ? `${label} media` : 'Unknown media';
}

export function mediaSourceTypeLabel(type: MediaSourceType): string {
  return MEDIA_SOURCE_TYPE_OPTIONS.find((item) => item.value === type)?.label ?? 'Unknown';
}

export function mediaLibraryToForm(library: MediaLibraryResponse): MediaLibraryFormValues {
  return {
    id: library.id,
    name: library.name,
    description: library.description ?? '',
    defaultMediaType: library.defaultMediaType,
    sourceType: library.sourceType,
    sourceRef: library.sourceRef,
    isActive: library.isActive,
  };
}

export function formToCreateRequest(form: MediaLibraryFormValues): CreateMediaLibraryRequest {
  return {
    name: form.name.trim(),
    description: nullableText(form.description),
    defaultMediaType: form.defaultMediaType,
    sourceType: form.sourceType,
    sourceRef: form.sourceRef.trim(),
  };
}

export function formToUpdateRequest(form: MediaLibraryFormValues): UpdateMediaLibraryRequest {
  return {
    ...formToCreateRequest(form),
    isActive: form.isActive,
  };
}

export function mergeMediaLibraryForm(
  value?: Partial<MediaLibraryFormValues>,
): MediaLibraryFormValues {
  return {
    ...EMPTY_MEDIA_LIBRARY_FORM,
    ...value,
  };
}

function nullableText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
