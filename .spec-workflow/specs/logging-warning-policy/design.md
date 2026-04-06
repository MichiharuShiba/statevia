# Design: Warning ポリシー（STV-405 / LOG-3）

## Overview

`STV-404` で導入した **`ILogger<WorkflowEngine>`** を利用し、次の 2 系統で `LogWarning` を発火する。

1. **Input 評価**: `StateInputEvaluator.Apply` 周辺で、欠損キー・型変換の補正・フォールバック等を検知したとき（**正確な条件は tasks でコード調査後に確定**）。
2. **遷移なし**: `ProcessFact` / `TransitionTable.Evaluate` の結果が **`HasTransition == false` かつ `End == false`** のとき（Join 待ち等と区別する分岐は `WorkflowEngine` の既存ロジックに従う）。

## Integration Points

- `engine/Statevia.Core.Engine/.../StateInputEvaluator.cs`（または該当クラス）
- `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs` — `ProcessFact` / FSM 評価直後

## Log Shape（論理フィールド）

| 系統 | 推奨プロパティ |
|------|----------------|
| Input 注意 | `WorkflowId`, `StateName`, `InputKey`, `Reason` |
| 遷移なし | `WorkflowId`, `StateName`, `Fact` |

命名の物理キー（Pascal / camel）は **`STV-407`** で最終統一。本段階では Engine 既存ログと揃える。

## Edge Cases

- **Join 待ち**で意図的に止まる場合は Warning にしない（誤検知防止）。
- **終端 `End`** に到達した場合は Warning にしない。

## Testing

- `WorkflowEngineTests` または Evaluator の単体テストで、**意図的に遷移なし**になる定義を用いて Warning が 1 回出ることを検証。

## References

- `engine/Statevia.Core.Engine/FSM/` — 遷移評価
- `v2-logging-v1-tasks.md` — Engine Warning 表
