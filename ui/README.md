# SignalNine UI

Vite + React + TS frontend for the SignalNine operator console (Broadcast Control Room theme). Build output goes to `../src/SignalNine.Web/wwwroot/` (gitignored), served statically by ASP.NET in production.

## Develop

```bash
npm install
npm run dev
# http://localhost:5173 — proxies /api and /hubs to the ASP.NET backend on :5001
```

Start the backend with `dotnet run --project src/SignalNine.Web` in another shell.

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

## Backend contract

- `GET /api/config` → `200 text/plain` raw TOML
- `POST /api/config` → `text/plain` body; `200` ok / `422 { message, line?, column? }`
- SignalR `/hubs/logs` → server invokes client method `log(entry)` with `LogEntry` shape:
  ```ts
  type LogEntry = {
    ts: string;            // ISO 8601
    level: 'debug' | 'info' | 'warn' | 'error';
    source: string;
    message: string;
    props?: Record<string, unknown>;
  };
  ```
