# AGENTS.md

Cursor Cloud / エージェント向けの**薄い索引**。詳細は [`docs/README.md`](docs/README.md) を正本とする。

## ドキュメント入口

| 目的 | 参照 |
| --- | --- |
| 利用者向け索引 | [`docs/README.md`](docs/README.md) |
| Quick Start | [`docs/guides/getting-started.md`](docs/guides/getting-started.md) |
| HTTP / データ契約 | [`docs/specifications/api-http.md`](docs/specifications/api-http.md), [`docs/specifications/data-integration.md`](docs/specifications/data-integration.md) |
| コーディング規約 | [`docs/development-guidelines.md`](docs/development-guidelines.md) |
| レイヤー・境界 | [`docs/architecture/overview.md`](docs/architecture/overview.md), [`docs/architecture/domain-model-boundaries.md`](docs/architecture/domain-model-boundaries.md) |

## リポジトリ構成（概要）

| コンポーネント | 場所 |
| --- | --- |
| engine（C# ライブラリ） | `core/engine/` |
| application | `core/application/` |
| infrastructure | `infrastructure/` |
| Service API（ASP.NET Core） | `service/api/` |
| runtime（共有 HostedService） | `service/runtime/Statevia.Runtime/` |
| scheduler（Generic Host・任意） | `service/runtime/Statevia.Service.Runtime.Scheduler/` |
| worker（Generic Host・任意） | `service/runtime/Statevia.Service.Runtime.Worker/` |
| action-host（gRPC） | `service/action-host/` |
| cli | `service/cli/` |
| ui（Next.js） | `ui/studio/` |

PostgreSQL 16 + EF Core。UI は `/api/core/*` で Service API にプロキシ。

## 起動

1. **PostgreSQL** — Docker 例: `docker compose up -d postgres`、または [`docs/guides/getting-started.md`](docs/guides/getting-started.md)
2. **マイグレーション** — `cd service/api && dotnet ef database update --project Statevia.Service.Api`
3. **Service API** — `cd service/api && dotnet run --project Statevia.Service.Api --no-launch-profile`（`ASPNETCORE_URLS=http://0.0.0.0:8080` 推奨。`launchSettings.json` は git 管理外で、VS がランダムポートを付ける）
4. **（任意）Scheduler / Worker 分離** — `docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml up -d`（API 内 HostedService Off と scheduler/worker 起動を同時に適用。手順: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)）
5. **UI** — `cd ui/studio && SERVICE_API_INTERNAL_BASE=http://localhost:8080 npm run dev`

Docker Compose 一式: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)

OpenAPI / Scalar: [`docs/reference/api-openapi.md`](docs/reference/api-openapi.md)

### Docker gotcha (Cloud VM)

`fuse-overlayfs` + `iptables-legacy`。`sudo dockerd` で手動起動が必要な場合あり。

## テスト

| 対象 | コマンド |
| --- | --- |
| engine | `cd core/engine && dotnet test statevia-engine.sln` |
| Service API | `cd service/api && dotnet test statevia-api.sln` |
| ui | `cd ui/studio && npm run lint && npm run typecheck && npm run test:run` |

Sonar / Analyzer: [`docs/development-guidelines.md`](docs/development-guidelines.md) §4.3 / §5

## 主要 env / 設定（抜粋）

| 変数 / 設定 | 用途 |
| --- | --- |
| `DATABASE_URL` | PostgreSQL 接続 |
| `SERVICE_API_INTERNAL_BASE` | UI → Service API |
| `STATEVIA_ENABLE_API_DOCS` | 本番で OpenAPI / Scalar を有効化 |
| `STATEVIA_MODULES_PATH` | Action Module ルート |
| `Statevia:ActionHost:BaseUrl` | OutOfProcess 実行（未設定時は `ActionHostNotConfigured`） |
| `Statevia:Modules:Signing:*` | Module 署名・TrustLevel |
| `Statevia:Modules:Oci:*` | OCI Module Source |
| `Statevia:Modules:S3:*` | S3 Module Source |
| `Statevia:Modules:Git:*` | Git Module Source（HTTP archive） |
| `Statevia:ExecutionPolicy:*` | 実行モード下限・テナント Policy / `Sandbox:Docker:*`（Container） |
| `Statevia:Runtime:*` | API 内 Worker / DelayWait Scheduler / OwnershipRecovery の On/Off（既定 On） |

完全な一覧と説明: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)、[`docs/specifications/actions/platform.md`](docs/specifications/actions/platform.md)

## 実装メモ（エージェント向け）

- **IDE sln**: ルート `statevia.sln` は Cursor / VS Code の IntelliSense 用（`.vscode/settings.json` の `dotnet.defaultSolution`）。CI / テスト / Warning 0 / Sonar はコンポーネント別 6 sln。
- **Read-model**: `GET /v1/executions` / graph は DB projection 正本（[`data-integration.md`](docs/specifications/data-integration.md)）。Hosted 物理 Fork の親 graph / events は **GET 時合成**（UI は分割非認知。[`fork-join.md`](docs/specifications/execution/fork-join.md)）
- **IO-14**: 既定で `input` / `output` を一覧 GET に含めない。ログは `LogRedaction`（[`io-log-masking.md`](docs/specifications/platform/io-log-masking.md)）
- **Engine 境界**: `ExecutionEngine` は `IStateExecutor` のみ。Catalog / Policy / ModuleHost は Service API 側。Hosted Fork の親子協調は Application（`execution_branches`・予約 Resume）
- **Execution Facade**: HTTP / Worker は `IExecutionService` のみ。実処理は `core/application` のドメインサービス（Query / Lifecycle / WaitEvent / Checkpoint / Ownership / Recovery / Projection）。境界は [`docs/architecture/domain-model-boundaries.md`](docs/architecture/domain-model-boundaries.md)
- **Serena MCP**: プロジェクトごとに `serena-engine` … `serena-ui` の 9 サーバー（`serena-runtime` 含む）。切替は UI トグル（同時 On は原則 1）。未起動時は停止して起動を促す。[`serena-mcp-project-toggle`](.spec/archive/specs/serena-mcp-project-toggle/requirements.md) / [`.cursor/skills/serena-mcp-project-switch/SKILL.md`](.cursor/skills/serena-mcp-project-switch/SKILL.md)

## .NET SDK

.NET 8: `/usr/local/share/dotnet`（Cloud VM）。PATH は `~/.bashrc` 参照。
