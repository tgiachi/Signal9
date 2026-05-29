import { createBrowserRouter, Navigate } from 'react-router';
import { AppShell } from '@/components/shell/app-shell';
import { JobsPage } from '@/features/jobs/jobs-page';
import { LogsPage } from '@/features/logs/logs-page';
import { ConfigPage } from '@/features/config/config-page';
import { MonitorPage } from '@/features/monitor/monitor-page';
import { ChannelsPage } from '@/features/channels/channels-page';
import { JellyfinPage } from '@/features/jellyfin/jellyfin-page';
import { MediaLibrariesPage } from '@/features/media-libraries/media-libraries-page';
import { ChannelMediaPage } from '@/features/media/channel-media-page';
import { FfmpegPage } from '@/features/ffmpeg/ffmpeg-page';
import { PipelineConfigPage } from '@/features/pipelines/pipeline-config-page';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/monitor" replace /> },
      { path: 'monitor', element: <MonitorPage /> },
      { path: 'channels', element: <ChannelsPage /> },
      { path: 'media-libraries', element: <MediaLibrariesPage /> },
      { path: 'media', element: <ChannelMediaPage /> },
      { path: 'pipelines', element: <PipelineConfigPage /> },
      { path: 'ffmpeg', element: <FfmpegPage /> },
      { path: 'jobs', element: <JobsPage /> },
      { path: 'logs', element: <LogsPage /> },
      { path: 'settings/jellyfin', element: <JellyfinPage /> },
      { path: 'config', element: <ConfigPage /> },
      { path: '*', element: <div className="p-4 text-fg-1">404 — page not found</div> },
    ],
  },
]);
