export function formatRelativeTime(isoString: string, nowMs?: number): string {
  const now = nowMs ?? Date.now();
  const deltaMs = now - new Date(isoString).getTime();
  const deltaSeconds = Math.floor(deltaMs / 1_000);

  if (deltaSeconds < 5) return 'just now';
  if (deltaSeconds < 60) return `${deltaSeconds}s ago`;

  const deltaMinutes = Math.floor(deltaSeconds / 60);
  if (deltaMinutes < 60) return `${deltaMinutes}m ago`;

  const deltaHours = Math.floor(deltaMinutes / 60);
  if (deltaHours < 24) return `${deltaHours}h ago`;

  const deltaDays = Math.floor(deltaHours / 24);
  return `${deltaDays}d ago`;
}
