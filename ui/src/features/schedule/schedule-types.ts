export type ScheduleBlockRuleType = 'Pin' | 'Series' | 'TagPool';
export type ScheduledEntryKind = 'Media' | 'Bumper' | 'Commercial';

export type ScheduleBlock = {
  id: string;
  channelId: string;
  name: string;
  dayOfWeek: number;        // 0=Sun..6=Sat (matches System.DayOfWeek)
  startTime: string;        // "HH:mm:ss"
  durationMinutes: number;
  ruleType: ScheduleBlockRuleType;
  pinnedChannelMediaId: string | null;
  seriesName: string | null;
  seriesCursorChannelMediaId: string | null;
  tagFilterCsv: string | null;
  typeFilterCsv: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
};

export type ScheduleBlockInput = Omit<ScheduleBlock,
  'id' | 'channelId' | 'seriesCursorChannelMediaId' | 'createdAt' | 'updatedAt'>;

export type ScheduledEntry = {
  id: string;
  sourceBlockId: string | null;
  startAt: string;
  durationSeconds: number;
  kind: ScheduledEntryKind;
  channelMediaId: string;
  title: string;
  partIndex: number;
  partCount: number;
  mediaOffsetSeconds: number;
};

export type ScheduleTimeline = {
  channelId: string;
  from: string;
  to: string;
  entries: ScheduledEntry[];
};

export type ScheduleNow = {
  current: ScheduledEntry | null;
  next: ScheduledEntry | null;
  secondsIntoCurrent: number;
  computedAt: string;
};
