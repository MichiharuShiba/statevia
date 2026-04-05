# Technical Steering — Statevia

## Project Type

モノレポ構成の **ワークフローエンジン + REST API + Web UI**。バックエンドは **.NET 8**、フロントは **Node.js 上の Next.js**。

## Primary Languages & Runtimes

| 領域 | 言語 / ランタイム |
|------|-------------------|
| Engine | C# / .NET 8 |
| Core-API | C# / ASP.NET Core（.NET 8） |
| UI | TypeScript / React / Next.js |

## Key Dependencies（概要）

- **Core-API**: ASP.NET Core、**EF Core**、**Npgsql**、PostgreSQL への接続（`DATABASE_URL` 等）
- **Engine**: ワークフロー実行・FSM・グラフ（HTTP/DB に直接依存しないライブラリとして維持）
- **UI**: React、ReactFlow、Next.js（API 呼び出しは route handler 経由で Core-API にプロキシし CORS を回避）

## Application Architecture

- **Engine**: `IWorkflowEngine` をシングルトンとして Core-API が利用。定義コンパイルは専用サービス経由。
- **Core-API**: Controllers → Services → Repositories / `IWorkflowEngine`。例外は `ApiExceptionFilter` と契約エラー JSON に集約。
- **Persistence**: `CoreDbContext` は `IDbContextFactory<>` を主に使用。書き込み経路はユースケースとトランザクション境界を `AGENTS.md` に従う。

## Data Storage

- **PostgreSQL 16** を正とする。スキーマは EF Core マイグレーションで管理。

## Build & Quality

| 領域 | コマンド（例） |
|------|----------------|
| Engine | `cd engine && dotnet test statevia-engine.sln` |
| Core-API | `cd api && dotnet test statevia-api.sln` |
| UI | `cd services/ui && npm run test:run`（Vitest）、型は `tsc --noEmit` |

- **コメント・XML・テストの書き方**: `.cursor/rules/csharp-standards.mdc`（詳細は **`docs/development-guidelines.md` §4**）。
- **ビルド／Analyzer／Markdown／UI の静的チェックの扱い**: 同上 **§4.2**。`.spec-workflow/**/*.md` はルート **`.markdownlint.json`** に従い、`markdownlint-cli2` で検証可能。

## Security & Compliance（概要）

- 契約上のエラー形式・ヘッダ（`X-Tenant-Id`, `X-Idempotency-Key` 等）は `docs/` に従う。
- ログ・観測性を追加する際は、機微クエリ／本文のマスキングと本番での本文ログ可否を設計に含める。

## Technical Decisions & Rationale

- **Core-API は C# のみ**: 単一スタックでの保守と EF / ホスティング統合のため。
- **UI からの API はプロキシ経由**: ブラウザ CORS を避け、同一オリジンで扱うため。

## References

- `AGENTS.md`（DI、イベントストア、環境変数の表）
- `docs/development-guidelines.md`（コメント規約・リンター・品質ゲート）
- `docs/core-api-interface.md`、`docs/statevia-data-integration-contract.md`
