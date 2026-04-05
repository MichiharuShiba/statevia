# Tasks: workflow IO ログマスキング（STV-408）

**前提:** `STV-407` 完了推奨（キー名確定後にテスト期待値を固定しやすい）。

---

- [ ] 1. 現状調査: IO がログに載る経路の列挙
  - **内容:** API の本文ログ、Engine の将来ログ。載せない方針なら文書のみで要件を満たすか判断。
  - **Purpose:** スコープ確定。

- [ ] 2. `LogBodyRedactor` の拡張（必要なら）
  - **Files:** `api/Statevia.Core.Api/Hosting/LogBodyRedactor.cs`
  - **Purpose:** Requirement 1。

- [ ] 3. 共有方針の実装（API / Engine）
  - **Files:** design に従う
  - **Purpose:** Requirement 2。

- [ ] 4. テスト追加
  - **Files:** `api/Statevia.Core.Api.Tests/Hosting/`
  - **Purpose:** Requirement 3。

- [ ] 5. ドキュメント（IO-14 との相互リンク）
  - **Files:** `AGENTS.md` または `docs/`
  - **Purpose:** 運用。

---

## 完了チェック

1. 代表機密キーがマスクされる
2. テストで検証済み
