export type EffectParameterDescriptor = {
  name: string;
  label: string;
  type: 'number';
  min: number | null;
  max: number | null;
  step: number | null;
  default: number;
};

export type EffectCatalogItem = {
  kind: string;
  label: string;
  description: string;
  parameters: EffectParameterDescriptor[];
};

export type ChannelEffect = {
  kind: string;
  enabled: boolean;
  params: Record<string, number>;
};

export type ChannelStreamSnapshot = {
  running: boolean;
  channelId: string;
  currentEntryId: string | null;
  currentEntryTitle: string | null;
  currentEntryKind: string | null;
  currentEntryPartIndex: number | null;
  currentEntryPartCount: number | null;
  nextSegmentNumber: number;
  ffmpegPid: number | null;
  lastViewerAt: string;
  willStopAt: string;
};
