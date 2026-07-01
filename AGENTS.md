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
| **Controllers** | HTTP I/O: route, headers (`X-Tenant-Id`, `X-Idempotency-Key`), binding, status codes. Prefer `[Required]` / model validation; avoid business logic. | `api/Statevia.Service.Api/Controllers/` |
| **Services** | Use cases: orchestrate **repositories**, **display IDs**, **command dedup**, and the in-process **`IExecutionEngine`**. | `api/Statevia.Service.Api/Services/`, interfaces in `Abstractions/Services/` |
| **Repositories** | Persistence only: operate on `ICoreUnitOfWork.Db` passed by the caller. No `SaveChanges`, `BeginTransaction`, or `IDbContextFactory` in repository implementations. | `api/Statevia.Service.Api/Persistence/Repositories/`, interfaces in `Abstractions/Persistence/` |
| **UoW / Executor** | `ICoreUnitOfWork` + `ICoreUnitOfWorkFactory` own DbContext lifetime and transactions. `ICoreTransactionExecutor` runs ReadCommitted / ReadOnly use cases. `IExecutionMutationPersistence` owns Serializable retry for Cancel / Publish. | `api/Statevia.Service.Api/Persistence/` |
| **Engine** | Workflow execution (in-memory); Core-API calls it as a singleton. | `engine/` → `IExecutionEngine` / `ExecutionEngine` |

**Persistence note:** Application services decide commit boundaries via **`ICoreTransactionExecutor`** or **`IExecutionMutationPersistence`**. Repositories only mutate `uow.Db`. **`IDbContextFactory<CoreDbContext>`** is closed inside **`CoreUnitOfWork`** (not in services or repositories). Read-model assembly (`ExecutionReadModelService`, `GraphDefinitionService`) uses **`ExecuteReadOnlyAsync`** on the executor.

**event_store:** Appends go through **`IEventStoreRepository.AppendAsync(ICoreUnitOfWork uow, …)`** inside the caller’s transaction. **Start** uses one ReadCommitted commit (`executions` + `execution_graph_snapshots` + `execution_cursors` / `execution_waits` + `event_store` + optional `command_dedup`). **Cancel / Publish** use **tx1** (RECEIVED on `event_delivery_dedup`, ReadCommitted) then **tx2** (projection + operational projection + `event_store` + `command_dedup` + APPLIED, Serializable with retry via `IExecutionMutationPersistence`). Event kinds: **`EventStoreEventType`**（`WorkflowStarted` / `WorkflowCancelled` 等の種別文字列は DSL 由来で維持）。Projection authority remains `executions` + `execution_graph_snapshots`. 監査テーブル **`execution_events`** はスキーマ先行（INSERT 経路は未実装）。

**Read-model authority (STV-416):** HTTP の `GET /v1/executions/{id}` / `GET /v1/executions/{id}/graph` は **DB projection（`executions` / `execution_graph_snapshots`）を正**とする。`GetSnapshot` / `ExportExecutionGraph` は in-process のランタイムビューであり、将来コールバック経路導入後は最終永続状態と一致しない可能性がある。

**Wait / cursor（task 8）:** `execution_cursors`（operational projection）と `execution_waits`（**EventWait** の durable wait のみ）を、`executions` / `execution_graph_snapshots` 更新と **同一トランザクション**で同期する。read-model GET の正本は cursor に依存しない。`wait_kind` 語彙は **EventWait / CallbackWait / DelayWait**。詳細は `.spec-workflow/specs/execution-platform-data-model/design.md` と `docs/statevia-data-integration-contract.md` §STV-413。

**Dependency injection (`Program.cs`):**

- **Singleton:** `IExecutionEngine`, `IDefinitionCompilerService`, `IIdGenerator` (as registered).
- **Scoped:** Services (`IDefinitionService`, `IExecutionService`, …), repositories, `IDbContextFactory`-backed helpers.
- **DbContext:** `AddDbContextFactory<CoreDbContext>` (Npgsql; `DATABASE_URL` normalized from `postgres://` when needed).

**Errors and validation (contract `docs/statevia-data-integration-contract.md` §7):**

- **`ApiExceptionFilter`:** maps `NotFoundException` → 404, `ArgumentException` → 422, others → 500 with `{ "error": { "code", "message", … } }`.
- **`ApiBehaviorOptions.InvalidModelStateResponseFactory`:** ASP.NET model validation failures → 422 with the same envelope.

Further HTTP contract: `docs/core-api-interface.md`.

**Input/Output exposure policy (IO-14):** Start 時の `input` / state `output` are not returned by default in list/get APIs; when they appear in graph/snapshot payloads for debugging, clients must treat them as potentially sensitive and apply masking/size controls before external logging. `LogRedaction` masks `input` and `output` keys in log snapshots.

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
   cd api && dotnet run --project Statevia.Service.Api
   ```

   Or with env: `DATABASE_URL="postgres://statevia:statevia@localhost:5432/statevia" ASPNETCORE_URLS="http://0.0.0.0:8080" dotnet run --project Statevia.Service.Api --no-launch-profile`
   **Gotcha**: `launchSettings.json` hardcodes ports 62427/62428. Use `--no-launch-profile` + `ASPNETCORE_URLS` to bind to port 8080.
   Migrations: `cd api && dotnet ef database update --project Statevia.Service.Api`.

   **OpenAPI / Scalar**: Core-API 起動後、`/scalar/v1` で閲覧（OpenAPI JSON は `/swagger/v1/swagger.json`）。`docker compose` の core-api は `ASPNETCORE_ENVIRONMENT=Development` で有効。本番イメージ単体（Production）では既定オフ — `STATEVIA_ENABLE_API_DOCS=true` で有効化。export は `.\scripts\export-core-api-openapi.ps1`。
3. **ui** — run dev server:

   ```bash
   cd services/ui && CORE_API_INTERNAL_BASE="http://localhost:8080" npm run dev
   ```

   UI は `/api/core/executions/*` 等のプロキシ経由で Core-API（C#）の `/v1/definitions` と `/v1/executions` を利用する。

### Docker gotcha (Cloud VM)

The Cloud VM runs inside a container. Docker needs `fuse-overlayfs` storage driver and `iptables-legacy`. The daemon must be started manually with `sudo dockerd`.

### Tests

- **engine (C#):** `cd engine && dotnet test statevia-engine.sln` — xunit
- **core-api (C#):** `cd api && dotnet test statevia-api.sln` — xunit。厳格 Analyzer・Sonar 手順は `docs/development-guidelines.md` §4.3 / §5.1
- **ui:** from `services/ui/` — `npm run lint`, `npm run typecheck`, `npm run test:run` (vitest). Sonar 前は `npm run test:coverage`。一括スキャンはリポジトリルートから `./sonar/sonar-scanner-ui.ps1`（手順は `docs/development-guidelines.md` §5.2）

### Lint

- **UI:** ESLint 9（`eslint.config.js`）— `npm run lint` / `npm run lint:fix`。型チェックは `npm run typecheck`（`tsc --noEmit`）。
- **Core-API / Engine:** `dotnet build` の Analyzer 警告方針は `docs/development-guidelines.md` §4.3。

Comment rules, Markdownlint (e.g. `.spec-workflow/`), build or analyzer warnings, and Sonar/coverage: see **`docs/development-guidelines.md`** (sections 4.1–4.3, 5.1–5.2).

### Key env vars

| Variable                 | Service       | Default                                                                                                                |
| ------------------------ | ------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `DATABASE_URL`           | core-api (C#) | `Host=localhost;Database=statevia;Username=statevia;Password=statevia` (or `postgres://core:core@localhost:5432/core`) |
| `PORT`                   | core-api (C#) | `8080` (via ASP.NET Core)                                                                                              |
| `CORE_API_INTERNAL_BASE` | ui            | `http://localhost:8080`                                                                                                |
| `STATEVIA_LOG_HTTP_BODIES` | core-api (C#) | 未設定時は従来どおり。`true` のとき本番でも HTTP リクエスト/レスポンス本文をログに載せる（機密に注意）。 |
| `STATEVIA_ENABLE_API_DOCS` | core-api (C#) | 未設定時は Production で OpenAPI / Scalar 無効。`true` で Staging / Production でも `/swagger`・`/scalar` を公開（API 構造露出に注意）。 |
| `STATEVIA_MODULES_PATH` | core-api (C#) | 未設定時は `Statevia:Modules:Path` → `{ContentRoot}/modules` の順で Action Module ルートを解決。 |
| `Statevia:ActionHost:BaseUrl` | core-api (C#) | 未設定時は OutOfProcess 実行不可（`ActionHostNotConfigured`）。ローカル例: `http://localhost:5001`。docker compose では `http://action-host:5001`。 |
| `Statevia:ExecutionPolicy:DeploymentProfile` | core-api (C#) | 未設定時は `null`。`saas-shared` 等で Untrusted Action の Container 強制など Policy マトリクスに反映。 |
| `Statevia:ExecutionPolicy:Tenants:{tenantId}:MinimumMode` | core-api (C#) | Tenant スコープの実行下限（`ActionExecutionMode` 名）。`IExecutionPolicyProvider` 経由で base（TrustLevel×Env）へ最厳優先で重なる。緩和不可（base より緩い指定は無視）。未設定時は階層下限なし。 |
| `Statevia:Modules:Signing:TrustedSignerFingerprints` | core-api (C#) | Module 署名者の信頼フィンガープリント（公開鍵 SubjectPublicKeyInfo の SHA-256, 16 進）配列。署名有効かつ含まれる場合のみ `Verified`、含まれなければ `Signed`。未設定時は信頼署名者なし。 |
| `Statevia:Modules:Signing:RequireSignature` | core-api (C#) | 未設定時は `false`（署名なし Module は `Community` 登録）。`true` で署名なし Module の登録を skip。 |
| `Statevia:Modules:Oci:Enabled` | core-api (C#) | 未設定時は `false`（filesystem Source のみ＝後方互換）。`true` で OCI registry Source を有効化し、複数 Source を `CompositeModuleSource` が `Priority` 昇順で集約。 |
| `Statevia:Modules:Oci:Priority` | core-api (C#) | OCI Source の集約優先度（小さいほど優先）。未設定時は `200`（filesystem=100 より低く、同名 Module はローカル配置を優先）。 |
| `Statevia:Modules:Oci:CacheRoot` | core-api (C#) | OCI Module の materialize 先絶対パス。未設定時は `{ContentRoot}/oci-modules-cache`（filesystem modules ルートと分離し二重 discover を回避）。 |
| `Statevia:Modules:Oci:Artifacts` | core-api (C#) | 取得対象 OCI artifact 配列（`Registry` / `Repository` / `Reference`(tag or digest) ＋任意 `Username`/`Password`/`RefreshToken`/`PlainHttp`）。認証情報は機密のためログ非出力。**認証情報（`Username`/`Password`/`RefreshToken`）を一切設定しない場合は匿名 pull**（公開レジストリ向け。ORAS の匿名トークン経路を使用）。レイヤ media type は `application/vnd.statevia.module.layer.v1+zip`（無ければ単一レイヤ）。 |

### Core-API: Action 実行プラットフォーム（Phase 2）

- **契約:** `shared/Statevia.Actions.Abstractions` — `ActionDescriptor`, `ModuleDescriptor`, `IActionCatalog`, `IActionVisibilityResolver`, `IActionExecutionPolicy`, `IActionExecutor`。
- **責務分離:** Catalog（解決・メタデータ保持）/ VisibilityResolver（テナント境界）/ Policy（`ConfigurableExecutionPolicy` — base＝TrustLevel × Environment、`IExecutionPolicyProvider` による階層下限を最厳優先で合成、Hints、いずれも緩和不可）/ Executor（`DispatchingActionExecutor` → `IActionExecutionBackendSelector` → 各 `IActionExecutionBackend`）。
- **階層 Execution Policy:** `IExecutionPolicyProvider` が scope（Organization / Project / Environment / Tenant）別の `ExecutionPolicy`（`MinimumMode`）を返し、`ConfigurableExecutionPolicy` が base 下限へ `Strictness.Max` で重ねる。どの階層も base を緩和できない。本フェーズ実装は Tenant scope（`TenantExecutionPolicyProvider`、appsettings の `Statevia:ExecutionPolicy:Tenants`）のみ。
- **Engine 境界:** `ExecutionEngine` は `IStateExecutor` のみ呼び出す。Catalog / Policy / ModuleHost は Engine 非依存。
- **Module:** `ModuleHost` が filesystem Module を ALC load し `ActionDescriptor`（`Visibility=Tenant`, `OwnerTenantId`）+ `ModuleDescriptor` を Catalog / load catalog へ登録。`TrustLevel` は `ModuleSignatureVerifier` の署名検証結果で決定（署名なし=`Community` / 有効+信頼=`Verified` / 有効+未信頼=`Signed` / 検証失敗=`Untrusted`。`Statevia:Modules:Signing`）。
- **Module Source 供給パイプライン:** ModuleHost が consume するのは複数 `IModuleSource` を集約した `CompositeModuleSource`（明示 `Priority` 昇順・DI 順非依存、同名 Module は高優先勝ち+warning、同 Priority は `SourceLabel` で tie-break）。リモート Source は `MaterializingModuleSourceBase` 派生が acquire→cache→verify→extract→materialize を担い、Module を `MaterializedModule`（`shared/Statevia.Modules`・ローカル正本）へ materialize して `DiscoveredModule` へ射影。最初のリモート実装が **`OciModuleSource`**（`IOciArtifactFetcher` / OrasProject.Oras。`Statevia:Modules:Oci`、既定 `Priority=200`）。version coexist（同一 moduleId の複数版共存）は責務分離を設計確定済みで実装は未着手（Catalog=fullVersion 保持/検索のみ → Compiler 配下 `VersionResolver` が range→具体版を解決 → `ResolvedModuleReference` を不変 Definition に保存 → Runtime は再解決せず exact lookup。actionId は版を含めず論理 ID 維持）。
- **OutOfProcess:** Production 等で Policy が `OutOfProcess` を返す Community / Verified Module は Action Host（`Statevia:ActionHost:BaseUrl`）へ gRPC dispatch。未設定時は `ActionHostNotConfigured`。
- **Container / Wasm（Phase 4 スタブ）:** Policy が `Container`（例: Untrusted × `saas-shared`）/ `Wasm` を返す経路は `ContainerActionBackend` / `WasmActionBackend` が受ける。実体の隔離は `IActionSandboxRuntime` 実装に委譲し、`Statevia:ExecutionPolicy:Sandbox`（`ContainerProvider` / `WasmProvider` ＋ CPU/メモリ/タイムアウト上限）で選択する。ランタイム未構成・未登録時は安全側に `SandboxRuntimeNotConfigured` で Failed。Engine 内部状態（`StateContext.Events` / `Store`）は委譲しない。`Remote` は Backend 未登録のため引き続き `UnsupportedExecutionMode`。

### Core-API: HTTP リクエストログ（STV-403）

- **ミドルウェア** `RequestLoggingMiddleware` が **CORS より前**に実行され、各リクエストで **開始**・**完了**（およびミドルウェア境界の **未処理例外**）を `ILogger` に出力する。
- **相関 ID**: 優先順は `traceparent`（W3C）→ `X-Trace-Id` → `X-Request-Id` → 生成 UUID（32 hex）。`HttpContext.Items["Statevia.TraceId"]` にも格納する。
- **ログ項目（概要）**: `TraceId`, `Method`, `Path`（クエリなし）, `Query`, `TenantId`, `UserAgent`, 任意で `RequestBody` / `ResponseBody` スナップショット（**マスキング・長さ上限あり**）。本番では **本文ログは既定オフ**（`RequestLogOptions`）。開発環境では既定オン。本番で本文を有効化する場合は **`STATEVIA_LOG_HTTP_BODIES=true`**（IO-14 に照らし外部ログへ流す前はマスキングを確認すること）。

### Engine: 実行ログ（STV-404）

- **`ExecutionEngine`** は `Microsoft.Extensions.Logging` の **同期 `ILogger<ExecutionEngineLogger>`** をコンストラクタで受け取る（Core-API は `AddStateviaExecutionEngine` で登録）。テスト等では `NullLogger` を渡してよい。プロバイダ例外は **ログ呼び出しを try/catch** して遷移に伝播させない。
- **主な項目（概要）**: `Execution started`（`ExecutionId`, `DefinitionName`, `InitialState`）、`State scheduled` / `State completed`（`StateName`, `NodeId`, `Fact`、通常状態は **`ElapsedMs`**＝`ExecuteAsync` 前後の壁時計。Wait の待機込み）、`State execute failed` / `Execution terminal failure`（Error）、`Execution completed`。Join 合成ノードの完了ログには **`ElapsedMs` を付けない**。

### .NET SDK

.NET 8 is installed to `/usr/local/share/dotnet`. The PATH is configured in `~/.bashrc`.
