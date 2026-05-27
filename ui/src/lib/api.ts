export class ApiError extends Error {
  status: number;
  body?: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

let accessTokenProvider: (() => string | null) | null = null;

export function setApiAccessTokenProvider(provider: (() => string | null) | null): void {
  accessTokenProvider = provider;
}

function withAuth(init?: RequestInit): RequestInit | undefined {
  const token = accessTokenProvider?.();
  if (!token) return init;

  const headers = new Headers(init?.headers);
  if (!headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  return { ...init, headers };
}

async function readErrorBody(res: Response): Promise<unknown> {
  try {
    return await res.json();
  } catch {
    try {
      return await res.text();
    } catch {
      return undefined;
    }
  }
}

export async function apiText(url: string, init?: RequestInit): Promise<string> {
  const res = await fetch(url, withAuth(init));
  if (!res.ok) {
    const body = await readErrorBody(res);
    throw new ApiError(res.status, res.statusText, body);
  }
  return res.text();
}

export async function apiJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, withAuth(init));
  if (!res.ok) {
    const body = await readErrorBody(res);
    throw new ApiError(res.status, res.statusText, body);
  }
  return res.json() as Promise<T>;
}

export async function apiEmpty(url: string, init?: RequestInit): Promise<void> {
  const res = await fetch(url, withAuth(init));
  if (!res.ok) {
    const body = await readErrorBody(res);
    throw new ApiError(res.status, res.statusText, body);
  }
}
