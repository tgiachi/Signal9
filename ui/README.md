# SignalNine UI

Vite + React + TS frontend for the SignalNine operator console (Broadcast Control Room theme). Build output goes to `../src/SignalNine.Web/wwwroot/` (gitignored), served statically by ASP.NET in production.

## Develop

Against real backend on `:5001`:

```bash
npm install
npm run dev
# http://localhost:5173 — proxies /api and /hub to :5001
```

In isolation (no backend running):

```bash
npm run dev:mocks
# MSW + mock SignalR drive the UI from fixtures
```

## Build

```bash
npm run build
# Outputs to ../src/SignalNine.Web/wwwroot/
```

## Test / quality

```bash
npm test            # vitest run
npm run test:watch  # vitest watch
npm run typecheck
npm run lint
npm run format
```

## Backend contract (assumption — not implemented in this repo)

- `GET /api/config` → `200 text/plain` raw TOML
- `POST /api/config` → `text/plain` body; `200` ok / `422 { message, line?, column? }`
- `/hub/logs` SignalR → server invokes client method `log(entry)` with `LogEntry` shape:
  ```ts
  type LogEntry = {
    ts: string;            // ISO 8601
    level: 'debug' | 'info' | 'warn' | 'error';
    source: string;
    message: string;
    props?: Record<string, unknown>;
  };
  ```

The backend team adds `app.UseDefaultFiles().UseStaticFiles()` to `Program.cs` (one line) when the SPA is ready to serve.
