# AGENTS.md

## Cursor Cloud specific instructions

開発者向けのコーディング方針（変更スコープ、コンポーネント別ルール、コミット方針など）は **`docs/development-guidelines.md`** を参照してください。

### Architecture overview

Statevia is a definition-driven, event-sourced workflow engine with three components:

| Component                     | Stack                       | Location       |
| ----------------------------- | --------------------------- | -------------- |
| **engine** (C# library + CLI) | .NET 8                      | `engine/`      |
| **core-api** (REST API)       | **C# ASP.NET Core**           | `api/`         |
| **ui** (Web dashboard)        | Next.js / React / ReactFlow | `services/ui/` |

Core-API は C# のみ。PostgreSQL 16 は EF Core 経由で使用。UI は Next.js の route handler で API にプロキシして CORS を避けられる。

### Core-API — layers, DI, persistence

| Layer | Role | Location / types |
| ----- | ---- | ---------------- |
| **Controllers** | HTTP I/O: route, headers (`X-Tenant-Id`, `X-Idempotency-Key`), binding, status codes. Prefer `[Required]` / model validation; avoid business logic. | `api/Statevia.Core.Api/Controllers/` |
| **Services** | Use cases: orchestrate **repositories**, **display IDs**, **command dedup**, and the in-process **`IWorkflowEngine`**. | `api/Statevia.Core.Api/Services/`, interfaces in `Abstractions/Services/` |
| **Repositories** | Persistence only: EF Core `CoreDbContext` via `IDbContextFactory<CoreDbContext>` inside repository implementations. | `api/Statevia.Core.Api/Persistence/Repositories/`, interfaces in `Abstractions/Persistence/` |
| **Engine** | Workflow execution (in-memory); Core-API calls it as a singleton. | `engine/` → `IWorkflowEngine` / `WorkflowEngine` |

**Persistence note:** Write paths go through **repositories** from `WorkflowService` / `DefinitionService`. Some **read-model** assembly (`ExecutionReadModelService`, `GraphDefinitionService`) uses `IDbContextFactory<CoreDbContext>` directly to keep projection queries localized without duplicating repository surface for every report shape.

**event_store:** `IEventStoreRepository` can append in isolation (own `DbContext` + **Serializable** tx) or via **`AppendAsync(CoreDbContext db, …)`** so **`WorkflowService`** can commit **workflows + execution_graph_snapshots + event_store + command_dedup** in **one** `SaveChanges` + outer transaction (`ReadCommitted` on start, `Serializable` on cancel/publish when seq races matter). Event kinds: **`EventStoreEventType`**. This is not a full engine event log; projection remains `workflows` + `execution_graph_snapshots`.

**Dependency injection (`Program.cs`):**

- **Singleton:** `IWorkflowEngine`, `IDefinitionCompilerService`, `IIdGenerator` (as registered).
- **Scoped:** Services (`IDefinitionService`, `IWorkflowService`, …), repositories, `IDbContextFactory`-backed helpers.
- **DbContext:** `AddDbContextFactory<CoreDbContext>` (Npgsql; `DATABASE_URL` normalized from `postgres://` when needed).

**Errors and validation (contract `docs/statevia-data-integration-contract.md` §7):**

- **`ApiExceptionFilter`:** maps `NotFoundException` → 404, `ArgumentException` → 422, others → 500 with `{ "error": { "code", "message", … } }`.
- **`ApiBehaviorOptions.InvalidModelStateResponseFactory`:** ASP.NET model validation failures → 422 with the same envelope.

Further HTTP contract: `docs/core-api-interface.md`.

**Input/Output exposure policy (IO-14):** `workflowInput` / state `output` are not returned by default in list/get APIs; when they appear in graph/snapshot payloads for debugging, clients must treat them as potentially sensitive and apply masking/size controls before external logging.

### Docker Compose（運用）

`docker-compose.yml` の起動手順・注意点は **`docs/operations-docker.md`** を参照。

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

   UI は `/api/core/workflows/*` 等のプロキシ経由で Core-API（C#）の `/v1/definitions` と `/v1/workflows` を利用する。

### Docker gotcha (Cloud VM)

The Cloud VM runs inside a container. Docker needs `fuse-overlayfs` storage driver and `iptables-legacy`. The daemon must be started manually with `sudo dockerd`.

### Tests

- **engine (C#):** `cd engine && dotnet test statevia-engine.sln` — xunit
- **core-api (C#):** `cd api && dotnet test statevia-api.sln` — xunit (when tests exist)
- **ui:** `npm run test:run` from `services/ui/` — vitest

### Lint

No ESLint is configured. TypeScript compilation (`tsc --noEmit`) serves as the primary code quality check. The UI test files have minor pre-existing TS2783 warnings (harmless spread-override pattern).

Comment rules, Markdownlint (e.g. `.spec-workflow/`), and how to treat build or analyzer warnings: see **`docs/development-guidelines.md`** (sections 4.1–4.2).

### Key env vars

| Variable                 | Service       | Default                                                                                                                |
| ------------------------ | ------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `DATABASE_URL`           | core-api (C#) | `Host=localhost;Database=statevia;Username=statevia;Password=statevia` (or `postgres://core:core@localhost:5432/core`) |
| `PORT`                   | core-api (C#) | `8080` (via ASP.NET Core)                                                                                              |
| `CORE_API_INTERNAL_BASE` | ui            | `http://localhost:8080`                                                                                                |
| `STATEVIA_LOG_HTTP_BODIES` | core-api (C#) | 未設定時は従来どおり。`true` のとき本番でも HTTP リクエスト/レスポンス本文をログに載せる（機密に注意）。 |

### Core-API: HTTP リクエストログ（STV-403）

- **ミドルウェア** `RequestLoggingMiddleware` が **CORS より前**に実行され、各リクエストで **開始**・**完了**（およびミドルウェア境界の **未処理例外**）を `ILogger` に出力する。
- **相関 ID**: 優先順は `traceparent`（W3C）→ `X-Trace-Id` → `X-Request-Id` → 生成 UUID（32 hex）。`HttpContext.Items["Statevia.TraceId"]` にも格納する。
- **ログ項目（概要）**: `TraceId`, `Method`, `Path`（クエリなし）, `Query`, `TenantId`, `UserAgent`, 任意で `RequestBody` / `ResponseBody` スナップショット（**マスキング・長さ上限あり**）。本番では **本文ログは既定オフ**（`RequestLogOptions`）。開発環境では既定オン。本番で本文を有効化する場合は **`STATEVIA_LOG_HTTP_BODIES=true`**（IO-14 に照らし外部ログへ流す前はマスキングを確認すること）。

### .NET SDK

.NET 8 is installed to `/usr/local/share/dotnet`. The PATH is configured in `~/.bashrc`.
