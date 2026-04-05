# Tasks: Warning ポリシー（STV-405）

**前提:** `STV-404` マージ済み（Engine に `ILogger` が届く）。

---

- [ ] 1. コード調査: Warning 候補箇所の洗い出し
  - **内容:** `StateInputEvaluator` と `WorkflowEngine.ProcessFact` / FSM の「遷移なし」分岐を確認し、`design.md` の Edge Cases を確定。
  - **Purpose:** 誤検知防止。

- [ ] 2. input 評価注意の Warning 実装
  - **Files:** Evaluator 系（調査結果に基づく）
  - **Purpose:** Requirement 1。

- [ ] 3. 遷移なし停止の Warning 実装
  - **Files:** `WorkflowEngine.cs`（または FSM ラッパ）
  - **Purpose:** Requirement 2。

- [ ] 4. 条件の明文化
  - **Files:** ソースコメント、または `docs/` 1 節
  - **Purpose:** Requirement 3。

- [ ] 5. 単体テスト（最低 1 ケース）
  - **Files:** `Statevia.Core.Engine.Tests`
  - **Purpose:** Requirement 3。

---

## 完了チェック

1. Warning 条件がコードまたは文書で追える
2. テストで Warning 経路が 1 ケース以上ある
