# Project Structure Steering — Statevia

## Top-Level Layout

```text
core/                  # ドメイン・契約（非デプロイ）
infrastructure/        # 技術実装（差し替え可能・最外殻）
service/               # デプロイ可能なインターフェイス（API / CLI / action-host）
ui/studio/             # Web ダッシュボード（Next.js — @statevia/studio）
tests/                 # 横断テスト（Architecture.Tests）
docs/                  # 契約・運用・開発ガイド
scripts/               # ビルド・運用スクリプト
```

## Core（`core/`）

ドメインと契約。HTTP/DB に直接依存しない。

| プロジェクト | 役割 |
| --- | --- |
| `core/engine/Statevia.Core.Engine` | ワークフローエンジン（FSM, Graph, Scheduler） |
| `core/application/Statevia.Core.Application` | ユースケース実装 |
| `core/application/Statevia.Core.Application.Contracts` | DDD ポート / DTO（リポジトリ、UoW、サービスインターフェース） |
| `core/actions/Statevia.Core.Actions.Abstractions` | Action / Module SPI |

## Infrastructure（`infrastructure/`）

core の契約（`Application.Contracts` / `Actions.Abstractions`）の技術実装。

| プロジェクト | 役割 |
| --- | --- |
| `Statevia.Infrastructure.Persistence` | EF Core + Migrations |
| `Statevia.Infrastructure.Security` | JWT, Tenant context, 認可 |
| `Statevia.Infrastructure.Notification` | SMTP 通知 |
| `Statevia.Infrastructure.Modules` | Module ホスト + OCI/filesystem Source |
| `Statevia.Infrastructure.Actions.Grpc` | gRPC Action Backend |
| `Statevia.Infrastructure.Common` | IdGenerator (UUID v7) 等 |

## Service（`service/`）

HTTP / gRPC / CLI のアダプタ。Composition Root として全層を DI で結合。

| プロジェクト | 役割 |
| --- | --- |
| `service/api/Statevia.Service.Api` | HTTP アダプタ（Controllers + Hosting） |
| `service/api/Statevia.Service.Api.Bootstrap` | エントリポイント（Program.cs） |
| `service/action-host/Statevia.Service.ActionHost` | OutOfProcess Action 実行（gRPC sandbox） |
| `service/cli/Statevia.Service.Cli` | 統合 `statevia` コマンド |

## UI（`ui/studio/`）

- Next.js の route handler で Core-API にプロキシし、ブラウザからは同一オリジンの `/api/core/...` 等を利用。
- 環境変数 `CORE_API_INTERNAL_BASE` でバックエンド基底 URL を指定（`AGENTS.md` の表）。

## Tests（`tests/`）

- `tests/Statevia.Architecture.Tests`: `NetArchTest.eNhancedEdition` で依存方向禁止ルールを機械的に検証。

## Module Boundaries

- **Engine ↔ Application**: `IExecutionEngine` 越しのみ。Application はユースケースを実装し Engine を利用。
- **Application ↔ Infrastructure**: `Application.Contracts` のポート越しのみ。Infrastructure が実装を提供。
- **Service ↔ Core/Infrastructure**: DI で結合。Service は HTTP/gRPC 境界のみ担当。
- **UI ↔ Service**: HTTP（プロキシ）のみ。DB に直接接続しない。

## Documentation & Specs

| 種類 | 場所 |
| --- | --- |
| エージェント／起動・テスト | ルート `AGENTS.md` |
| 開発の共通ルール | `docs/development-guidelines.md` |
| Spec Workflow（本 Steering を含む） | `.spec-workflow/` |
| 作業用メモ・タスク（任意） | `.workspace-docs/`（入口 `README.md`） |

## References

- `AGENTS.md` の Architecture overview と Core-API layers 表
