# Design: StateContext に Logger を追加（STV-406 / LOG-4）

## Overview

`StateContext` に **`ILogger? Logger { get; init; }`** または **`ILogger StateLogger { get; }`** を追加する。`WorkflowEngine.ScheduleStateAsync` で `StateContext` を生成する際、**`WorkflowEngine` が保持する `ILogger<WorkflowEngine>` からファクトリを用いて** `ILogger` を供給する。

### 推奨パターン

- **`ILogger` をそのまま載せる**: 最も単純。カテゴリは `Statevia.Core.Engine.State` 等を `LoggerFactory.CreateLogger` で生成。
- **`BeginScope`**: `WorkflowId` / `StateName` をスコープ状態として付与し、子ログは親コンテキストを継承。

```csharp
// 概念例（実装は tasks で確定）
using (_logger.BeginScope(new Dictionary<string, object> {
    ["WorkflowId"] = instance.WorkflowId,
    ["StateName"] = stateName
}))
{
    await executor.ExecuteAsync(ctx, input, ct);
}
```

## Backward Compatibility

- `StateContext` は **record / init プロパティ**のため、既存のオブジェクト初期化子で **`Logger` を省略**すると `null`。実行時は **`NullLogger.Instance`** をプロパティ getter で返すか、**`WorkflowEngine` が必ず設定**する。

## Integration Points

- `Statevia.Core.Engine.Abstractions.StateContext`
- `WorkflowEngine.ScheduleStateAsync` / `RunJoinStateAsync`

## Testing

- 既存 `WorkflowEngineTests` に、**FakeLogger** を注入した `StateContext` 経由でログが出るケースを追加。

## References

- `.spec-workflow/specs/engine-execution-logging/design.md`
