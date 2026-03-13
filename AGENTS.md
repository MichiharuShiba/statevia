# AGENTS.md

## Cursor Cloud specific instructions

### Architecture overview

Statevia is a definition-driven, event-sourced workflow engine with three components:

| Component                     | Stack                       | Location       |
| ----------------------------- | --------------------------- | -------------- |
| **engine** (C# library + CLI) | .NET 8                      | `engine/`      |
| **core-api** (REST API)       | **C# ASP.NET Core** (v2)    | `api/`         |
| **ui** (Web dashboard)        | Next.js / React / ReactFlow | `services/ui/` |

**v2**: Core-API は C# のみ。TypeScript の `services/core-api/` は v2 では使用しない（legacy はタグ `legacy/core-api-ts` で保存）。PostgreSQL 16 は EF Core 経由で使用。UI は Next.js の route handler で API にプロキシして CORS を避けられる。

### Running services

1. **PostgreSQL** — run via Docker:

   ```bash
   sudo docker run -d --name statevia-postgres \
     -e POSTGRES_USER=statevia -e POSTGRES_PASSWORD=statevia -e POSTGRES_DB=statevia \
     -p 5432:5432 \
     postgres:16
   ```

   C# API は EF Core マイグレーションでスキーマを作成するため、初期 SQL のマウントは不要。
2. **core-api (C#)** — run (requires PostgreSQL; run migrations first):

   ```bash
   cd api && dotnet run --project Statevia.Core.Api
   ```

   Or with env: `DATABASE_URL="postgres://statevia:statevia@localhost:5432/statevia" PORT=8080 dotnet run --project Statevia.Core.Api`
   Migrations: `cd api && dotnet ef database update --project Statevia.Core.Api`.
3. **ui** — run dev server:

   ```bash
   cd services/ui && CORE_API_INTERNAL_BASE="http://localhost:8080" npm run dev
   ```

   UI はプロキシ経由で Core-API（C#）の `/v1/definitions` と `/v1/workflows` を利用する（Phase 3 で切り替え済みを想定）。

### Docker gotcha (Cloud VM)

The Cloud VM runs inside a container. Docker needs `fuse-overlayfs` storage driver and `iptables-legacy`. The daemon must be started manually with `sudo dockerd`.

### Tests

- **engine (C#):** `cd engine && dotnet test statevia-engine.sln` — xunit
- **core-api (C#):** `cd api && dotnet test statevia-api.sln` — xunit (when tests exist)
- **ui:** `npm run test:run` from `services/ui/` — vitest

### Lint

No ESLint is configured. TypeScript compilation (`tsc --noEmit`) serves as the primary code quality check. The UI test files have minor pre-existing TS2783 warnings (harmless spread-override pattern).

### Key env vars

| Variable                 | Service       | Default                                                                                                                |
| ------------------------ | ------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `DATABASE_URL`           | core-api (C#) | `Host=localhost;Database=statevia;Username=statevia;Password=statevia` (or `postgres://core:core@localhost:5432/core`) |
| `PORT`                   | core-api (C#) | `8080` (via ASP.NET Core)                                                                                              |
| `CORE_API_INTERNAL_BASE` | ui            | `http://localhost:8080`                                                                                                |

### .NET SDK

.NET 8 is installed to `/usr/local/share/dotnet`. The PATH is configured in `~/.bashrc`.
