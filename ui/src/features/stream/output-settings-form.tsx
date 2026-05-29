import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { useChannels } from '@/features/channels/use-channels';

type Props = {
  channelId: string;
  initial: { outputWidth: number; outputHeight: number; outputVideoBitrateKbps: number };
};

export function OutputSettingsForm({ channelId, initial }: Props) {
  const [w, setW] = useState<number>(initial.outputWidth);
  const [h, setH] = useState<number>(initial.outputHeight);
  const [b, setB] = useState<number>(initial.outputVideoBitrateKbps);
  const [saving, setSaving] = useState(false);

  const { channels, updateChannel } = useChannels();
  const channel = channels.find((c) => c.id === channelId);

  useEffect(() => {
    setW(initial.outputWidth);
    setH(initial.outputHeight);
    setB(initial.outputVideoBitrateKbps);
  }, [initial.outputWidth, initial.outputHeight, initial.outputVideoBitrateKbps]);

  const save = async () => {
    if (!channel) return;
    setSaving(true);
    try {
      await updateChannel({
        id: channelId,
        input: {
          name: channel.name,
          slug: channel.slug,
          description: channel.description,
          logoUrl: channel.logoUrl,
          displayOrder: channel.displayOrder,
          isActive: channel.isActive,
          commercialsEnabled: channel.commercialsEnabled,
          commercialIntervalMinSeconds: channel.commercialIntervalMinSeconds,
          commercialIntervalMaxSeconds: channel.commercialIntervalMaxSeconds,
          outputWidth: w,
          outputHeight: h,
          outputVideoBitrateKbps: b,
          videoEffectsJson: channel.videoEffectsJson,
        },
      });
      toast.success('Output salvato');
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="rounded-md bg-bg-2 p-3">
      <h3 className="mb-2 text-[12px] font-semibold uppercase tracking-wide text-fg-2">Output</h3>
      <div className="grid grid-cols-3 gap-2">
        <label className="text-[11px] text-fg-2">
          Width
          <input
            type="number"
            min={320}
            max={3840}
            value={w}
            onChange={(e) => setW(Number(e.target.value))}
            className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
          />
        </label>
        <label className="text-[11px] text-fg-2">
          Height
          <input
            type="number"
            min={180}
            max={2160}
            value={h}
            onChange={(e) => setH(Number(e.target.value))}
            className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
          />
        </label>
        <label className="text-[11px] text-fg-2">
          Bitrate (kbps)
          <input
            type="number"
            min={300}
            max={20000}
            value={b}
            onChange={(e) => setB(Number(e.target.value))}
            className="mt-1 w-full rounded bg-bg-1 px-2 py-1 text-fg-0"
          />
        </label>
      </div>
      <div className="mt-2 flex justify-end">
        <button
          type="button"
          onClick={save}
          disabled={saving || !channel}
          className="rounded bg-accent-live px-3 py-1.5 text-[12px] font-medium text-bg-5 hover:bg-accent-live-hover"
        >
          Salva output
        </button>
      </div>
    </div>
  );
}
