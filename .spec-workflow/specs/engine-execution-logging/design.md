# Design: Engine 実行ログ（STV-404 / LOG-2）

## Overview

`engine/Statevia.Core.Engine` に **`Microsoft.Extensions.Logging.Abstractions`** を追加し、`WorkflowEngine` の実行経路（`RunWorkflowAsync` / `ScheduleStateAsync` / `RunJoinStateAsync` / `ProcessFact` 周辺）で構造化ログを出力する。ログは **`ILogger<WorkflowEngine>`**（または `WorkflowEngineOptions` 経由で注入する `ILoggerFactory`）を使用する。

## Steering Document Alignment

`.spec-workflow/steering/tech.md` / `structure.md` に従い、Engine は **ライブラリ**として疎結合を維持する。

## Architecture

### ロガー取得

- **案 A（推奨）**: `WorkflowEngineOptions` に `ILoggerFactory?` または `ILogger<WorkflowEngine>?` を追加し、`WorkflowEngine` コンストラクタで `NullLogger` 相当にフォールバック。
- **案 B**: `WorkflowEngine` のコンストラクタ引数に `ILogger<WorkflowEngine>?` を追加（破壊的変更を最小化するなら Options 優先）。

Core-API の `Program.cs` では既存の `AddSingleton<IWorkflowEngine>(…)` 登録時にロガーを解決して渡す。

### ログポイント（論理）

| イベント | レベル | 主なプロパティ |
|----------|--------|----------------|
| Workflow 開始 | Information | `WorkflowId`, `DefinitionId` または定義名（取得可なら）, `InitialState` |
| State 開始 | Information | `WorkflowId`, `StateName`, `NodeId` |
| State 完了 | Information | `WorkflowId`, `StateName`, `Fact`, `ElapsedMs` |
| State 実行例外 | Error | `WorkflowId`, `StateName`, `ErrorType`, `Message` |
| Workflow 終了（完了/失敗/キャンセル） | Information または Warning | `WorkflowId`, `Outcome` |

**Note:** `definitionName` は `CompiledWorkflowDefinition` から取れる場合のみ。無い場合は省略。

### 経過時間

- `Stopwatch` または `DateTime.UtcNow` 差分で state 実行ブロックの `ElapsedMs` を計測する。

### 例外とスタック

- API リクエストログ（STV-403）と整合し、**本番ではスタック省略**を `IHostEnvironment` 相当で分岐するか、Engine では **message のみ**とし詳細は呼び出し側に任せるかを **tasks** で確定。

## Code Reuse

- 既存の `ExecutionGraph` / `StateContext` は変更を最小化（`STV-406` で `Logger` を `StateContext` に載せる予定なら、本設計で `WorkflowEngine` 内の `ILogger` をその後共有注入してよい）。

## Modular Design Principles

- ログメッセージは **テンプレート + 構造化引数**（`LoggerMessage` ソースジェネレータ推奨）。
- 文字列補間で巨大オブジェクトを作らない。

## Testing Strategy

- `Microsoft.Extensions.Logging.Testing` または `FakeLogger` 相当で、**開始・失敗**の最低 1 ケースずつを `WorkflowEngineTests` に追加。

## References

- `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`
- `api/Statevia.Core.Api/Program.cs` — DI 登録
