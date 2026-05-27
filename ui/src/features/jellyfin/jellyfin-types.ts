export type JellyfinConnectionStatus = {
  isConfigured: boolean;
  baseUrl: string | null;
  lastVerifiedAt: string | null;
};

export type JellyfinConnectionInput = {
  baseUrl: string;
  apiKey: string;
};

export type JellyfinServerInfo = {
  serverName: string;
  version: string;
  id: string;
};

export type JellyfinLibrarySummary = {
  id: string;
  name: string;
  collectionType: string | null;
};
