import { useParams } from 'react-router';
import { useChannels } from '@/features/channels/use-channels';
import type { ChannelEffect } from './stream-types';
import { StreamPlayer } from './stream-player';
import { EffectChainEditor } from './effect-chain-editor';
import { OutputSettingsForm } from './output-settings-form';
import { StreamStatusCard } from './stream-status-card';

function parseEffects(json: string | null | undefined): ChannelEffect[] {
  if (!json) return [];
  try {
    const v = JSON.parse(json);
    return Array.isArray(v) ? (v as ChannelEffect[]) : [];
  } catch {
    return [];
  }
}

export function StreamPage() {
  const { channelId = '' } = useParams<{ channelId: string }>();
  const { channels } = useChannels();
  const channel = channels.find((c) => c.id === channelId);

  if (!channel) {
    return <div className="p-4 text-fg-2">Canale non trovato.</div>;
  }

  return (
    <div className="flex h-full flex-col gap-3 p-4">
      <div className="flex items-baseline gap-2">
        <h1 className="text-base font-semibold text-fg-0">{channel.name}</h1>
        <span className="text-[11px] text-fg-3">/ stream</span>
      </div>
      <StreamStatusCard channelId={channelId} />
      <div className="grid gap-3 md:grid-cols-2">
        <StreamPlayer channelId={channelId} />
        <EffectChainEditor channelId={channelId} initialEffects={parseEffects(channel.videoEffectsJson)} />
      </div>
      <OutputSettingsForm
        channelId={channelId}
        initial={{
          outputWidth: channel.outputWidth ?? 1280,
          outputHeight: channel.outputHeight ?? 720,
          outputVideoBitrateKbps: channel.outputVideoBitrateKbps ?? 2500,
        }}
      />
    </div>
  );
}
