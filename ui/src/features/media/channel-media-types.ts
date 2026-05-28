import {
  type ChannelMediaType,
  type MediaSourceType,
  channelMediaTypeLabel,
  mediaSourceTypeLabel,
} from '@/features/media-libraries/media-library-types';

export type TagSummary = {
  id: string;
  name: string;
  label: string | null;
};

export type ChannelMediaResponse = {
  id: string;
  type: ChannelMediaType;
  title: string;
  durationSeconds: number | null;
  isActive: boolean;
  sourceType: MediaSourceType;
  sourceRef: string | null;
  movieReleaseYear: number | null;
  movieDirector: string | null;
  tvSeriesName: string | null;
  tvSeason: number | null;
  tvEpisode: number | null;
  commercialAdvertiser: string | null;
  commercialCampaign: string | null;
  informationEdition: string | null;
  createdAt: string;
  updatedAt: string;
  tags: TagSummary[];
};

export function mediaTypeLabel(type: ChannelMediaType): string {
  return channelMediaTypeLabel(type);
}

export function sourceTypeLabel(type: MediaSourceType): string {
  return mediaSourceTypeLabel(type);
}

export function formatDuration(seconds: number | null): string {
  if (seconds === null) return 'Unknown';

  const safe = Math.max(0, seconds);
  const h = Math.floor(safe / 3600);
  const m = Math.floor((safe % 3600) / 60);
  const s = safe % 60;

  return [h, m, s].map((part) => String(part).padStart(2, '0')).join(':');
}

export function previewUrl(mediaId: string, index: number): string {
  return `/assets/previews/${mediaId}/thumb-${String(index).padStart(3, '0')}.jpg`;
}

export function streamUrl(mediaId: string, accessToken: string | null): string {
  const base = `/api/media/${mediaId}/stream`;
  return accessToken ? `${base}?access_token=${encodeURIComponent(accessToken)}` : base;
}
