import { http, HttpResponse } from 'msw';
import configToml from './fixtures/config.toml?raw';

let stored = configToml;

export const handlers = [
  http.get(
    '/api/config',
    () => new HttpResponse(stored, { headers: { 'Content-Type': 'text/plain' } }),
  ),
  http.post('/api/config', async ({ request }) => {
    const text = await request.text();
    if (text.includes('BAD')) {
      return HttpResponse.json(
        { message: 'Synthetic validation error', line: 1, column: 1 },
        { status: 422 },
      );
    }
    stored = text;
    await new Promise((r) => setTimeout(r, 200));
    return new HttpResponse(null, { status: 200 });
  }),
];
