
# statevia

A definition-driven workflow engine based on fact-driven FSM.

---

## 1. Concept

This engine is designed around the following principles:

* Definition-driven workflow (JSON/YAML)
* Fact-driven finite state machine (FSM)
* Fork / Join as control nodes (not states)
* Explicit dependency declaration
* Asynchronous execution with cooperative cancellation
* Execution graph as an observation layer
* Engine does not interfere with user logic
* Safety-first design

The engine separates:

* Definition (what should happen)
* Execution (how it runs)
* Observation (what happened)

This separation allows reproducible execution, deterministic state transitions, and debuggable workflows.

---

## 2. Architecture Overview

```text
Definition (YAML / JSON)
   ↓
AST
   ↓
Compiler
   ↓
FSM / Fork / Join / JoinTracker
   ↓
Scheduler (parallelism control)
   ↓
State Executor (async execution)
   ↓
Execution Graph (observation)
```

Layered responsibilities:

* FSM: deterministic transitions based on facts
* Scheduler: parallelism and execution order
* Executor: actual state execution
* Execution Graph: history and visualization

---

## 3. Hello Workflow (Minimal Example)

### 3.1 Definition

```yaml
workflow:
  name: HelloWorkflow

states:
  Start:
    on:
      Completed:
        fork: [Prepare, AskUser]

  Prepare:
    on:
      Completed:
        next: Join1

  AskUser:
    wait:
      event: UserApproved
    on:
      Completed:
        next: Join1

  Join1:
    join:
      allOf: [Prepare, AskUser]
    on:
      Joined:
        next: Work

  Work:
    on:
      Completed:
        next: End

  End:
    on:
      Completed:
        end: true
```

---

### 3.2 State Implementation (C#)

```csharp
public sealed class StartState : IState<Unit, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
        => Task.FromResult(Unit.Value);
}

public sealed class PrepareState : IState<Unit, string>
{
    public async Task<string> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await Task.Delay(500, ct);
        return "prepared";
    }
}

public sealed class AskUserState : IState<Unit, bool>
{
    public async Task<bool> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await ctx.Events.WaitAsync("UserApproved", ct);
        return true;
    }
}

public sealed class WorkState : IState<(string prepared, bool approved), Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, (string, bool) input, CancellationToken ct)
        => Task.FromResult(Unit.Value);
}
```

---

### 3.3 Execution

```csharp
var engine = new WorkflowEngine(new WorkflowEngineOptions
{
    MaxParallelism = 2
});

var def = loader.Load(File.ReadAllText("hello.yaml"));
var id = engine.Start(def);

// Resume wait state
engine.PublishEvent("UserApproved");

// Optional cancel
// await engine.CancelAsync(id);
```

---

## 4. Execution Graph

The engine provides an execution graph that records:

* State execution nodes
* Fork / Join relationships
* Wait / Resume transitions
* Cancel propagation
* Time-based execution history

The execution graph can be exported as JSON and visualized by external tools.

---

## 5. Design Decisions

### 5.1 Fact-driven FSM

State transitions are triggered only by facts:

* Completed
* Failed
* Cancelled
* Joined

Requests (e.g., Cancel request) are not facts.

---

### 5.2 Fork / Join as Control Nodes

Fork and Join are not states.
They are control constructs that affect execution flow but do not execute user logic.

---

### 5.3 Cancellation

* Cancellation is cooperative.
* The engine does not forcefully abort running states.
* Cancelled is a fact emitted only when execution actually stops.

---

### 5.4 Parallelism Control

* Parallel execution is limited by the scheduler.
* Deadlock risks are detected but not automatically resolved.
* Users are responsible for execution policies.

---

## 6. Validation Levels

### LEVEL 1

* Syntax validation
* State reference integrity
* No self-transition (A -> A)
* Join references must exist

### LEVEL 2

* Join must be reachable from fork
* No circular joins
* No unreachable states
* Explicit dependency enforcement

---

## 7. Roadmap

* Expression engine for conditional transitions
* Retry / Timeout policies
* Sub-workflows
* Web UI for DAG visualization
* AI agent integration layer
* Multi-language SDK (NuGet / Maven / npm / pip)

---

## 8. License

TBD

---

## 9. Contribution

TBD

---

## 10. Status

This project is under active design and development.
Breaking changes may occur before the first stable release.

---

# 日本語

## 1. コンセプト

このエンジンは以下の原則に基づいて設計されています：

* 定義駆動型ワークフロー（JSON/YAML）
* 事実駆動型有限状態機械（FSM）
* Fork / Join を制御ノードとして（状態ではない）
* 明示的な依存関係宣言
* 協調的キャンセルによる非同期実行
* 実行グラフを観測レイヤーとして
* エンジンはユーザーロジックに干渉しない
* セーフティファースト設計

エンジンは以下を分離します：

* 定義（何が起こるべきか）
* 実行（どのように実行されるか）
* 観測（何が起こったか）

この分離により、再現可能な実行、決定論的な状態遷移、デバッグ可能なワークフローが実現されます。

---

## 2. アーキテクチャ概要

```text
定義 (YAML / JSON)
   ↓
AST
   ↓
コンパイラ
   ↓
FSM / Fork / Join / JoinTracker
   ↓
スケジューラ（並列度制御）
   ↓
状態エグゼキューター（非同期実行）
   ↓
実行グラフ（観測）
```

レイヤーごとの責務：

* FSM: 事実に基づく決定論的遷移
* スケジューラ: 並列度と実行順序
* エグゼキューター: 実際の状態実行
* 実行グラフ: 履歴と可視化

---

## 3. Hello Workflow（最小例）

### 3.1 定義

（上記 YAML 定義を参照）

### 3.2 状態実装（C#）

（上記 C# コードを参照）

### 3.3 実行

（上記 C# 実行コードを参照）

---

## 4. 実行グラフ

エンジンは以下を記録する実行グラフを提供します：

* 状態実行ノード
* Fork / Join の関係
* Wait / Resume 遷移
* キャンセル伝播
* 時間ベースの実行履歴

実行グラフは JSON としてエクスポートでき、外部ツールで可視化できます。

---

## 5. 設計上の決定

### 5.1 事実駆動型 FSM

状態遷移は事実によってのみトリガーされます：

* Completed（完了）
* Failed（失敗）
* Cancelled（キャンセル済み）
* Joined（結合済み）

リクエスト（例：キャンセルリクエスト）は事実ではありません。

### 5.2 Fork / Join を制御ノードとして

Fork と Join は状態ではありません。実行フローに影響を与える制御構造であり、ユーザーロジックは実行しません。

### 5.3 キャンセル

* キャンセルは協調的です。
* エンジンは実行中の状態を強制終了しません。
* Cancelled は実行が実際に停止したときにのみ発行される事実です。

### 5.4 並列度制御

* 並列実行はスケジューラによって制限されます。
* デッドロックリスクは検出されますが、自動解決はされません。
* 実行ポリシーはユーザーの責任です。

---

## 6. 検証レベル

### LEVEL 1

* 構文検証
* 状態参照の整合性
* 自己遷移の禁止（A -> A）
* Join 参照が存在すること

### レベル 2

* Fork から Join への到達可能性
* 循環 Join の禁止
* 到達不能状態の禁止
* 明示的依存関係の強制

---

## 7. ロードマップ

* 条件付き遷移のための式エンジン
* リトライ / タイムアウトポリシー
* サブワークフロー
* DAG 可視化のための Web UI
* AI エージェント統合レイヤー
* マルチ言語 SDK（NuGet / Maven / npm / pip）

---

## 8. ライセンス

TBD

---

## 9. 貢献

TBD

---

## 10. ステータス

本プロジェクトは設計・開発中です。初回安定版リリース前に破壊的変更が発生する可能性があります。
