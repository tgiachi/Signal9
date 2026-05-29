import { useEffect, useRef } from 'react';
import Hls from 'hls.js';
import { useAuth } from '@/providers/auth-context';

export function StreamPlayer({ channelId }: { channelId: string }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const auth = useAuth();

  useEffect(() => {
    const video = videoRef.current;
    const token = auth.token;
    if (!video || !token) return;

    const url = `/api/channels/${channelId}/stream/index.m3u8?access_token=${encodeURIComponent(token)}`;

    if (Hls.isSupported()) {
      const hls = new Hls({ liveDurationInfinity: true, lowLatencyMode: false });
      hls.loadSource(url);
      hls.attachMedia(video);
      return () => hls.destroy();
    }
    if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = url;
    }
    return undefined;
  }, [channelId, auth.token]);

  return (
    <video
      ref={videoRef}
      controls
      autoPlay
      muted
      playsInline
      className="aspect-video w-full rounded-md bg-black"
    />
  );
}
