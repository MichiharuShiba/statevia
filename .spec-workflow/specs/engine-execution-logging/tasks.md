# Tasks: Engine 実行ログ（STV-404）

**前提:** `requirements.md` 承認済み（`elapsedMs` 区間・Wait 含む、Requirement 2b）。

実装順に実行する。承認後、`tasks.md` のチェックを `[x]` に更新する。

---

- [x] 1. `Microsoft.Extensions.Logging.Abstractions` を Engine プロジェクトに追加
  - **Files:** `engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`
  - **Purpose:** `ILogger` 利用の前提。

- [x] 2. `WorkflowEngineOptions` とコンストラクタにロガー注入（**案 A 確定: Options のみ**）
  - **Files:** `engine/Statevia.Core.Engine/Engine/WorkflowEngineOptions.cs`, `WorkflowEngine.cs`
  - **内容:** Options に `ILogger<WorkflowEngine>?` と `ILoggerFactory?` を追加。**解決順**は `design.md`（直接指定 → Factory → Null）。**コンストラクタ直引数で `ILogger` を渡さない**。
  - **Purpose:** Core-API から DI で `WorkflowEngineOptions` にロガーをセットでき、単体 `new WorkflowEngine()` でログ基盤を要求しない（Requirement 2b）。

- [x] 3. 実行経路へのログ（workflow / state / 失敗）
  - **Files:** `WorkflowEngine.cs`
  - **内容:**
    - `design.md` のログポイント表に沿い、Requirement 1–3 を満たす。
    - **各 `Log*` 呼び出し**: プロバイダ例外を握りつぶす **`try/catch`** で囲む（Requirement 2b / design）。
    - **`ElapsedMs`**: `_scheduler.RunAsync` 内、`ExecuteAsync` 直前〜完了直後で `Stopwatch`（Wait の `await` 中も計測に含む）。**Join（`ExecuteAsync` なし）の State 完了ログでは `ElapsedMs` を出さない**。
  - **Purpose:** STV-404 の受け入れ。

- [x] 4. Core-API の DI 更新
  - **Files:** `api/Statevia.Core.Api/Program.cs`
  - **内容:** `IWorkflowEngine` 登録時にロガーを渡す。
  - **Purpose:** 本番・開発でログが出ること。

- [x] 5. 単体テスト
  - **Files:** `engine/Statevia.Core.Engine.Tests/Engine/WorkflowEngineTests.cs`（または新規）
  - **内容:** FakeLogger で主要フィールドを検証。**開始・失敗**に加え、可能なら **State 完了ログに `ElapsedMs` が載る**こと（通常 state のみ；Join 経路では欠如）を 1 ケース。
  - **Purpose:** Requirement 4。

- [x] 6. ドキュメント追記（任意・最小）
  - **Files:** `AGENTS.md` または `docs/` の短い節
  - **内容:** Engine 実行ログのフィールド概要。
  - **Purpose:** 運用者向け。

---

## 完了チェック（STV-404 受け入れ）

1. `workflowId` / `stateName` を含むログが出る
2. 失敗経路で Error ログが出る
3. `dotnet test`（engine）が green
