# STV-404 完了記録（Engine 実行ログ）

- Version: 1.0.0
- 更新日: 2026-04-05
- 対象: `STV-404`
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`、`AGENTS.md`（Engine 実行ログ STV-404）、`.spec-workflow/specs/engine-execution-logging/`

---

本ファイルは `v2-ticket-backlog.md` から移した**完了チケット**の受け入れ・実装参照を保持する。

---

## 実装サマリ

| ID | 実装 |
|----|------|
| STV-404 | `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`（本流ロジック）、`WorkflowEngine.Logging.cs`（`WorkflowExecutionLogger` ＋ `SafeLog`）。`WorkflowEngineOptions.cs`（`Logger` / `LoggerFactory` 解決）。`Microsoft.Extensions.Logging.Abstractions` 参照。Core-API では `api/Statevia.Core.Api/Program.cs` が `ILogger<WorkflowEngine>` を Options へ注入。単体テスト: `engine/Statevia.Core.Engine.Tests/Engine/WorkflowEngineLoggingTests.cs`。運用・要約: リポジトリ直下 `AGENTS.md`（Engine 実行ログ STV-404）。 |

---

### STV-404: Engine 実行ログを導入

- **spec-workflow**: `.spec-workflow/specs/engine-execution-logging/`（`requirements.md` / `design.md` / `tasks.md`）
- 優先度: **P2**
- 元タスク: `logging-v1-tasks.md` の `LOG-2`
- 目的: workflow/state のライフサイクルを可視化する
- スコープ:
  - workflow 開始/終了（完了・失敗・キャンセル）
  - state 開始/完了
- 受け入れ条件:
  - workflowId/stateName を含むログが出る
  - 失敗経路で Error ログが出る
- 依存: なし

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-05 | `STV-404` 完了に伴い `10_in-progress` から移設（ファイル名: `v2-logging-stv404_backlog.md`）。 |
