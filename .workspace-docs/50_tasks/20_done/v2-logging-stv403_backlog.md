# STV-403 完了記録（API リクエスト基本ログ）

- Version: 1.0.0
- 更新日: 2026-04-05
- 対象: `STV-403`
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`、`AGENTS.md`（Core-API HTTP リクエストログ）

---

本ファイルは `v2-ticket-backlog.md` から移した**完了チケット**の受け入れ・実装参照を保持する。

---

## 実装サマリ

| ID | 実装 |
|----|------|
| STV-403 | `api/Statevia.Core.Api/Hosting/RequestLoggingMiddleware.cs`（開始・完了・未処理例外）。`TraceIdResolver.cs`、`TraceContextEnrichmentMiddleware.cs`、`RequestLogContext.cs`、`RequestLogOptions.cs`、`LogBodyRedactor.cs`、`ResponseBodyLoggingStream.cs`。`Program.cs` でミドルウェア登録。単体テスト: `api/Statevia.Core.Api.Tests/Hosting/RequestLoggingMiddlewareTests.cs` 等。運用・項目説明: リポジトリ直下 `AGENTS.md`（Core-API: HTTP リクエストログ STV-403）。 |

---

### STV-403: API リクエスト基本ログを導入

- **spec-workflow**: `.spec-workflow/specs/api-request-basic-logging/`（`requirements.md` / `design.md` / `tasks.md`）
- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-1`
- 目的: API の開始/完了/例外を追跡可能にする
- スコープ:
  - request 開始ログ（`traceId`, `method`, `path`, `tenantId`）
  - request 完了ログ（`statusCode`, `elapsedMs`）
  - 例外ログ（5xx）
- 受け入れ条件:
  - 主要エンドポイントで開始/完了ログが出力される
  - `traceId` で相関できる
  - テストまたは検証手順をドキュメント化
- 依存: なし

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-05 | `STV-403` 完了に伴い `10_in-progress` から移設（ファイル名: `v2-logging-stv403_backlog.md`）。 |
