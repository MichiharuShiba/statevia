# Tasks: ログ関連テスト（STV-409）

**前提:** `STV-403`～`STV-408` の実装状況に応じて順次実施。

---

- [x] 1. ギャップ分析
  - **内容:** 既存テストでカバー済みのログ経路と不足を一覧化。
  - **Purpose:** Requirement 1。

- [x] 2. API ログの不足テストを追加
  - **Files:** `api/Statevia.Core.Api.Tests/Hosting/`
  - **Purpose:** Requirement 1。

- [x] 3. Engine ログの不足テストを追加
  - **Files:** `engine/Statevia.Core.Engine.Tests/`
  - **Purpose:** Requirement 1。

- [x] 4. Warning / Error 経路（利用可能なら）
  - **内容:** `STV-405` 未マージならタスクをブロックまたはスキップ理由を記載。
  - **Purpose:** Requirement 1。

- [x] 5. （任意）テストマップの 1 段落
  - **Files:** `docs/` または `AGENTS.md`
  - **Purpose:** Requirement 3。

---

## 完了チェック

1. `dotnet test`（api / engine） green
2. 重要ログ経路がテストで保護されている
