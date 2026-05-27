import { useQuery } from '@tanstack/react-query';

export type EndpointStatus = 'ok' | 'down' | 'unknown';

type HealthCheck = {
  status: EndpointStatus;
  httpStatus?: number;
  checkedAt?: string;
};

export type HealthState = {
  live: HealthCheck;
  health: HealthCheck;
};

export function useHealthState(): HealthState {
  const live = useEndpointHealth('/live');
  const health = useEndpointHealth('/health');

  return { live, health };
}

function useEndpointHealth(path: string): HealthCheck {
  const query = useQuery({
    queryKey: ['endpoint-health', path],
    queryFn: async () => {
      const response = await fetch(path, { cache: 'no-store' });
      return {
        status: response.ok ? 'ok' : 'down',
        httpStatus: response.status,
        checkedAt: new Date().toISOString(),
      } satisfies HealthCheck;
    },
    staleTime: 5_000,
    refetchInterval: 15_000,
    retry: 0,
  });

  if (query.data) return query.data;
  if (query.isError) return { status: 'down' };
  return { status: 'unknown' };
}
