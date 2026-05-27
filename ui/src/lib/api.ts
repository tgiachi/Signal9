export class ApiError extends Error {
  status: number;
  body?: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

export async function apiText(url: string, init?: RequestInit): Promise<string> {
  const res = await fetch(url, init);
  if (!res.ok) {
    let body: unknown;
    try {
      body = await res.json();
    } catch {
      try {
        body = await res.text();
      } catch {
        body = undefined;
      }
    }
    throw new ApiError(res.status, res.statusText, body);
  }
  return res.text();
}

export async function apiJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  if (!res.ok) {
    let body: unknown;
    try {
      body = await res.json();
    } catch {
      body = undefined;
    }
    throw new ApiError(res.status, res.statusText, body);
  }
  return res.json() as Promise<T>;
}
