# AGENTS.md

## Cursor Cloud specific instructions

### Architecture overview

Statevia is a definition-driven, event-sourced workflow engine with three components:

| Component | Stack | Location |
|---|---|---|
| **core** (C# library + CLI) | .NET 8 | `core/` |
| **core-api** (REST API) | Node.js / Express / TypeScript | `services/core-api/` |
| **ui** (Web dashboard) | Next.js / React / ReactFlow | `services/ui/` |

PostgreSQL 16 is the event store. The UI proxies API calls through a Next.js route handler to avoid CORS.

### Running services

1. **PostgreSQL** — run via Docker:
   ```
   sudo docker run -d --name statevia-postgres \
     -e POSTGRES_USER=core -e POSTGRES_PASSWORD=core -e POSTGRES_DB=core \
     -p 5432:5432 \
     -v $(pwd)/services/core-api/sql:/docker-entrypoint-initdb.d:ro \
     postgres:16
   ```
2. **core-api** — build then run (requires PostgreSQL):
   ```
   cd services/core-api && npm run build && DATABASE_URL="postgres://core:core@localhost:5432/core" PORT=8080 npm run dev
   ```
   The `dev` script uses `node --watch dist/server.js`, so you must `npm run build` first; it watches the compiled JS, not the TS source.
3. **ui** — run dev server:
   ```
   cd services/ui && CORE_API_INTERNAL_BASE="http://localhost:8080" npm run dev
   ```

### Docker gotcha (Cloud VM)

The Cloud VM runs inside a container. Docker needs `fuse-overlayfs` storage driver and `iptables-legacy`. The daemon must be started manually with `sudo dockerd`.

### Tests

- **core (C#):** `dotnet test` from `core/` — 91 tests (xunit). **v2 改修時**: `engine/` を使用。`cd engine && dotnet test statevia-engine.sln` — 91 tests。
- **core-api:** `npm test` from `services/core-api/` — 151 tests (vitest, no DB needed)
- **ui:** `npm run test:run` from `services/ui/` — 178 tests (vitest)

### Lint

No ESLint is configured. TypeScript compilation (`tsc --noEmit`) serves as the primary code quality check. The UI test files have minor pre-existing TS2783 warnings (harmless spread-override pattern).

### Key env vars

| Variable | Service | Default |
|---|---|---|
| `DATABASE_URL` | core-api | `postgres://core:core@localhost:5432/core` |
| `PORT` | core-api | `8080` |
| `CORE_API_INTERNAL_BASE` | ui | `http://localhost:8080` |

### .NET SDK

.NET 8 is installed to `/usr/local/share/dotnet`. The PATH is configured in `~/.bashrc`.
