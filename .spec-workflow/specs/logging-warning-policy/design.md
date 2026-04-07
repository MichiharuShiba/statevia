# Design Document: Warning Policy (STV-405 / LOG-3)

## Overview

STV-405は、運用上注意が必要なものの致命的ではない実行状況（フォールバック等）を明確に記録するため、直近のSTV-404で導入された `WorkflowExecutionLogger` に明示的な `LogWarning` 実装を追加するものです。
具体的には、1) state input 評価時に代替（フォールバック）や補正が必要だった場合、2) FSM評価の結果として「遷移先なし」となった場合（正常な一時停止やEndではない場合）に警告ログを出力します。

## Steering Document Alignment

### Technical Standards (tech.md)

ロギング時の例外がワークフロー処理本体をクラッシュさせないよう、既存の `WorkflowExecutionLogger` 内にある `SafeLog` パターンを利用します。

### Project Structure (structure.md)

変更は既存の依存関係アーキテクチャに準拠し、主ロジックは `Statevia.Core.Engine` クラスに、テストは `Statevia.Core.Engine.Tests` に限定して追加します。

## Code Reuse Analysis

### Existing Components to Leverage

- **`WorkflowExecutionLogger`**: `WorkflowEngine.Logging.cs` に配置されています。ここに `SafeLog` でラップした `LogWarningInputEvaluation` と `LogWarningNoTransition` メソッドを新設します。
- **`StateInputEvaluator`**: input の検証を担います。`WorkflowEngine.ApplyStateInput` 呼び出し内でフォールバックシナリオが検知された際に警告を発火します。

### Integration Points

- **`WorkflowEngine.ProcessFact`**: `TransitionTable` の評価結果にフックします。`HasTransition == false` かつ `End == false` であれば、`WorkflowExecutionLogger.LogWarningNoTransition` 経由で警告を出力します。

## Architecture

STV-404で導入された `WorkflowExecutionLogger` の設計を踏襲します。Loggerインスタンスは `WorkflowEngine` が保持しており、警告条件を満たしたときに必要なメタデータ（`WorkflowId`, `StateName` 等）を引き渡します。純粋関数（TransitionTable等）の深部までLoggerを引き回すことは避け、オーケストレーション層であるEngineでのみ発火させます。

## Components and Interfaces

### WorkflowExecutionLogger

- **Purpose:** ワークフロー実行イベントのための安全なロギングラッパーを提供します。
- **Interfaces:**
  - `void LogWarningInputEvaluation(Guid workflowId, string stateName, string inputKey, string reason)`
  - `void LogWarningNoTransition(Guid workflowId, string stateName, string fact)`
- **Dependencies:** `ILogger`
- **Reuses:** 例外のキャッチ＆ラップを行う `SafeLog` ヘルパーメソッド。

### WorkflowEngine

- **Purpose:** 中心となる実行エンジン。
- **Interfaces:** なし（`ApplyStateInput` と `ProcessFact` 内への内部ロジック追加のみ）。
- **Dependencies:** `WorkflowExecutionLogger`
- **Reuses:** なし

## Data Models

*(DBの新規モデルは不要です。ログ出力のフィールド構成のみ)*

### Warning Log Fields

```csharp
// ログ用の概念的構造
- WorkflowId: Guid
- StateName: string
- InputKey: string (任意、Input Evaluation 用)
- Reason: string (任意、Input Evaluation 用)
- Fact: string (任意、No Transition 用)
```

## Error Handling

### Error Scenarios

1. **Scenario 1:** ログの書き込みに失敗した場合（ApplicationInsightsの障害やディスクフルなど）
   - **Handling:** `WorkflowExecutionLogger.SafeLog()` 内部でキャッチされます。ランタイム実行に被災させません。
   - **User Impact:** ワークフローの進行には一切影響を与えません（実行継続）。

## Testing Strategy

### Unit Testing

- `WorkflowEngineLoggingTests.cs` にテストを追加します。
- `WorkflowEngine` に `ILogger` モックを注入します。
- FSM評価で遷移なしの状況を意図的に引き起こし、`LogWarning` が呼ばれることをアサーションします。
- StateInputのフォールバックシナリオを引き起こし、`LogWarning` が呼ばれることをアサーションします。
