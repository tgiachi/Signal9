export type ChannelResponse = {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  displayOrder: number;
  isActive: boolean;
  commercialsEnabled: boolean;
  commercialIntervalMinSeconds: number;
  commercialIntervalMaxSeconds: number;
  outputWidth: number;
  outputHeight: number;
  outputVideoBitrateKbps: number;
  videoEffectsJson: string | null;
  createdAt: string;
  updatedAt: string;
};

export type ChannelFormValues = {
  id: string | null;
  name: string;
  slug: string;
  description: string;
  logoUrl: string;
  displayOrder: number;
  isActive: boolean;
  commercialsEnabled: boolean;
  commercialIntervalMinSeconds: number;
  commercialIntervalMaxSeconds: number;
};

export type CreateChannelInput = {
  name: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  displayOrder: number;
  commercialsEnabled: boolean;
  commercialIntervalMinSeconds: number;
  commercialIntervalMaxSeconds: number;
};

export type UpdateChannelInput = CreateChannelInput & {
  isActive: boolean;
  outputWidth?: number;
  outputHeight?: number;
  outputVideoBitrateKbps?: number;
  videoEffectsJson?: string | null;
};

export type ChannelLogoUploadResponse = {
  logoUrl: string;
};

export const EMPTY_CHANNEL_FORM: ChannelFormValues = {
  id: null,
  name: '',
  slug: '',
  description: '',
  logoUrl: '',
  displayOrder: 0,
  isActive: true,
  commercialsEnabled: true,
  commercialIntervalMinSeconds: 120,
  commercialIntervalMaxSeconds: 300,
};

export function channelToForm(channel: ChannelResponse): ChannelFormValues {
  return {
    id: channel.id,
    name: channel.name,
    slug: channel.slug,
    description: channel.description ?? '',
    logoUrl: channel.logoUrl ?? '',
    displayOrder: channel.displayOrder,
    isActive: channel.isActive,
    commercialsEnabled: channel.commercialsEnabled,
    commercialIntervalMinSeconds: channel.commercialIntervalMinSeconds,
    commercialIntervalMaxSeconds: channel.commercialIntervalMaxSeconds,
  };
}

export function formToCreateInput(form: ChannelFormValues): CreateChannelInput {
  return {
    name: form.name.trim(),
    slug: form.slug.trim(),
    description: nullableText(form.description),
    logoUrl: nullableText(form.logoUrl),
    displayOrder: form.displayOrder,
    commercialsEnabled: form.commercialsEnabled,
    commercialIntervalMinSeconds: form.commercialIntervalMinSeconds,
    commercialIntervalMaxSeconds: form.commercialIntervalMaxSeconds,
  };
}

export function formToUpdateInput(form: ChannelFormValues): UpdateChannelInput {
  return {
    ...formToCreateInput(form),
    isActive: form.isActive,
  };
}

function nullableText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
