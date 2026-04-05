# Tasks: Engine 実行ログ（STV-404）

実装順に実行する。承認後、`tasks.md` のチェックを `[x]` に更新する。

---

- [ ] 1. `Microsoft.Extensions.Logging.Abstractions` を Engine プロジェクトに追加
  - **Files:** `engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`
  - **Purpose:** `ILogger` 利用の前提。

- [ ] 2. `WorkflowEngineOptions` とコンストラクタにロガー注入
  - **Files:** `engine/Statevia.Core.Engine/Engine/WorkflowEngineOptions.cs`, `WorkflowEngine.cs`
  - **内容:** `ILogger<WorkflowEngine>?` または `ILoggerFactory?` を受け取り、未指定時はノーログで既存挙動を維持。
  - **Purpose:** Core-API から DI で解決可能にする。

- [ ] 3. 実行経路へのログ（workflow / state / 失敗）
  - **Files:** `WorkflowEngine.cs`
  - **内容:** `design.md` のログポイント表に沿い、Requirement 1–3 を満たす。
  - **Purpose:** STV-404 の受け入れ。

- [ ] 4. Core-API の DI 更新
  - **Files:** `api/Statevia.Core.Api/Program.cs`
  - **内容:** `IWorkflowEngine` 登録時にロガーを渡す。
  - **Purpose:** 本番・開発でログが出ること。

- [ ] 5. 単体テスト
  - **Files:** `engine/Statevia.Core.Engine.Tests/Engine/WorkflowEngineTests.cs`（または新規）
  - **内容:** FakeLogger で主要フィールドを検証。
  - **Purpose:** Requirement 4。

- [ ] 6. ドキュメント追記（任意・最小）
  - **Files:** `AGENTS.md` または `docs/` の短い節
  - **内容:** Engine 実行ログのフィールド概要。
  - **Purpose:** 運用者向け。

---

## 完了チェック（STV-404 受け入れ）

1. `workflowId` / `stateName` を含むログが出る
2. 失敗経路で Error ログが出る
3. `dotnet test`（engine）が green
