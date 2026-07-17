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
| core-api（ASP.NET Core） | `service/api/` |
| action-host（gRPC） | `service/action-host/` |
| cli | `service/cli/` |
| ui（Next.js） | `ui/studio/` |

PostgreSQL 16 + EF Core。UI は `/api/core/*` で Core-API にプロキシ。

## 起動

1. **PostgreSQL** — Docker 例: `docker compose up -d postgres`、または [`docs/guides/getting-started.md`](docs/guides/getting-started.md)
2. **マイグレーション** — `cd service/api && dotnet ef database update --project Statevia.Service.Api`
3. **Core-API** — `cd service/api && dotnet run --project Statevia.Service.Api --no-launch-profile`（`ASPNETCORE_URLS=http://0.0.0.0:8080` 推奨。`launchSettings.json` のポートに注意）
4. **UI** — `cd ui/studio && CORE_API_INTERNAL_BASE=http://localhost:8080 npm run dev`

Docker Compose 一式: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)

OpenAPI / Scalar: [`docs/reference/api-openapi.md`](docs/reference/api-openapi.md)

### Docker gotcha (Cloud VM)

`fuse-overlayfs` + `iptables-legacy`。`sudo dockerd` で手動起動が必要な場合あり。

## テスト

| 対象 | コマンド |
| --- | --- |
| engine | `cd core/engine && dotnet test statevia-engine.sln` |
| core-api | `cd service/api && dotnet test statevia-api.sln` |
| ui | `cd ui/studio && npm run lint && npm run typecheck && npm run test:run` |

Sonar / Analyzer: [`docs/development-guidelines.md`](docs/development-guidelines.md) §4.3 / §5

## 主要 env / 設定（抜粋）

| 変数 / 設定 | 用途 |
| --- | --- |
| `DATABASE_URL` | PostgreSQL 接続 |
| `CORE_API_INTERNAL_BASE` | UI → Core-API |
| `STATEVIA_ENABLE_API_DOCS` | 本番で OpenAPI / Scalar を有効化 |
| `STATEVIA_MODULES_PATH` | Action Module ルート |
| `Statevia:ActionHost:BaseUrl` | OutOfProcess 実行（未設定時は `ActionHostNotConfigured`） |
| `Statevia:Modules:Signing:*` | Module 署名・TrustLevel |
| `Statevia:Modules:Oci:*` | OCI Module Source |
| `Statevia:Modules:S3:*` | S3 Module Source |
| `Statevia:Modules:Git:*` | Git Module Source（HTTP archive） |
| `Statevia:ExecutionPolicy:*` | 実行モード下限・テナント Policy |

完全な一覧と説明: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)、[`docs/specifications/actions/platform.md`](docs/specifications/actions/platform.md)

## 実装メモ（エージェント向け）

- **Read-model**: `GET /v1/executions` / graph は DB projection 正本（[`data-integration.md`](docs/specifications/data-integration.md)）
- **IO-14**: 既定で `input` / `output` を一覧 GET に含めない。ログは `LogRedaction`（[`io-log-masking.md`](docs/specifications/platform/io-log-masking.md)）
- **Engine 境界**: `ExecutionEngine` は `IStateExecutor` のみ。Catalog / Policy / ModuleHost は Core-API 側

## .NET SDK

.NET 8: `/usr/local/share/dotnet`（Cloud VM）。PATH は `~/.bashrc` 参照。
