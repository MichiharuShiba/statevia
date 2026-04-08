# Tasks: workflow IO ログマスキング（STV-408）

**前提:** `STV-407` 完了推奨（キー名確定後にテスト期待値を固定しやすい）。

---

- [ ] 1. 現状調査: IO がログに載る経路の列挙
  - **内容:** API の本文ログ、Engine の将来ログ。載せない方針なら文書のみで要件を満たすか判断。
  - **Purpose:** スコープ確定。

- [ ] 2. 共通 `LogRedaction` を新設
  - **Files:** `engine/Statevia.Core.Engine/Infrastructure/Logging/LogRedaction.cs`（新規）
  - **Purpose:** Requirement 1。

- [ ] 3. API / Engine の適用先を共通実装に切替
  - **Files:** `api/Statevia.Core.Api/Hosting/RequestLoggingMiddleware.cs`, `api/Statevia.Core.Api/Hosting/LogBodyRedactor.cs`（必要なら委譲化）, `engine/Statevia.Core.Engine/**/*.cs`（IO ログ経路）
  - **Purpose:** Requirement 2。

- [ ] 4. テスト追加
  - **Files:** `api/Statevia.Core.Api.Tests/Hosting/`, `engine/Statevia.Core.Engine.Tests/`
  - **Purpose:** Requirement 3。

- [ ] 5. ドキュメント（IO-14 との相互リンク）
  - **Files:** `AGENTS.md` または `docs/`
  - **Purpose:** 運用。

---

## 完了チェック

1. 代表機密キーがマスクされる
2. テストで検証済み
