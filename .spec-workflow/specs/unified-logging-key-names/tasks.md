# Tasks: ログキー名の統一（STV-407）

**前提:** `STV-403`・`STV-404` 実装済み。

---

- [x] 1. 既存ログ出力の棚卸し
  - **内容:** API Hosting / Engine の `LogInformation` 等を一覧化。
  - **Purpose:** design の表を確定。

- [x] 2. 命名規約ドキュメントの追加
  - **Files:** `docs/`（新規）または `AGENTS.md`
  - **Purpose:** Requirement 1。

- [x] 3. Core-API のキー名整合
  - **Files:** `api/Statevia.Core.Api/Hosting/*.cs`
  - **Purpose:** Requirement 2。

- [x] 4. Engine のキー名整合
  - **Files:** `engine/Statevia.Core.Engine/**/*.cs`
  - **Purpose:** Requirement 2。

- [x] 5. 回帰確認
  - **内容:** `dotnet test`（api / engine）。
  - **Purpose:** リネームの安全性。

---

## 完了チェック

1. docs に規約がある
2. API / Engine の主要ログが規約に従う
