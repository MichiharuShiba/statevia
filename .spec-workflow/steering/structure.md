# Project Structure Steering — Statevia

## Top-Level Layout

```text
engine/                 # ワークフロー実行エンジン（C# ライブラリ；既存 CLI は cli/ へ段階移行）
api/                    # Core-API（ASP.NET Core）— HTTP 契約の正
cli/                    # 統合 CLI（statevia コマンド；module install 等）
shared/                 # 横断共有ライブラリ（例: Statevia.Modules）
services/ui/            # Web ダッシュボード（Next.js）
docs/                   # 契約・運用・開発ガイド
```

## Core-API（`api/Statevia.Core.Api/`）

レイヤー責務の目安（詳細は `AGENTS.md`）。

| 領域 | 役割 | 配置の例 |
|------|------|----------|
| Controllers | ルート・ヘッダ・バインディング・HTTP ステータス | `Controllers/` |
| Services | ユースケース（Repository・DisplayId・dedup・Engine 連携） | `Services/`、インターフェースは `Abstractions/Services/` |
| Repositories | 永続化のみ | `Persistence/Repositories/`、`Abstractions/Persistence/` |
| Hosting | HTTP パイプラインに近い横断関心（テナントヘッダ、リクエストログ等） | `Hosting/` |
| Infrastructure | ID 生成など横断インフラ | `Infrastructure/` |

- **read-model 系**で Repository 表面を増やさず局所クエリするサービスは、`IDbContextFactory<CoreDbContext>` を直接使う場合がある（`AGENTS.md` の Persistence note）。

## Engine（`engine/`）

- ワークフロー定義の実行・グラフ／状態機械の責務に限定する。
- **HTTP や DB に直接依存しない**（Core-API が境界となる）。
- `engine/Statevia.Cli` は開発用の既存 CLI。**ユーザー向け統合 CLI は `cli/Statevia.Cli` に集約**する（段階移行）。

## CLI（`cli/`）

- **`cli/Statevia.Cli`**: 統合 `statevia` コマンド（platform / module / 定義検証など）。
- **`cli/statevia-cli.sln`**: CLI 用ソリューション。
- Engine 向けの旧 CLI 機能はサブコマンドとして移行し、最終的に `engine/Statevia.Cli` は廃止または thin wrapper とする。

## Shared（`shared/`）

- **`shared/Statevia.Modules`**: modules ルート解決（`ModulePathResolver`）など、**API と CLI の両方**から参照する横断ライブラリ。
- ASP.NET 固有の型は API 側アダプタに留め、shared はフレームワーク非依存を優先する。

## UI（`services/ui/`）

- Next.js の route handler で Core-API にプロキシし、ブラウザからは同一オリジンの `/api/core/...` 等を利用。
- 環境変数 `CORE_API_INTERNAL_BASE` でバックエンド基底 URL を指定（`AGENTS.md` の表）。

## Documentation & Specs

| 種類 | 場所 |
|------|------|
| エージェント／起動・テスト | ルート `AGENTS.md` |
| 開発の共通ルール | `docs/development-guidelines.md` |
| Spec Workflow（本 Steering を含む） | `.spec-workflow/` |
| 作業用メモ・タスク（任意） | `.workspace-docs/`（入口 `README.md`） |

## Module Boundaries

- **Engine ↔ Core-API**: 公開 API（`IExecutionEngine` 等）越しのみ。
- **Core-API ↔ PostgreSQL**: DbContext / Repository 経由。イベントストアやトランザクション境界は `AGENTS.md` の event_store 記述に従う。
- **UI ↔ Core-API**: HTTP（プロキシ）のみ。DB に直接接続しない。
- **CLI ↔ Core-API**: HTTP（reload 等）または **filesystem 上の modules ルート共有**（`shared/Statevia.Modules`）。CLI は DB に直接接続しない。
- **shared ↔ api/cli**: 共有はパス解決・軽量 DTO に限定し、ModuleHost や IActionRegistry は API 内に閉じる。

## References

- `AGENTS.md` の Architecture overview と Core-API layers 表
