# Tasks: StateContext に Logger を追加（STV-406）

**前提:** `STV-404` 完了（Engine にロガー注入パターンあり）。

---

- [ ] 1. `StateContext` にロガープロパティを追加
  - **Files:** `engine/Statevia.Core.Engine/Abstractions/StateContext.cs`
  - **Purpose:** Requirement 1。

- [ ] 2. `WorkflowEngine` で `Logger` を注入
  - **Files:** `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`
  - **内容:** `stateName` ごとにスコープ付与するか、コンテキストに `ILogger` を設定するかを実装。
  - **Purpose:** Requirement 2。

- [ ] 3. サンプルまたはテスト
  - **Files:** `engine/Statevia.Core.Engine.Tests` または `engine/samples/`
  - **Purpose:** Requirement 3。

- [ ] 4. 後方互換の確認
  - **内容:** 既存テスト・コンパイル全通過。
  - **Purpose:** Requirement 3。

---

## 完了チェック

1. サンプルまたはテストで `ctx.Logger` が利用可能
2. 既存実行パスが壊れない
