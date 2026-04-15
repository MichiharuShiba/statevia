# Design: Engine 実行ログ（STV-404 / LOG-2）

> **前提:** `requirements.md` は承認済み。本書はその受け入れ（`elapsedMs` 区間、Requirement 2b のログ手段・ライブラリ境界）を実装レベルで固定する。

## Overview

`engine/Statevia.Core.Engine` に **`Microsoft.Extensions.Logging.Abstractions`** を追加し、`WorkflowEngine` の実行経路（`RunWorkflowAsync` / `ScheduleStateAsync` / `RunJoinStateAsync` / `ProcessFact` 周辺）で構造化ログを出力する。ロガーは **`WorkflowEngineOptions` 経由でのみ**受け取り、実体は **`ILogger<WorkflowEngine>`**（または `ILoggerFactory` から製造）とする。

## Steering Document Alignment

`.spec-workflow/steering/tech.md` / `structure.md` に従い、Engine は **ライブラリ**として疎結合を維持する。

## Architecture

### ロガー取得（**採用: レビューにより案 A 確定**）

- **`WorkflowEngineOptions`** に `ILogger<WorkflowEngine>?` と `ILoggerFactory?` を**任意**で保持する。`WorkflowEngine` コンストラクタは **Options のみ**を受け取り、**ロガーをコンストラクタの別引数で渡す案は採用しない**（設計レビューで案 B 不採用）。
- **解決順**（実装で固定）: `ILogger<WorkflowEngine>` が非 null → それを使用。そうでなければ `ILoggerFactory` が非 null → `CreateLogger<WorkflowEngine>()`。両方 null → **`NullLogger<WorkflowEngine>.Instance`**。フィールド **`ILogger<WorkflowEngine>` は常に非 null**（Requirement 2b）。

Core-API の `Program.cs` では既存の `AddSingleton<IWorkflowEngine>(…)` 登録時に **`WorkflowEngineOptions` へロガーをセット**する。

### ログ出力とライブラリ境界

- **API**: `ILogger.Log*` / `LoggerMessage` デリゲートのみ使用。**Engine 内でログ専用の `Channel`、専用 `Task`、タイマーを起動しない**。
- **既定**: 注入なしなら **`NullLogger<WorkflowEngine>.Instance`** を使い、`ILogger<WorkflowEngine>` フィールドは **常に非 null**（null チェックを呼び出し側にばらまかない）。
- **故障分離**: 各ログブロックを `try/catch` で囲み、例外を握りつぶして **遷移ロジックへは伝えない**（観測失敗はサイレント許容。必要なら自前で診断カウンタは Out of Scope）。
- **ホスト**: Serilog / Console 等の **具体プロバイダ**はアプリ（Core-API / CLI）側の DI が登録する。Engine プロジェクトは **`Microsoft.Extensions.Logging.Abstractions` のみ**追加する。

### ログポイント（論理）

| イベント | レベル | 主なプロパティ |
|----------|--------|----------------|
| Workflow 開始 | Information | `WorkflowId`, `DefinitionId` または定義名（取得可なら）, `InitialState` |
| State 開始 | Information | `WorkflowId`, `StateName`, `NodeId` |
| State 完了 | Information | `WorkflowId`, `StateName`, `Fact`, `ElapsedMs` |
| State 実行例外 | Error | `WorkflowId`, `StateName`, `ErrorType`, `Message` |
| Workflow 終了（完了/失敗/キャンセル） | Information または Warning | `WorkflowId`, `Outcome` |

**Note:** `definitionName` は `CompiledWorkflowDefinition` から取れる場合のみ。無い場合は省略。

### 経過時間（`ElapsedMs`）

- **計測区間**: `_scheduler.RunAsync` に渡すラムダ内で、**`executor.ExecuteAsync(ctx, input, ct)` を呼ぶ直前**に `Stopwatch.StartNew()`（または `UtcNow` スナップショット）し、**同じ `ExecuteAsync` が完了した直後**（`finally` で fact が確定した時点）で止める。得られた経過を **`ElapsedMs`** として state 完了ログに載せる。
- **Wait 状態**: `ExecuteAsync` がイベント到着まで `await` する間も **タイマーは止めない**ため、**待機時間を含む**経過が `ElapsedMs` に乗る（requirements の「占有時間」と一致）。
- **Join ノード**（`RunJoinStateAsync` でグラフにノードを追加し `CompleteNode` するが `ExecuteAsync` を呼ばない経路）: **State 完了ログから `ElapsedMs` プロパティを省略する**（要件の「省略」案に合わせ、ゼロ埋めはしない）。
- 精度: `Stopwatch` 推奨（`DateTime` 差分よりオーバーヘッドと分解能のバランスがよい）。

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
