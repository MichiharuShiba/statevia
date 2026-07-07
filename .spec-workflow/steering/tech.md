# Technical Steering — Statevia

## Project Type

モノレポ構成の **ワークフローエンジン + REST API + Web UI**。バックエンドは **.NET 8**、フロントは **Node.js 上の Next.js**。

## Primary Languages & Runtimes

| 領域 | 言語 / ランタイム |
| --- | --- |
| Engine / Application / Infrastructure | C# / .NET 8 |
| Core-API / Action Host / CLI | C# / ASP.NET Core（.NET 8） |
| UI | TypeScript / React / Next.js |

## Key Dependencies（概要）

- **Core-API**: ASP.NET Core、**EF Core**、**Npgsql**、PostgreSQL への接続（`DATABASE_URL` 等）
- **Engine**: ワークフロー実行・FSM・グラフ（HTTP/DB に直接依存しないライブラリとして維持）
- **Application**: ユースケース実装（Engine + Contracts にのみ依存）
- **Infrastructure**: EF Core、JWT、SMTP、gRPC（core の契約を技術実装）
- **UI**: React、ReactFlow、Next.js（API 呼び出しは route handler 経由で Core-API にプロキシし CORS を回避）

## Application Architecture

- **Engine**: `IExecutionEngine` をシングルトンとして Core-API が利用。定義コンパイルは専用サービス経由。
- **Application**: ユースケース（`IDefinitionService`, `IExecutionService` 等）を実装。`Application.Contracts` のポート（Repository, UoW）経由で永続化。
- **Infrastructure**: `Application.Contracts` / `Actions.Abstractions` のポートを技術実装。
- **Core-API**: HTTP アダプタ + Composition Root。Controllers → Application Services → Repositories / Engine。例外は `ApiExceptionFilter` と契約エラー JSON に集約。
- **Persistence**: `CoreDbContext` は `IDbContextFactory<>` を主に使用。書き込み経路はユースケースとトランザクション境界を `AGENTS.md` に従う。

## Data Storage

- **PostgreSQL 16** を正とする。スキーマは EF Core マイグレーションで管理。

## Build & Quality

| 領域 | コマンド（例） |
| --- | --- |
| Engine | `cd core/engine && dotnet test statevia-engine.sln` |
| Core-API + Infrastructure + Architecture | `cd service/api && dotnet test statevia-api.sln` |
| Infrastructure (standalone) | `cd infrastructure && dotnet test statevia-infrastructure.sln` |
| Action Host | `cd service/action-host && dotnet test statevia-action-host.sln` |
| CLI | `cd service/cli && dotnet test statevia-cli.sln` |
| UI | `cd ui/studio && npm run test:run`（Vitest）、型は `tsc --noEmit` |

- **C# のコメント・XML・テスト**: `.cursor/rules/csharp-standards.mdc`。
- **TypeScript / React のコメント・テスト**: `.cursor/rules/typescript-standards.mdc`。
- 全体の参照は **`docs/development-guidelines.md` §4**。
- **ビルド／Analyzer／Markdown／UI の静的チェックの扱い**: 同上 **§4.3**。`.spec-workflow/**/*.md` はルート **`.markdownlint.json`** に従い、`markdownlint-cli2` で検証可能。

## Security & Compliance（概要）

- 契約上のエラー形式・ヘッダ（`X-Tenant-Id`, `X-Idempotency-Key` 等）は `docs/` に従う。
- ログ・観測性を追加する際は、機微クエリ／本文のマスキングと本番での本文ログ可否を設計に含める。

## Technical Decisions & Rationale

- **Core-API は C# のみ**: 単一スタックでの保守と EF / ホスティング統合のため。
- **UI からの API はプロキシ経由**: ブラウザ CORS を避け、同一オリジンで扱うため。
- **4 カテゴリ構成**: クリーン・アーキテクチャの依存方向をディレクトリで物理的に表現し、Architecture.Tests で機械的に維持するため。

## References

- `AGENTS.md`（DI、イベントストア、環境変数の表）
- `docs/development-guidelines.md`（コメント規約・リンター・品質ゲート）
- `docs/specifications/api-http.md`、`docs/specifications/data-integration.md`
